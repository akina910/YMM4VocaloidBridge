using YMM4VocaloidBridge.Core.LipSync;
using YMM4VocaloidBridge.Core.Reading;
using YMM4VocaloidBridge.Core.Sequence;

namespace YMM4VocaloidBridge.Core;

public sealed record SynthesisArtifacts(
    JapaneseReadingResult Reading,
    SequencePlan Sequence,
    IReadOnlyList<LipSyncFrame> LipSyncFrames,
    string MidiPath,
    string LabPath);

public sealed class SynthesisArtifactBuilder(
    JapaneseReadingService readingService,
    ISequencePlanner sequencePlanner,
    StandardMidiWriter midiWriter,
    LipSyncPlanner lipSyncPlanner,
    LabWriter labWriter)
{
    public static SynthesisArtifactBuilder CreateDefault() => new(
        new JapaneseReadingService(),
        new RobotSpeechSequencePlanner(new MoraTokenizer()),
        new StandardMidiWriter(),
        new LipSyncPlanner(),
        new LabWriter());

    public async Task<SynthesisArtifacts> BuildAsync(
        string text,
        BridgeOptions options,
        string workDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);
        Directory.CreateDirectory(workDirectory);

        var reading = readingService.Convert(text);
        var sequence = sequencePlanner.Plan(reading, options);
        var lipSyncFrames = lipSyncPlanner.Plan(sequence);
        var stem = Guid.NewGuid().ToString("N");
        var midiPath = Path.Combine(workDirectory, stem + ".mid");
        var labPath = Path.Combine(workDirectory, stem + ".lab");
        midiWriter.Write(sequence, midiPath);
        await labWriter.WriteAsync(lipSyncFrames, labPath, cancellationToken).ConfigureAwait(false);
        return new SynthesisArtifacts(reading, sequence, lipSyncFrames, midiPath, labPath);
    }
}
