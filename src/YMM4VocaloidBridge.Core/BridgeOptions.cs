namespace YMM4VocaloidBridge.Core;

public enum VocaloidDriverMode
{
    Assisted,
    Automatic,
}

public sealed record BridgeOptions
{
    public const int TicksPerQuarterNote = 480;

    public const int DefaultTempoBpm = 130;

    public const string DefaultVoicebankName = "HATSUNE_MIKU_V6_ORIGINAL";

    public const string DefaultVoiceStyleName = "No Effect";

    public const int DefaultVoiceTakeNumber = 10;

    public int TempoBpm { get; init; } = DefaultTempoBpm;

    public int BaseNote { get; init; } = 60;

    public int Velocity { get; init; } = 88;

    public int MoraTicks { get; init; } = 160;

    public int NoteGapTicks { get; init; } = 0;

    public int LeadInTicks { get; init; } = 120;

    public int TailTicks { get; init; } = 240;

    public int TimeoutSeconds { get; init; } = 300;

    public int LipSyncLeadMilliseconds { get; init; } = 33;

    public string VoicebankName { get; init; } = DefaultVoicebankName;

    public string VoiceStyleName { get; init; } = DefaultVoiceStyleName;

    public int VoiceTakeNumber { get; init; } = DefaultVoiceTakeNumber;

    public VocaloidDriverMode DriverMode { get; init; } = VocaloidDriverMode.Assisted;

    public BridgeOptions Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(TempoBpm, 40);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(TempoBpm, 300);
        ArgumentOutOfRangeException.ThrowIfLessThan(BaseNote, 36);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BaseNote, 84);
        ArgumentOutOfRangeException.ThrowIfLessThan(Velocity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Velocity, 127);
        ArgumentOutOfRangeException.ThrowIfLessThan(MoraTicks, 30);
        ArgumentOutOfRangeException.ThrowIfLessThan(NoteGapTicks, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(NoteGapTicks, MoraTicks);
        ArgumentOutOfRangeException.ThrowIfLessThan(LeadInTicks, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(TailTicks, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(TimeoutSeconds, 10);
        ArgumentOutOfRangeException.ThrowIfLessThan(LipSyncLeadMilliseconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LipSyncLeadMilliseconds, 100);
        ArgumentException.ThrowIfNullOrWhiteSpace(VoicebankName);
        ArgumentException.ThrowIfNullOrWhiteSpace(VoiceStyleName);
        ArgumentOutOfRangeException.ThrowIfLessThan(VoiceTakeNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(VoiceTakeNumber, 10);
        return this;
    }
}
