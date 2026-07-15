using System.Buffers.Binary;
using System.Text;

namespace YMM4VocaloidBridge.Core.Audio;

public sealed record WaveFileInfo(
    ushort AudioFormat,
    ushort Channels,
    int SampleRate,
    ushort BitsPerSample,
    long DataBytes,
    TimeSpan Duration);

public sealed class WaveFileValidator
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    public WaveFileInfo Validate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        long dataBytes = 0;
        Span<byte> format = stackalloc byte[40];
        format.Clear();

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = ReadFourCc(reader);
            var chunkLength = reader.ReadUInt32();
            var nextChunk = checked(stream.Position + chunkLength + (chunkLength & 1));
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
                dataBytes = chunkLength;
            }

            stream.Position = nextChunk;
        }

        if (audioFormat is not (1 or 3) || channels == 0 || sampleRate <= 0 || bitsPerSample == 0 || dataBytes == 0)
        {
            throw new InvalidDataException("The WAVE file has no supported non-empty PCM audio stream.");
        }

        var bytesPerSampleFrame = channels * Math.Max(1, bitsPerSample / 8);
        var duration = TimeSpan.FromSeconds(dataBytes / (double)(sampleRate * bytesPerSampleFrame));
        return new WaveFileInfo(audioFormat, channels, sampleRate, bitsPerSample, dataBytes, duration);
    }

    private static string ReadFourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));

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
}
