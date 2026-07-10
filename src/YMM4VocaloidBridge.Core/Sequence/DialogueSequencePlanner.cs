using YMM4VocaloidBridge.Core.Reading;

namespace YMM4VocaloidBridge.Core.Sequence;

public sealed record SequenceNote(
    int StartTick,
    int DurationTicks,
    int NoteNumber,
    int Velocity,
    string Lyric,
    MouthShape MouthShape);

public sealed record SequencePlan(
    int TempoBpm,
    int TicksPerQuarterNote,
    int TotalTicks,
    IReadOnlyList<SequenceNote> Notes);

public sealed class DialogueSequencePlanner(MoraTokenizer moraTokenizer)
{
    private static readonly int[] PitchContour = [0, 2, 1, 0, -1, 0, 1, 0];

    public SequencePlan Plan(JapaneseReadingResult reading, BridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(reading);
        options.Validate();

        var notes = new List<SequenceNote>();
        var cursor = options.LeadInTicks;
        var phraseIndex = 0;
        var moraIndex = 0;

        foreach (var segment in reading.Segments)
        {
            if (segment.IsPunctuation)
            {
                cursor += GetPauseTicks(segment.Surface, options.MoraTicks);
                phraseIndex++;
                moraIndex = 0;
                continue;
            }

            foreach (var mora in moraTokenizer.Tokenize(segment.Pronunciation))
            {
                var phraseBias = phraseIndex % 2 == 0 ? 0 : -1;
                var pitch = Math.Clamp(options.BaseNote + PitchContour[moraIndex % PitchContour.Length] + phraseBias, 0, 127);
                notes.Add(new SequenceNote(
                    cursor,
                    options.MoraTicks - options.NoteGapTicks,
                    pitch,
                    options.Velocity,
                    mora.Lyric,
                    mora.MouthShape));
                cursor += options.MoraTicks;
                moraIndex++;
            }
        }

        if (notes.Count == 0)
        {
            throw new InvalidOperationException("No pronounceable Japanese mora was produced from the input.");
        }

        return new SequencePlan(
            options.TempoBpm,
            BridgeOptions.TicksPerQuarterNote,
            cursor + options.TailTicks,
            notes);
    }

    private static int GetPauseTicks(string punctuation, int moraTicks)
    {
        if (punctuation.IndexOfAny(['。', '.', '！', '!', '？', '?']) >= 0)
        {
            return moraTicks * 2;
        }

        return punctuation.IndexOfAny(['、', ',', '・', ':', ';']) >= 0 ? moraTicks : moraTicks / 2;
    }
}
