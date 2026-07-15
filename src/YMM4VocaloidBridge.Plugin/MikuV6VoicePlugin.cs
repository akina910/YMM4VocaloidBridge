using YukkuriMovieMaker.Plugin.Voice;

namespace YMM4VocaloidBridge.Plugin;

public sealed class MikuV6VoicePlugin : IVoicePlugin
{
    public MikuV6VoicePlugin()
    {
        MikuSelectionAutoStarter.EnsureMonitoring();
    }

    public string Name => "YMM4 VOCALOID Bridge";

    public IEnumerable<IVoiceSpeaker> Voices => [new MikuV6Speaker()];

    public bool CanUpdateVoices => false;

    public bool IsVoicesCached => true;

    public Task UpdateVoicesAsync() => Task.CompletedTask;
}
