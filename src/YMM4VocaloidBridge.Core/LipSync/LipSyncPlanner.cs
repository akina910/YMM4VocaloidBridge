using YMM4VocaloidBridge.Core.Reading;
using YMM4VocaloidBridge.Core.Sequence;

namespace YMM4VocaloidBridge.Core.LipSync;

public sealed record LipSyncFrame(TimeSpan Time, MouthShape Shape);

public sealed class LipSyncPlanner
{
    public IReadOnlyList<LipSyncFrame> Plan(SequencePlan sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        var frames = new List<LipSyncFrame> { new(TimeSpan.Zero, MouthShape.Closed) };

        for (var index = 0; index < sequence.Notes.Count; index++)
        {
            var note = sequence.Notes[index];
            frames.Add(new LipSyncFrame(ToTimeSpan(note.StartTick, sequence), note.MouthShape));

            var nextStart = index + 1 < sequence.Notes.Count
                ? sequence.Notes[index + 1].StartTick
                : sequence.TotalTicks;
            if (nextStart - (note.StartTick + note.DurationTicks) >= 30 || index == sequence.Notes.Count - 1)
            {
                frames.Add(new LipSyncFrame(ToTimeSpan(note.StartTick + note.DurationTicks, sequence), MouthShape.Closed));
            }
        }

        var end = ToTimeSpan(sequence.TotalTicks, sequence);
        if (frames[^1].Time != end || frames[^1].Shape != MouthShape.Closed)
        {
            frames.Add(new LipSyncFrame(end, MouthShape.Closed));
        }

        return frames
            .GroupBy(x => x.Time)
            .Select(x => x.Last())
            .OrderBy(x => x.Time)
            .ToArray();
    }

    private static TimeSpan ToTimeSpan(int ticks, SequencePlan sequence)
    {
        var seconds = ticks * 60d / (sequence.TempoBpm * sequence.TicksPerQuarterNote);
        return TimeSpan.FromSeconds(seconds);
    }
}
