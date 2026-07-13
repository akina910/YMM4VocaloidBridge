using YMM4VocaloidBridge.Core.Audio;
using YMM4VocaloidBridge.Core.Reading;

namespace YMM4VocaloidBridge.Core.LipSync;

public sealed class LipSyncTimelineAligner
{
    public IReadOnlyList<LipSyncFrame> Align(
        IReadOnlyList<LipSyncFrame> plannedFrames,
        WaveAudioActivity activity,
        TimeSpan leadTime)
    {
        ArgumentNullException.ThrowIfNull(plannedFrames);
        ArgumentNullException.ThrowIfNull(activity);
        if (leadTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leadTime));
        }

        var firstOpenIndex = FindFirstOpenFrame(plannedFrames);
        var lastOpenIndex = FindLastOpenFrame(plannedFrames);
        if (firstOpenIndex < 0 || lastOpenIndex < firstOpenIndex)
        {
            throw new ArgumentException("At least one open-mouth frame is required.", nameof(plannedFrames));
        }

        var audioEnd = activity.Format.Duration;
        if (audioEnd <= TimeSpan.FromMilliseconds(1))
        {
            throw new ArgumentException("The rendered audio is too short for lip sync.", nameof(activity));
        }

        var plannedStart = plannedFrames[firstOpenIndex].Time;
        var plannedOpenDuration = plannedFrames[lastOpenIndex].Time - plannedStart;
        var firstOpenTime = Min(
            Max(TimeSpan.FromMilliseconds(1), activity.ActiveStart - leadTime),
            audioEnd - TimeSpan.FromMilliseconds(1));
        var closingTime = Min(
            audioEnd,
            Max(firstOpenTime + TimeSpan.FromMilliseconds(1), activity.ActiveEnd - leadTime));
        var availableOpenDuration = closingTime - firstOpenTime - TimeSpan.FromTicks(1);
        var scale = plannedOpenDuration > availableOpenDuration && plannedOpenDuration > TimeSpan.Zero
            ? availableOpenDuration.Ticks / (double)plannedOpenDuration.Ticks
            : 1d;
        var aligned = new List<LipSyncFrame> { new(TimeSpan.Zero, MouthShape.Closed) };
        for (var index = firstOpenIndex; index <= lastOpenIndex; index++)
        {
            var frame = plannedFrames[index];
            var relativeTicks = frame.Time.Ticks - plannedStart.Ticks;
            var candidateTicks = firstOpenTime.Ticks + (long)(relativeTicks * scale);
            var minimumTicks = aligned[^1].Time.Ticks + 1;
            var remainingOpenFrames = lastOpenIndex - index;
            var maximumTicks = closingTime.Ticks - remainingOpenFrames - 1;
            var mappedTicks = Math.Clamp(candidateTicks, minimumTicks, maximumTicks);
            aligned.Add(new LipSyncFrame(TimeSpan.FromTicks(mappedTicks), frame.Shape));
        }

        aligned.Add(new LipSyncFrame(closingTime, MouthShape.Closed));

        if (aligned[^1].Time < audioEnd)
        {
            aligned.Add(new LipSyncFrame(audioEnd, MouthShape.Closed));
        }

        return aligned
            .OrderBy(frame => frame.Time)
            .GroupBy(frame => frame.Time)
            .Select(group => group.Last())
            .ToArray();
    }

    private static int FindFirstOpenFrame(IReadOnlyList<LipSyncFrame> frames)
    {
        for (var index = 0; index < frames.Count; index++)
        {
            if (frames[index].Shape != MouthShape.Closed)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindLastOpenFrame(IReadOnlyList<LipSyncFrame> frames)
    {
        for (var index = frames.Count - 1; index >= 0; index--)
        {
            if (frames[index].Shape != MouthShape.Closed)
            {
                return index;
            }
        }

        return -1;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

}
