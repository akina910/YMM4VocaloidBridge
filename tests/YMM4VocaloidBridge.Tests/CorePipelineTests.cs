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
        Assert.Equal(10, new BridgeOptions().VoiceTakeNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BridgeOptions { VoiceTakeNumber = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new BridgeOptions { VoiceTakeNumber = 11 }.Validate());
    }

    [Fact]
    public void Reading_preserves_particles_and_punctuation()
    {
        var result = new JapaneseReadingService().Convert("今日は初音ミクです。ありがとう！");

        Assert.Equal("キョーワハツネミクデス。アリガトー!", result.Pronunciation);
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
        Assert.Equal(120, first.Notes[0].StartTick);
        Assert.Equal("ハ", first.Notes[0].Lyric);
        Assert.All(first.Notes, note => Assert.InRange(note.NoteNumber, 36, 84));
    }

    [Fact]
    public void Reading_round_trip_preserves_dialogue_punctuation_for_YMM4()
    {
        var service = new JapaneseReadingService();
        var first = service.Convert("待って、行きますか？");
        var second = service.Convert(first.Pronunciation);

        Assert.Contains(second.Segments, segment => segment.Surface == "、" && segment.IsPunctuation);
        Assert.Contains(second.Segments, segment => segment.Surface == "?" && segment.IsPunctuation);
        Assert.EndsWith("?", second.Pronunciation, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogue_prosody_raises_questions_and_keeps_sokuon_as_silence()
    {
        var service = new JapaneseReadingService();
        var planner = new DialogueSequencePlanner(new MoraTokenizer());
        var options = new BridgeOptions();
        var question = planner.Plan(service.Convert("行きますか？"), options);
        var statement = planner.Plan(service.Convert("行きます。"), options);
        var sokuon = planner.Plan(service.Convert("待って。"), options);

        Assert.True(question.Notes[^1].NoteNumber > question.Notes[^2].NoteNumber);
        Assert.True(question.Notes[^1].NoteNumber > statement.Notes[^1].NoteNumber);
        Assert.DoesNotContain(sokuon.Notes, note => note.Lyric == "ッ");
        Assert.Equal(2, sokuon.Notes.Count);
        Assert.Equal(
            options.MoraTicks,
            sokuon.Notes[1].StartTick - (sokuon.Notes[0].StartTick + sokuon.Notes[0].DurationTicks));
    }

    [Fact]
    public void Dialogue_defaults_use_connected_notes_and_a_speech_like_rate()
    {
        var reading = new JapaneseReadingService().Convert("これは自然な会話です");
        var sequence = new DialogueSequencePlanner(new MoraTokenizer()).Plan(reading, new BridgeOptions());
        var first = sequence.Notes[0];
        var last = sequence.Notes[^1];
        var spokenSeconds = (last.StartTick + last.DurationTicks - first.StartTick)
            / (double)sequence.TicksPerQuarterNote
            * 60
            / sequence.TempoBpm;
        var moraPerSecond = sequence.Notes.Count / spokenSeconds;

        Assert.InRange(moraPerSecond, 5.5, 8.0);
        Assert.All(
            sequence.Notes.Zip(sequence.Notes.Skip(1)),
            pair =>
            {
                Assert.Equal(pair.First.StartTick + pair.First.DurationTicks, pair.Second.StartTick);
                Assert.InRange(Math.Abs(pair.Second.NoteNumber - pair.First.NoteNumber), 0, 1);
            });
        Assert.InRange(sequence.Notes.Max(note => note.NoteNumber) - sequence.Notes.Min(note => note.NoteNumber), 0, 2);
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

    [Fact]
    public void Wave_audio_analyzer_detects_activity_boundaries()
    {
        var path = Path.Combine(temporaryDirectory, "dialogue.wav");
        WritePcmWaveWithActivity(
            path,
            sampleRate: 44_100,
            duration: TimeSpan.FromSeconds(1),
            activeStart: TimeSpan.FromMilliseconds(200),
            activeEnd: TimeSpan.FromMilliseconds(700));

        var result = new WaveAudioAnalyzer().Analyze(path);

        Assert.InRange(result.ActiveStart.TotalMilliseconds, 170, 210);
        Assert.InRange(result.ActiveEnd.TotalMilliseconds, 690, 730);
        Assert.True(result.PeakAmplitude > 0.01);
        Assert.True(result.RmsAmplitude > 0.001);
    }

    [Fact]
    public void Wave_audio_analyzer_rejects_silent_pcm()
    {
        var path = Path.Combine(temporaryDirectory, "silent.wav");
        WritePcmWaveWithActivity(
            path,
            sampleRate: 44_100,
            duration: TimeSpan.FromSeconds(1),
            activeStart: TimeSpan.Zero,
            activeEnd: TimeSpan.Zero);

        Assert.Throws<InvalidDataException>(() => new WaveAudioAnalyzer().Analyze(path));
    }

    [Fact]
    public void Wave_audio_analyzer_accepts_extensible_pcm()
    {
        var path = Path.Combine(temporaryDirectory, "extensible.wav");
        WriteExtensiblePcmWaveWithActivity(
            path,
            sampleRate: 44_100,
            duration: TimeSpan.FromMilliseconds(500),
            activeStart: TimeSpan.FromMilliseconds(100),
            activeEnd: TimeSpan.FromMilliseconds(400));

        var result = new WaveAudioAnalyzer().Analyze(path);

        Assert.Equal((ushort)1, result.Format.AudioFormat);
        Assert.InRange(result.ActiveStart.TotalMilliseconds, 90, 110);
        Assert.InRange(result.ActiveEnd.TotalMilliseconds, 390, 410);
    }

    [Fact]
    public void Lip_sync_timeline_aligns_to_rendered_audio_and_applies_lead()
    {
        var path = Path.Combine(temporaryDirectory, "aligned.wav");
        WritePcmWaveWithActivity(
            path,
            sampleRate: 44_100,
            duration: TimeSpan.FromSeconds(1),
            activeStart: TimeSpan.FromMilliseconds(200),
            activeEnd: TimeSpan.FromMilliseconds(700));
        var activity = new WaveAudioAnalyzer().Analyze(path);
        LipSyncFrame[] planned =
        [
            new(TimeSpan.Zero, MouthShape.Closed),
            new(TimeSpan.FromMilliseconds(250), MouthShape.A),
            new(TimeSpan.FromMilliseconds(500), MouthShape.I),
            new(TimeSpan.FromMilliseconds(750), MouthShape.U),
            new(TimeSpan.FromMilliseconds(1_000), MouthShape.Closed),
            new(TimeSpan.FromMilliseconds(1_500), MouthShape.Closed),
        ];

        var aligned = new LipSyncTimelineAligner().Align(planned, activity, TimeSpan.FromMilliseconds(33));

        var firstOpen = aligned.First(frame => frame.Shape != MouthShape.Closed);
        var closing = aligned.First(frame => frame.Time > firstOpen.Time && frame.Shape == MouthShape.Closed);
        // Detected speech start/end minus the configured visual lead: about 167 ms and 667 ms.
        Assert.InRange(firstOpen.Time.TotalMilliseconds, 135, 180);
        Assert.InRange(closing.Time.TotalMilliseconds, 655, 700);
        Assert.Equal(TimeSpan.FromSeconds(1), aligned[^1].Time);
        Assert.Equal(MouthShape.Closed, aligned[^1].Shape);
    }

    [Fact]
    public void Lip_sync_timeline_preserves_transitions_when_audio_interval_is_shorter()
    {
        var format = new WaveFileInfo(1, 1, 44_100, 16, 88_200, TimeSpan.FromSeconds(1));
        var activity = new WaveAudioActivity(
            format,
            0.5,
            0.1,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(5));
        LipSyncFrame[] planned =
        [
            new(TimeSpan.Zero, MouthShape.Closed),
            new(TimeSpan.FromMilliseconds(100), MouthShape.A),
            new(TimeSpan.FromMilliseconds(200), MouthShape.I),
            new(TimeSpan.FromMilliseconds(300), MouthShape.U),
            new(TimeSpan.FromMilliseconds(400), MouthShape.E),
            new(TimeSpan.FromMilliseconds(500), MouthShape.Closed),
        ];

        var aligned = new LipSyncTimelineAligner().Align(planned, activity, TimeSpan.Zero);

        Assert.Equal(
            [MouthShape.A, MouthShape.I, MouthShape.U, MouthShape.E],
            aligned.Where(frame => frame.Shape != MouthShape.Closed).Select(frame => frame.Shape));
        Assert.Equal(4, aligned.Where(frame => frame.Shape != MouthShape.Closed).Select(frame => frame.Time).Distinct().Count());
    }

    [Fact]
    public void Lip_sync_timeline_accepts_a_single_open_frame()
    {
        var format = new WaveFileInfo(1, 1, 44_100, 16, 44_100, TimeSpan.FromMilliseconds(500));
        var activity = new WaveAudioActivity(
            format,
            0.5,
            0.1,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(300));
        LipSyncFrame[] planned =
        [
            new(TimeSpan.Zero, MouthShape.Closed),
            new(TimeSpan.FromMilliseconds(100), MouthShape.A),
        ];

        var aligned = new LipSyncTimelineAligner().Align(planned, activity, TimeSpan.Zero);

        Assert.Contains(aligned, frame => frame.Shape == MouthShape.A);
        Assert.Equal(MouthShape.Closed, aligned[^1].Shape);
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

    private static void WritePcmWaveWithActivity(
        string path,
        int sampleRate,
        TimeSpan duration,
        TimeSpan activeStart,
        TimeSpan activeEnd)
    {
        var sampleCount = (int)(sampleRate * duration.TotalSeconds);
        var activeStartSample = (int)(sampleRate * activeStart.TotalSeconds);
        var activeEndSample = (int)(sampleRate * activeEnd.TotalSeconds);
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
            var active = index >= activeStartSample && index < activeEndSample;
            writer.Write(active ? (short)(Math.Sin(index * 0.05) * 1000) : (short)0);
        }
    }

    private static void WriteExtensiblePcmWaveWithActivity(
        string path,
        int sampleRate,
        TimeSpan duration,
        TimeSpan activeStart,
        TimeSpan activeEnd)
    {
        var sampleCount = (int)(sampleRate * duration.TotalSeconds);
        var activeStartSample = (int)(sampleRate * activeStart.TotalSeconds);
        var activeEndSample = (int)(sampleRate * activeEnd.TotalSeconds);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        var dataBytes = sampleCount * sizeof(short);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(60 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(40);
        writer.Write((ushort)0xFFFE);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((ushort)sizeof(short));
        writer.Write((ushort)16);
        writer.Write((ushort)22);
        writer.Write((ushort)16);
        writer.Write(4u);
        writer.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray());
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        for (var index = 0; index < sampleCount; index++)
        {
            var active = index >= activeStartSample && index < activeEndSample;
            writer.Write(active ? (short)(Math.Sin(index * 0.05) * 1000) : (short)0);
        }
    }
}
