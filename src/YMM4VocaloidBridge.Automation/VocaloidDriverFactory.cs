using YMM4VocaloidBridge.Core;

namespace YMM4VocaloidBridge.Automation;

public static class VocaloidDriverFactory
{
    public static IVocaloidDriver Create(
        VocaloidDriverMode mode,
        IVocaloidDriver automaticDriver,
        IVocaloidDriver assistedDriver,
        bool allowAutomaticFallback = false)
    {
        ArgumentNullException.ThrowIfNull(automaticDriver);
        ArgumentNullException.ThrowIfNull(assistedDriver);

        return mode switch
        {
            VocaloidDriverMode.Automatic when allowAutomaticFallback =>
                new FallbackVocaloidDriver(automaticDriver, assistedDriver),
            VocaloidDriverMode.Automatic => automaticDriver,
            VocaloidDriverMode.Assisted => assistedDriver,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported VOCALOID driver mode."),
        };
    }
}
