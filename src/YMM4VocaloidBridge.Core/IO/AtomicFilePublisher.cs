namespace YMM4VocaloidBridge.Core.IO;

internal static class AtomicFilePublisher
{
    private const int MoveAttempts = 40;

    public static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporary = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destination)}.tmp-{Guid.NewGuid():N}");

        try
        {
            await using (var input = File.Open(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete))
            await using (var output = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await MoveIntoPlaceAsync(temporary, destination, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task MoveIntoPlaceAsync(
        string temporary,
        string destination,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporary, destination, overwrite: true);
                return;
            }
            catch (IOException exception) when (IsSharingViolation(exception) && attempt < MoveAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MoveAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSharingViolation(IOException exception)
    {
        var nativeErrorCode = exception.HResult & 0xFFFF;
        return nativeErrorCode is 32 or 33;
    }
}
