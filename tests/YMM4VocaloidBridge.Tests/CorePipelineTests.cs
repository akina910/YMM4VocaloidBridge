using System.Text;

using YMM4VocaloidBridge.Core;
using YMM4VocaloidBridge.Core.Audio;
using YMM4VocaloidBridge.Core.Caching;
using YMM4VocaloidBridge.Core.LipSync;
using YMM4VocaloidBridge.Core.Reading;
using YMM4VocaloidBridge.Core.Sequence;

namespace YMM4VocaloidBridge.Tests;

public sealed class CorePipelineTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "YMM4VocaloidBridgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Default_voicebank_is_miku_v6_original()
    {
        Assert.Equal("HATSUNE_MIKU_V6_ORIGINAL", new BridgeOptions().VoicebankName);
    }

    [Fact]
    public void Reading_preserves_particles_and_punctuation()
    {
        var result = new JapaneseReadingService().Convert("今日は初音ミクです。ありがとう！");

        Assert.Equal("キョーワハツネミクデスアリガトー", result.Pronunciation);
        Assert.Contains(result.Segments, x => x.Surface == "は" && x.Pronunciation == "ワ");
        Assert.Contains(result.Segments, x => x.Surface == "。" && x.IsPunctuation);
    }

    [Fact]
    public void Mora_tokenizer_combines_small_kana_and_expands_long_vowels()
    {
        var result = new MoraTokenizer().Tokenize("キョー ミク");

        Assert.Equal(["キョ", "オ", "ミ", "ク"], result.Select(x => x.Lyric));
        Assert.Equal([MouthShape.O, MouthShape.O, MouthShape.I, MouthShape.U], result.Select(x => x.MouthShape));
    }

    [Fact]
    public void Sequence_planning_is_deterministic()
    {
        var reading = new JapaneseReadingService().Convert("初音ミクです。");
        var planner = new DialogueSequencePlanner(new MoraTokenizer());
        var options = new BridgeOptions();

        var first = planner.Plan(reading, options);
        var second = planner.Plan(reading, options);

        Assert.Equal(first.TempoBpm, second.TempoBpm);
        Assert.Equal(first.TotalTicks, second.TotalTicks);
        Assert.Equal(first.Notes, second.Notes);
        Assert.Equal(240, first.Notes[0].StartTick);
        Assert.Equal("ハ", first.Notes[0].Lyric);
        Assert.All(first.Notes, note => Assert.InRange(note.NoteNumber, 36, 84));
    }

    [Fact]
    public void Midi_writer_emits_type_one_file_with_utf8_lyrics()
    {
        var reading = new JapaneseReadingService().Convert("きょう");
        var sequence = new DialogueSequencePlanner(new MoraTokenizer()).Plan(reading, new BridgeOptions());
        var path = Path.Combine(temporaryDirectory, "voice.mid");

        new StandardMidiWriter().Write(sequence, path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal("MThd", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(1, (bytes[8] << 8) | bytes[9]);
        Assert.Contains(Encoding.UTF8.GetBytes("キョ"), bytes);
    }

    [Fact]
    public void Lip_sync_starts_and_ends_closed()
    {
        var reading = new JapaneseReadingService().Convert("ミク");
        var sequence = new DialogueSequencePlanner(new MoraTokenizer()).Plan(reading, new BridgeOptions());

        var frames = new LipSyncPlanner().Plan(sequence);

        Assert.Equal(MouthShape.Closed, frames[0].Shape);
        Assert.Contains(frames, frame => frame.Shape == MouthShape.I);
        Assert.Contains(frames, frame => frame.Shape == MouthShape.U);
        Assert.Equal(MouthShape.Closed, frames[^1].Shape);
        Assert.True(frames.SequenceEqual(frames.OrderBy(x => x.Time)));
    }

    [Fact]
    public void Wave_validator_accepts_non_empty_pcm()
    {
        var path = Path.Combine(temporaryDirectory, "audio.wav");
        WritePcmWave(path, sampleRate: 44_100, sampleCount: 4_410);

        var result = new WaveFileValidator().Validate(path);

        Assert.Equal((ushort)1, result.Channels);
        Assert.Equal(44_100, result.SampleRate);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.Duration);
    }

    [Theory]
    [InlineData("output.rje.tmp")]
    [InlineData("output.wav")]
    public async Task Wave_output_renders_separately_then_publishes_atomically(string fileName)
    {
        var requested = Path.Combine(temporaryDirectory, "voice", fileName);
        var work = Path.Combine(temporaryDirectory, "work");
        var output = SynthesisWaveOutput.Create(requested, work);

        Assert.Equal(".wav", Path.GetExtension(output.RenderPath), StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(output.RequestedPath, output.RenderPath);

        WritePcmWave(output.RenderPath, sampleRate: 44_100, sampleCount: 441);
        await output.PublishAsync();

        Assert.Equal(File.ReadAllBytes(output.RenderPath), File.ReadAllBytes(output.RequestedPath));
        _ = new WaveFileValidator().Validate(output.RequestedPath);
    }

    [Fact]
    public async Task Wave_output_cancellation_preserves_existing_requested_file()
    {
        var requested = Path.Combine(temporaryDirectory, "voice", "output.rje.tmp");
        var work = Path.Combine(temporaryDirectory, "work");
        var output = SynthesisWaveOutput.Create(requested, work);
        var existing = Encoding.ASCII.GetBytes("existing-complete-output");
        Directory.CreateDirectory(Path.GetDirectoryName(requested)!);
        await File.WriteAllBytesAsync(requested, existing);
        WritePcmWave(output.RenderPath, sampleRate: 44_100, sampleCount: 441);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => output.PublishAsync(new CancellationToken(canceled: true)));

        Assert.Equal(existing, await File.ReadAllBytesAsync(requested));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(requested)!, "*.tmp-*"));
    }

    [Fact]
    public void Wave_output_avoids_requested_path_that_matches_default_render_name()
    {
        var work = Path.Combine(temporaryDirectory, "work");
        var requested = Path.Combine(work, "render.wav");

        var output = SynthesisWaveOutput.Create(requested, work);

        Assert.Equal(Path.GetFullPath(requested), output.RequestedPath);
        Assert.NotEqual(output.RequestedPath, output.RenderPath);
        Assert.Equal(".wav", Path.GetExtension(output.RenderPath), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wave_output_retries_a_temporarily_locked_destination()
    {
        var requested = Path.Combine(temporaryDirectory, "voice", "output.wav");
        var work = Path.Combine(temporaryDirectory, "work");
        var output = SynthesisWaveOutput.Create(requested, work);
        Directory.CreateDirectory(Path.GetDirectoryName(requested)!);
        await File.WriteAllBytesAsync(requested, Encoding.ASCII.GetBytes("existing-complete-output"));
        WritePcmWave(output.RenderPath, sampleRate: 44_100, sampleCount: 441);
        var expected = await File.ReadAllBytesAsync(output.RenderPath);

        Task publish;
        await using (var locked = File.Open(requested, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            publish = output.PublishAsync();
            await Task.Delay(150);
            Assert.False(publish.IsCompletedSuccessfully);
        }

        await publish;
        Assert.Equal(expected, await File.ReadAllBytesAsync(requested));
    }

    [Fact]
    public async Task Cache_stores_and_restores_atomically()
    {
        var source = Path.Combine(temporaryDirectory, "source.wav");
        var destination = Path.Combine(temporaryDirectory, "restored.wav");
        WritePcmWave(source, sampleRate: 44_100, sampleCount: 441);
        var cache = new SynthesisCache(Path.Combine(temporaryDirectory, "cache"));
        var key = SynthesisCacheKey.Create("ミク", new BridgeOptions(), "6.12.0");

        await cache.StoreAsync(key, source);
        var restored = await cache.TryRestoreAsync(key, destination);

        Assert.True(restored);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task Cache_restore_cancellation_preserves_existing_destination()
    {
        var source = Path.Combine(temporaryDirectory, "source.wav");
        var destination = Path.Combine(temporaryDirectory, "restored.wav");
        WritePcmWave(source, sampleRate: 44_100, sampleCount: 441);
        var existing = Encoding.ASCII.GetBytes("existing-complete-output");
        await File.WriteAllBytesAsync(destination, existing);
        var cache = new SynthesisCache(Path.Combine(temporaryDirectory, "cache"));
        const string key = "atomic-restore";
        await cache.StoreAsync(key, source);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.TryRestoreAsync(key, destination, new CancellationToken(canceled: true)));

        Assert.Equal(existing, await File.ReadAllBytesAsync(destination));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp-*"));
    }

    [Fact]
    public async Task Cache_missing_key_returns_false_without_touching_destination()
    {
        var destination = Path.Combine(temporaryDirectory, "restored.wav");
        var existing = Encoding.ASCII.GetBytes("existing-complete-output");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(destination, existing);
        var cache = new SynthesisCache(Path.Combine(temporaryDirectory, "cache"));

        var restored = await cache.TryRestoreAsync("missing", destination);

        Assert.False(restored);
        Assert.Equal(existing, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Concurrent_cache_stores_publish_one_complete_wave()
    {
        var first = Path.Combine(temporaryDirectory, "first.wav");
        var second = Path.Combine(temporaryDirectory, "second.wav");
        var destination = Path.Combine(temporaryDirectory, "restored.wav");
        WritePcmWave(first, sampleRate: 44_100, sampleCount: 441);
        WritePcmWave(second, sampleRate: 48_000, sampleCount: 882);
        var firstBytes = await File.ReadAllBytesAsync(first);
        var secondBytes = await File.ReadAllBytesAsync(second);
        var cache = new SynthesisCache(Path.Combine(temporaryDirectory, "cache"));
        const string key = "concurrent-store";

        await Task.WhenAll(cache.StoreAsync(key, first), cache.StoreAsync(key, second));
        Assert.True(await cache.TryRestoreAsync(key, destination));

        var restored = await File.ReadAllBytesAsync(destination);
        Assert.True(restored.SequenceEqual(firstBytes) || restored.SequenceEqual(secondBytes));
        _ = new WaveFileValidator().Validate(destination);
    }

    [Fact]
    public async Task Concurrent_cache_restore_and_store_do_not_publish_partial_files()
    {
        var first = Path.Combine(temporaryDirectory, "first.wav");
        var second = Path.Combine(temporaryDirectory, "second.wav");
        var destination = Path.Combine(temporaryDirectory, "restored.wav");
        WritePcmWave(first, sampleRate: 44_100, sampleCount: 44_100);
        WritePcmWave(second, sampleRate: 48_000, sampleCount: 48_000);
        var firstBytes = await File.ReadAllBytesAsync(first);
        var secondBytes = await File.ReadAllBytesAsync(second);
        var cache = new SynthesisCache(Path.Combine(temporaryDirectory, "cache"));
        const string key = "concurrent-restore-store";
        await cache.StoreAsync(key, first);

        var restore = cache.TryRestoreAsync(key, destination);
        var store = cache.StoreAsync(key, second);
        await Task.WhenAll(restore, store);

        Assert.True(await restore);
        var restored = await File.ReadAllBytesAsync(destination);
        Assert.True(restored.SequenceEqual(firstBytes) || restored.SequenceEqual(secondBytes));
        _ = new WaveFileValidator().Validate(destination);
    }

    [Fact]
    public async Task Artifact_builder_writes_midi_and_lab()
    {
        var artifacts = await SynthesisArtifactBuilder.CreateDefault().BuildAsync(
            "今日は初音ミクです。",
            new BridgeOptions(),
            temporaryDirectory);

        Assert.True(File.Exists(artifacts.MidiPath));
        Assert.True(File.Exists(artifacts.LabPath));
        Assert.NotEmpty(artifacts.Sequence.Notes);
        Assert.Contains("pau", await File.ReadAllTextAsync(artifacts.LabPath));
    }

    [Fact]
    public void Thirty_short_dialogues_have_deterministic_notes_and_mouth_shapes()
    {
        string[] dialogues =
        [
            "おはよう。", "こんにちは。", "こんばんは。", "ありがとう。", "どういたしまして。",
            "今日はいい天気ですね。", "初音ミクです。", "一緒に始めましょう。", "少し待ってください。", "準備ができました。",
            "次の場面へ進みます。", "音量を確認します。", "もう一度お願いします。", "それでは行きましょう。", "楽しい時間でした。",
            "質問はありますか。", "答えを考えています。", "大丈夫です。", "気をつけてください。", "また会いましょう。",
            "テストを開始します。", "処理が完了しました。", "ファイルを保存します。", "結果を表示します。", "設定を変更します。",
            "ゆっくり話します。", "元気に話します。", "静かに始めます。", "最後まで確認します。", "ご利用ありがとうございます。",
        ];
        var readingService = new JapaneseReadingService();
        var sequencePlanner = new DialogueSequencePlanner(new MoraTokenizer());
        var lipSyncPlanner = new LipSyncPlanner();

        foreach (var dialogue in dialogues)
        {
            var reading = readingService.Convert(dialogue);
            var first = sequencePlanner.Plan(reading, new BridgeOptions());
            var second = sequencePlanner.Plan(reading, new BridgeOptions());
            var frames = lipSyncPlanner.Plan(first);

            Assert.Equal(first.Notes, second.Notes);
            Assert.Equal(MouthShape.Closed, frames[0].Shape);
            Assert.Equal(MouthShape.Closed, frames[^1].Shape);
            Assert.Contains(frames, frame => frame.Shape != MouthShape.Closed);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void WritePcmWave(string path, int sampleRate, int sampleCount)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        var dataBytes = sampleCount * sizeof(short);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((ushort)sizeof(short));
        writer.Write((ushort)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        for (var index = 0; index < sampleCount; index++)
        {
            writer.Write((short)(Math.Sin(index * 0.05) * 1000));
        }
    }
}
