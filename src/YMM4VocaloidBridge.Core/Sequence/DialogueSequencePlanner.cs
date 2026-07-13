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
    private static readonly int[] PitchContour = [0, 2, 1, 1, 0, 0, -1, -1];

    public SequencePlan Plan(JapaneseReadingResult reading, BridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(reading);
        options.Validate();

        var notes = new List<SequenceNote>();
        var cursor = options.LeadInTicks;
        var phraseIndex = 0;
        var phrase = new List<Mora>();

        foreach (var segment in reading.Segments)
        {
            if (segment.IsPunctuation)
            {
                AppendPhrase(phrase, segment.Surface, phraseIndex, options, notes, ref cursor);
                phrase.Clear();
                cursor += GetPauseTicks(segment.Surface, options.MoraTicks);
                phraseIndex++;
                continue;
            }

            foreach (var mora in moraTokenizer.Tokenize(segment.Pronunciation))
            {
                phrase.Add(mora);
            }
        }

        AppendPhrase(phrase, string.Empty, phraseIndex, options, notes, ref cursor);

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
        int phraseIndex,
        BridgeOptions options,
        ICollection<SequenceNote> notes,
        ref int cursor)
    {
        var isQuestion = terminal.IndexOfAny(['？', '?']) >= 0;
        var isExclamation = terminal.IndexOfAny(['！', '!']) >= 0;
        for (var moraIndex = 0; moraIndex < phrase.Count; moraIndex++)
        {
            var phraseBias = phraseIndex % 2 == 0 ? 0 : -1;
            var declination = phrase.Count <= 1 ? 0 : -(moraIndex * 2 / (phrase.Count - 1));
            var pitchOffset = PitchContour[moraIndex % PitchContour.Length] + declination + phraseBias;
            var endingIndex = moraIndex - Math.Max(0, phrase.Count - 3);
            if (endingIndex >= 0)
            {
                pitchOffset = isQuestion ? phraseBias + endingIndex * 2 : pitchOffset - endingIndex;
            }

            var mora = phrase[moraIndex];
            var slotTicks = mora.Lyric == "ッ" ? Math.Max(30, options.MoraTicks / 2) : options.MoraTicks;
            var durationTicks = Math.Max(15, slotTicks - Math.Min(options.NoteGapTicks, slotTicks - 15));
            var velocity = Math.Clamp(options.Velocity + (isExclamation ? 8 : 0), 1, 127);
            notes.Add(new SequenceNote(
                cursor,
                durationTicks,
                Math.Clamp(options.BaseNote + pitchOffset, 0, 127),
                velocity,
                mora.Lyric,
                mora.MouthShape));
            cursor += options.MoraTicks;
        }
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
