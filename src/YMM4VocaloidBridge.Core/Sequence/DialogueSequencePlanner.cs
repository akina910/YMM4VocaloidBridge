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
    public SequencePlan Plan(JapaneseReadingResult reading, BridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(reading);
        options.Validate();

        var notes = new List<SequenceNote>();
        var cursor = options.LeadInTicks;
        var phrase = new List<Mora>();

        foreach (var segment in reading.Segments)
        {
            if (segment.IsPunctuation)
            {
                AppendPhrase(phrase, segment.Surface, options, notes, ref cursor);
                phrase.Clear();
                cursor += GetPauseTicks(segment.Surface, options.MoraTicks);
                continue;
            }

            foreach (var mora in moraTokenizer.Tokenize(segment.Pronunciation))
            {
                phrase.Add(mora);
            }
        }

        AppendPhrase(phrase, string.Empty, options, notes, ref cursor);

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

    private static void AppendPhrase(
        IReadOnlyList<Mora> phrase,
        string terminal,
        BridgeOptions options,
        ICollection<SequenceNote> notes,
        ref int cursor)
    {
        var isQuestion = terminal.IndexOfAny(['？', '?']) >= 0;
        var isExclamation = terminal.IndexOfAny(['！', '!']) >= 0;
        for (var moraIndex = 0; moraIndex < phrase.Count; moraIndex++)
        {
            var mora = phrase[moraIndex];
            var slotTicks = options.MoraTicks;
            if (mora.Lyric == "ッ")
            {
                cursor += slotTicks;
                continue;
            }

            var soundingTicks = slotTicks;
            var durationTicks = Math.Max(15, soundingTicks - Math.Min(options.NoteGapTicks, soundingTicks - 15));
            var velocity = Math.Clamp(options.Velocity + (isExclamation ? 8 : 0), 1, 127);
            notes.Add(new SequenceNote(
                cursor,
                durationTicks,
                Math.Clamp(options.BaseNote + GetPitchOffset(moraIndex, phrase.Count, isQuestion), 0, 127),
                velocity,
                mora.Lyric,
                mora.MouthShape));
            cursor += slotTicks;
        }
    }

    private static int GetPitchOffset(int index, int count, bool isQuestion)
    {
        return isQuestion && index == count - 1 ? 1 : 0;
    }

    private static int GetPauseTicks(string punctuation, int moraTicks)
    {
        if (punctuation.IndexOfAny(['。', '.', '！', '!', '？', '?']) >= 0)
        {
            return moraTicks * 3 / 2;
        }

        return punctuation.IndexOfAny(['、', ',', '・', ':', ';']) >= 0 ? moraTicks * 3 / 4 : moraTicks / 2;
    }
}
