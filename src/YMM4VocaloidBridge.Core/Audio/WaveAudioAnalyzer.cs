using System.Buffers.Binary;
using System.Text;

namespace YMM4VocaloidBridge.Core.Audio;

public sealed record WaveAudioActivity(
    WaveFileInfo Format,
    double PeakAmplitude,
    double RmsAmplitude,
    TimeSpan ActiveStart,
    TimeSpan ActiveEnd);

public sealed class WaveAudioAnalyzer
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");
    private const double MinimumPeakAmplitude = 0.001;
    private const double MinimumRmsAmplitude = 0.00005;
    private const double AbsoluteActivityThreshold = 0.001;
    private const double RelativeActivityThreshold = 0.015;

    public WaveAudioActivity Analyze(string path)
    {
        var format = new WaveFileValidator().Validate(path);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var layout = ReadLayout(stream);
        ValidateSupportedLayout(layout);

        stream.Position = layout.DataOffset;
        var bytesPerSample = layout.BitsPerSample / 8;
        var blockAlign = checked(layout.Channels * bytesPerSample);
        var windowFrames = Math.Max(1, layout.SampleRate / 100);
        var buffer = new byte[checked(windowFrames * blockAlign)];
        var windowRms = new List<double>();
        var peak = 0d;
        var sumSquares = 0d;
        long sampleCount = 0;
        long remaining = layout.DataBytes;

        while (remaining > 0)
        {
            var requested = (int)Math.Min(buffer.Length, remaining);
            stream.ReadExactly(buffer.AsSpan(0, requested));
            remaining -= requested;
            var completeBytes = requested - (requested % blockAlign);
            var windowSquares = 0d;
            long windowSamples = 0;
            for (var frameOffset = 0; frameOffset < completeBytes; frameOffset += blockAlign)
            {
                for (var channel = 0; channel < layout.Channels; channel++)
                {
                    var sampleOffset = frameOffset + channel * bytesPerSample;
                    var sample = Math.Abs(ReadSample(buffer.AsSpan(sampleOffset, bytesPerSample), layout));
                    peak = Math.Max(peak, sample);
                    var square = sample * sample;
                    sumSquares += square;
                    windowSquares += square;
                    sampleCount++;
                    windowSamples++;
                }
            }

            if (windowSamples > 0)
            {
                windowRms.Add(Math.Sqrt(windowSquares / windowSamples));
            }
        }

        var rms = sampleCount == 0 ? 0 : Math.Sqrt(sumSquares / sampleCount);
        if (peak < MinimumPeakAmplitude || rms < MinimumRmsAmplitude)
        {
            throw new InvalidDataException(
                $"The WAVE file is silent or below the supported signal floor (peak={peak:0.000000}, rms={rms:0.000000}).");
        }

        var threshold = Math.Max(AbsoluteActivityThreshold, peak * RelativeActivityThreshold);
        var firstActive = windowRms.FindIndex(value => value >= threshold);
        var lastActive = windowRms.FindLastIndex(value => value >= threshold);
        if (firstActive < 0 || lastActive < firstActive)
        {
            throw new InvalidDataException("The WAVE file contains no usable speech activity.");
        }

        var windowDuration = TimeSpan.FromSeconds(windowFrames / (double)layout.SampleRate);
        var activeStart = Max(TimeSpan.Zero, firstActive * windowDuration);
        var activeEnd = Min(format.Duration, (lastActive + 1) * windowDuration);
        if (activeEnd - activeStart < TimeSpan.FromMilliseconds(30))
        {
            throw new InvalidDataException("The WAVE file contains no sufficiently long speech activity.");
        }

        return new WaveAudioActivity(format, peak, rms, activeStart, activeEnd);
    }

    private static WaveLayout ReadLayout(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (ReadFourCc(reader) != "RIFF")
        {
            throw new InvalidDataException("The output file is not a RIFF container.");
        }

        _ = reader.ReadUInt32();
        if (ReadFourCc(reader) != "WAVE")
        {
            throw new InvalidDataException("The output file is not a WAVE file.");
        }

        ushort audioFormat = 0;
        ushort channels = 0;
        int sampleRate = 0;
        ushort bitsPerSample = 0;
        long dataOffset = 0;
        long dataBytes = 0;
        Span<byte> format = stackalloc byte[40];
        format.Clear();

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = ReadFourCc(reader);
            var chunkLength = reader.ReadUInt32();
            var chunkStart = stream.Position;
            var nextChunk = checked(chunkStart + chunkLength + (chunkLength & 1));
            if (nextChunk > stream.Length)
            {
                throw new InvalidDataException($"WAVE chunk '{chunkId}' is truncated.");
            }

            if (chunkId == "fmt ")
            {
                if (chunkLength < 16)
                {
                    throw new InvalidDataException("The WAVE format chunk is too short.");
                }

                var formatBytes = checked((int)Math.Min(chunkLength, (uint)format.Length));
                stream.ReadExactly(format[..formatBytes]);
                audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(format);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(format[4..]);
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
                audioFormat = ResolveAudioFormat(audioFormat, format, chunkLength);
            }
            else if (chunkId == "data" && dataBytes == 0)
            {
                dataOffset = chunkStart;
                dataBytes = chunkLength;
            }

            stream.Position = nextChunk;
        }

        return new WaveLayout(audioFormat, channels, sampleRate, bitsPerSample, dataOffset, dataBytes);
    }

    private static void ValidateSupportedLayout(WaveLayout layout)
    {
        var supported = layout.AudioFormat switch
        {
            1 => layout.BitsPerSample is 8 or 16 or 24 or 32,
            3 => layout.BitsPerSample is 32 or 64,
            _ => false,
        };
        if (!supported || layout.Channels == 0 || layout.SampleRate <= 0 || layout.DataBytes == 0)
        {
            throw new InvalidDataException(
                $"Speech activity analysis does not support WAVE format {layout.AudioFormat} with {layout.BitsPerSample}-bit samples.");
        }
    }

    private static ushort ResolveAudioFormat(ushort audioFormat, ReadOnlySpan<byte> format, uint chunkLength)
    {
        if (audioFormat != 0xFFFE)
        {
            return audioFormat;
        }

        if (chunkLength < 40 || BinaryPrimitives.ReadUInt16LittleEndian(format[16..]) < 22)
        {
            throw new InvalidDataException("The WAVE_FORMAT_EXTENSIBLE chunk is incomplete.");
        }

        var subFormat = new Guid(format[24..40]);
        if (subFormat == PcmSubFormat)
        {
            return 1;
        }

        return subFormat == FloatSubFormat ? (ushort)3 : (ushort)0xFFFE;
    }

    private static double ReadSample(ReadOnlySpan<byte> bytes, WaveLayout layout)
    {
        if (layout.AudioFormat == 3)
        {
            return layout.BitsPerSample == 32
                ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes))
                : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes));
        }

        return layout.BitsPerSample switch
        {
            8 => (bytes[0] - 128) / 128d,
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768d,
            24 => ReadInt24LittleEndian(bytes) / 8388608d,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648d,
            _ => throw new InvalidDataException("Unsupported PCM sample width."),
        };
    }

    private static int ReadInt24LittleEndian(ReadOnlySpan<byte> bytes)
    {
        var value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        return (value & 0x800000) == 0 ? value : value | unchecked((int)0xFF000000);
    }

    private static string ReadFourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private sealed record WaveLayout(
        ushort AudioFormat,
        ushort Channels,
        int SampleRate,
        ushort BitsPerSample,
        long DataOffset,
        long DataBytes);
}
