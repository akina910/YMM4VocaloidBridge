using System.Globalization;
using System.Text;

namespace YMM4VocaloidBridge.Core.LipSync;

public sealed class LabWriter
{
    public async Task WriteAsync(IReadOnlyList<LipSyncFrame> frames, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count < 2)
        {
            throw new ArgumentException("At least two lip-sync frames are required.", nameof(frames));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var builder = new StringBuilder();
        for (var index = 0; index < frames.Count - 1; index++)
        {
            var start = frames[index].Time.Ticks;
            var end = frames[index + 1].Time.Ticks;
            if (end <= start)
            {
                continue;
            }

            builder.Append(start.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(end.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ToPhoneme(frames[index].Shape));
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    private static string ToPhoneme(Reading.MouthShape shape) => shape switch
    {
        Reading.MouthShape.A => "a",
        Reading.MouthShape.I => "i",
        Reading.MouthShape.U => "u",
        Reading.MouthShape.E => "e",
        Reading.MouthShape.O => "o",
        _ => "pau",
    };
}
