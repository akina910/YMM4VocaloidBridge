using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

using CoreFrame = YMM4VocaloidBridge.Core.LipSync.LipSyncFrame;
using CoreMouthShape = YMM4VocaloidBridge.Core.Reading.MouthShape;

namespace YMM4VocaloidBridge.Plugin;

public sealed class MikuV6Pronounce : UndoRedoable, IVoicePronounce
{
    public MikuV6Pronounce(IEnumerable<CoreFrame> frames)
    {
        LipSyncFrames = frames
            .Select(frame => new LipSyncFrame(frame.Time, Map(frame.Shape)))
            .ToArray();
    }

    private MikuV6Pronounce(LipSyncFrame[] frames)
    {
        LipSyncFrames = frames.ToArray();
    }

    public LipSyncFrame[] LipSyncFrames { get; }

    public IVoicePronounce Clone() => new MikuV6Pronounce(LipSyncFrames);

    public void BeginEdit()
    {
    }

    public ValueTask EndEditAsync() => ValueTask.CompletedTask;

    private static MouthShape Map(CoreMouthShape shape) => shape switch
    {
        CoreMouthShape.A => MouthShape.A,
        CoreMouthShape.I => MouthShape.I,
        CoreMouthShape.U => MouthShape.U,
        CoreMouthShape.E => MouthShape.E,
        CoreMouthShape.O => MouthShape.O,
        _ => MouthShape.Silent,
    };
}
