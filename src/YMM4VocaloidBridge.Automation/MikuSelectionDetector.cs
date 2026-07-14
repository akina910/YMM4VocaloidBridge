using System.Text.Json;

namespace YMM4VocaloidBridge.Automation;

public static class MikuSelectionDetector
{
    public const string VoiceApi = "YMM4VocaloidBridge.Vocaloid6";
    public const string VoiceId = "HATSUNE_MIKU_V6";

    public static bool IsSelected(string characterSettingsJson)
    {
        using var document = JsonDocument.Parse(characterSettingsJson);
        if (!document.RootElement.TryGetProperty("CurrentCharacter", out var character)
            || character.ValueKind != JsonValueKind.Object
            || !character.TryGetProperty("Voice", out var voice)
            || voice.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return voice.TryGetProperty("API", out var api)
            && voice.TryGetProperty("Arg", out var id)
            && string.Equals(api.GetString(), VoiceApi, StringComparison.Ordinal)
            && string.Equals(id.GetString(), VoiceId, StringComparison.Ordinal);
    }
}
