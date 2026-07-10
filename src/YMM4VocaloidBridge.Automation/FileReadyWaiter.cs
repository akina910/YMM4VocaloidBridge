using YMM4VocaloidBridge.Core.Audio;

namespace YMM4VocaloidBridge.Automation;

public sealed class FileReadyWaiter(WaveFileValidator validator)
{
    public async Task WaitForWaveAsync(string path, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        long? previousLength = null;
        var stableObservations = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                var length = new FileInfo(path).Length;
                stableObservations = previousLength == length && length > 44 ? stableObservations + 1 : 0;
                previousLength = length;

                if (stableObservations >= 2)
                {
                    try
                    {
                        _ = validator.Validate(path);
                        return;
                    }
                    catch (IOException)
                    {
                    }
                    catch (InvalidDataException)
                    {
                    }
                }
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"VOCALOID6 did not produce a valid WAVE file within {timeout.TotalSeconds:0} seconds.");
    }
}
