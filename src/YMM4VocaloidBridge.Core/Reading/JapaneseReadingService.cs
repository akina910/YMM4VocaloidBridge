using System.Globalization;
using System.Text;

using Lucene.Net.Analysis.Ja;
using Lucene.Net.Analysis.Ja.TokenAttributes;
using Lucene.Net.Analysis.TokenAttributes;

namespace YMM4VocaloidBridge.Core.Reading;

public sealed record ReadingSegment(string Surface, string Pronunciation, bool IsPunctuation);

public sealed record JapaneseReadingResult(string NormalizedText, IReadOnlyList<ReadingSegment> Segments)
{
    public string Pronunciation => string.Concat(Segments.Where(x => !x.IsPunctuation).Select(x => x.Pronunciation));
}

public sealed class JapaneseReadingService
{
    public JapaneseReadingResult Convert(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Normalize(NormalizationForm.FormKC);
        var segments = new List<ReadingSegment>();

        using var reader = new StringReader(normalized);
        using var tokenizer = new JapaneseTokenizer(reader, null, false, JapaneseTokenizerMode.NORMAL);
        var term = tokenizer.AddAttribute<ICharTermAttribute>();
        var reading = tokenizer.AddAttribute<IReadingAttribute>();
        tokenizer.Reset();

        while (tokenizer.IncrementToken())
        {
            var surface = term.ToString();
            var pronunciation = reading.GetPronunciation() ?? reading.GetReading() ?? surface;
            var punctuation = surface.All(IsPunctuationOrSymbol);
            segments.Add(new ReadingSegment(surface, ToKatakana(pronunciation), punctuation));
        }

        tokenizer.End();
        return new JapaneseReadingResult(normalized, segments);
    }

    private static bool IsPunctuationOrSymbol(char value)
    {
        var category = char.GetUnicodeCategory(value);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.OtherPunctuation
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.MathSymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;
    }

    private static string ToKatakana(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is >= '\u3041' and <= '\u3096' ? (char)(character + 0x60) : character);
        }

        return builder.ToString();
    }
}
