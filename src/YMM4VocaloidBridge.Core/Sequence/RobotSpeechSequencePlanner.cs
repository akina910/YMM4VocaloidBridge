using YMM4VocaloidBridge.Core.Reading;

namespace YMM4VocaloidBridge.Core.Sequence;

public sealed class RobotSpeechSequencePlanner(MoraTokenizer moraTokenizer) : ISequencePlanner
{
    public SequencePlan Plan(JapaneseReadingResult reading, BridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(reading);
        options.Validate();

        var notes = new List<SequenceNote>();
        var cursor = ScaleTicks(options.LeadInTicks, options.SpeechRatePercent);
        var previousWasWord = false;

        foreach (var segment in reading.Segments)
        {
            if (segment.IsPunctuation)
            {
                cursor += GetPauseTicks(segment.Surface, options);
                previousWasWord = false;
                continue;
            }

            if (previousWasWord)
            {
                cursor += ScaleTicks(options.WordBoundaryTicks, options.SpeechRatePercent);
            }

            foreach (var mora in moraTokenizer.Tokenize(segment.Pronunciation))
            {
                if (mora.Lyric == "ッ")
                {
                    cursor += ScaleTicks(options.SokuonTicks, options.SpeechRatePercent);
                    continue;
                }

                var slotTicks = ScaleTicks(
                    options.MoraTicks * GetLengthPercent(mora.Lyric) / 100,
                    options.SpeechRatePercent);
                var gapTicks = Math.Min(
                    ScaleTicks(options.NoteGapTicks, options.SpeechRatePercent),
                    Math.Max(0, slotTicks - 15));
                notes.Add(new SequenceNote(
                    cursor,
                    slotTicks - gapTicks,
                    options.BaseNote,
                    options.Velocity,
                    mora.Lyric,
                    mora.MouthShape));
                cursor += slotTicks;
            }

            previousWasWord = true;
        }

        if (notes.Count == 0)
        {
            throw new InvalidOperationException("No pronounceable Japanese mora was produced from the input.");
        }

        return new SequencePlan(
            options.TempoBpm,
            BridgeOptions.TicksPerQuarterNote,
            cursor + ScaleTicks(options.TailTicks, options.SpeechRatePercent),
            notes);
    }

    private static int GetLengthPercent(string lyric)
    {
        if (lyric == "ン")
        {
            return 115;
        }

        return lyric == "ツ" ? 110 : 100;
    }

    private static int GetPauseTicks(string punctuation, BridgeOptions options)
    {
        var ticks = punctuation.IndexOfAny(['。', '.', '！', '!', '？', '?']) >= 0
            ? options.SentencePauseTicks
            : options.ShortPauseTicks;
        return ScaleTicks(ticks, options.SpeechRatePercent);
    }

    private static int ScaleTicks(int ticks, int speechRatePercent) =>
        Math.Max(1, (int)Math.Round(ticks * 100d / speechRatePercent, MidpointRounding.AwayFromZero));
}
