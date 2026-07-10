namespace YMM4VocaloidBridge.Core.Reading;

public sealed record Mora(string Lyric, MouthShape MouthShape);

public enum MouthShape
{
    Closed,
    A,
    I,
    U,
    E,
    O,
}

public sealed class MoraTokenizer
{
    private const string SmallKana = "ァィゥェォャュョヮヵヶ";

    public IReadOnlyList<Mora> Tokenize(string pronunciation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pronunciation);
        var morae = new List<Mora>();

        foreach (var character in pronunciation)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (SmallKana.Contains(character, StringComparison.Ordinal) && morae.Count > 0)
            {
                var previous = morae[^1];
                var lyric = previous.Lyric + character;
                morae[^1] = new Mora(lyric, GetMouthShape(lyric));
                continue;
            }

            if (character == 'ー' && morae.Count > 0)
            {
                var shape = morae[^1].MouthShape;
                morae.Add(new Mora(LongVowelLyric(shape), shape));
                continue;
            }

            var value = character.ToString();
            morae.Add(new Mora(value, GetMouthShape(value)));
        }

        return morae;
    }

    private static string LongVowelLyric(MouthShape shape) => shape switch
    {
        MouthShape.A => "ア",
        MouthShape.I => "イ",
        MouthShape.U => "ウ",
        MouthShape.E => "エ",
        MouthShape.O => "オ",
        _ => "ン",
    };

    private static MouthShape GetMouthShape(string mora)
    {
        var character = mora[^1];
        if ("ァアカガサザタダナハバパマャヤラヮワ".Contains(character, StringComparison.Ordinal))
        {
            return MouthShape.A;
        }

        if ("ィイキギシジチヂニヒビピミリヰヸ".Contains(character, StringComparison.Ordinal))
        {
            return MouthShape.I;
        }

        if ("ゥウェウクグスズツヅヌフブプムュユルヴ".Contains(character, StringComparison.Ordinal))
        {
            return MouthShape.U;
        }

        if ("ェエケゲセゼテデネヘベペメレヱヹ".Contains(character, StringComparison.Ordinal))
        {
            return MouthShape.E;
        }

        if ("ォオコゴソゾトドノホボポモョヨロヲヺ".Contains(character, StringComparison.Ordinal))
        {
            return MouthShape.O;
        }

        return MouthShape.Closed;
    }
}
