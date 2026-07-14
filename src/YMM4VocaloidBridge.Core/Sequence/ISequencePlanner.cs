using YMM4VocaloidBridge.Core.Reading;

namespace YMM4VocaloidBridge.Core.Sequence;

public interface ISequencePlanner
{
    SequencePlan Plan(JapaneseReadingResult reading, BridgeOptions options);
}
