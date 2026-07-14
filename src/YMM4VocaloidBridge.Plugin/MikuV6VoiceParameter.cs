using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

using YMM4VocaloidBridge.Core;

namespace YMM4VocaloidBridge.Plugin;

public sealed class MikuV6VoiceParameter : VoiceParameterBase
{
    private VocaloidDriverMode driverMode = VocaloidDriverMode.Automatic;
    private int tempoBpm = BridgeOptions.DefaultTempoBpm;
    private int baseNote = BridgeOptions.DefaultBaseNote;
    private int speechRatePercent = BridgeOptions.DefaultSpeechRatePercent;
    private int voiceTakeNumber = BridgeOptions.DefaultVoiceTakeNumber;
    private int lipSyncLeadMilliseconds = 33;
    private int timeoutSeconds = 300;

    [Display(Name = "連携モード", Description = "自動操作、またはMIDI読み込みとWAV書き出しを手動で行う補助モード")]
    [EnumComboBox]
    [DefaultValue(VocaloidDriverMode.Automatic)]
    public VocaloidDriverMode DriverMode
    {
        get => driverMode;
        set => Set(ref driverMode, value);
    }

    [Display(Name = "テンポ", Description = "会話シーケンスのテンポ")]
    [TextBoxSlider("F0", " BPM", 60, 240, Delay = -1)]
    [Range(60, 240)]
    [DefaultValue(BridgeOptions.DefaultTempoBpm)]
    public int TempoBpm
    {
        get => tempoBpm;
        set => Set(ref tempoBpm, value);
    }

    [Display(Name = "基準音程", Description = "中央ドを60とするMIDIノート番号")]
    [TextBoxSlider("F0", "", 48, 72, Delay = -1)]
    [Range(48, 72)]
    [DefaultValue(BridgeOptions.DefaultBaseNote)]
    public int BaseNote
    {
        get => baseNote;
        set => Set(ref baseNote, value);
    }

    [Display(Name = "話速", Description = "100%を基準にしたロボ声の速さ")]
    [TextBoxSlider("F0", "%", 50, 200, Delay = -1)]
    [Range(50, 200)]
    [DefaultValue(BridgeOptions.DefaultSpeechRatePercent)]
    public int SpeechRatePercent
    {
        get => speechRatePercent;
        set => Set(ref speechRatePercent, value);
    }

    [Display(Name = "テイク", Description = "VOCALOID:AIの発音タイミング候補")]
    [TextBoxSlider("F0", "", 1, 10, Delay = -1)]
    [Range(1, 10)]
    [DefaultValue(BridgeOptions.DefaultVoiceTakeNumber)]
    public int VoiceTakeNumber
    {
        get => voiceTakeNumber;
        set => Set(ref voiceTakeNumber, value);
    }

    [Display(Name = "口パク先行", Description = "口形の切り替えを音声より先行させるミリ秒")]
    [TextBoxSlider("F0", " ms", 0, 100, Delay = -1)]
    [Range(0, 100)]
    [DefaultValue(33)]
    public int LipSyncLeadMilliseconds
    {
        get => lipSyncLeadMilliseconds;
        set => Set(ref lipSyncLeadMilliseconds, value);
    }

    [Display(Name = "待機時間", Description = "WAV書き出しを待つ最大秒数")]
    [TextBoxSlider("F0", " 秒", 30, 900, Delay = -1)]
    [Range(30, 900)]
    [DefaultValue(300)]
    public int TimeoutSeconds
    {
        get => timeoutSeconds;
        set => Set(ref timeoutSeconds, value);
    }

    public BridgeOptions ToOptions() => new()
    {
        DriverMode = DriverMode,
        TempoBpm = TempoBpm,
        BaseNote = BaseNote,
        SpeechRatePercent = SpeechRatePercent,
        VoiceTakeNumber = VoiceTakeNumber,
        LipSyncLeadMilliseconds = LipSyncLeadMilliseconds,
        TimeoutSeconds = TimeoutSeconds,
    };
}
