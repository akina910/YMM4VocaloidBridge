using System.Buffers.Binary;
using System.Text;

namespace YMM4VocaloidBridge.Core.Sequence;

public sealed class StandardMidiWriter
{
    public void Write(SequencePlan plan, string path)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var output = File.Create(path);
        WriteAscii(output, "MThd");
        WriteInt32BigEndian(output, 6);
        WriteInt16BigEndian(output, 1);
        WriteInt16BigEndian(output, 2);
        WriteInt16BigEndian(output, checked((short)plan.TicksPerQuarterNote));
        WriteTrack(output, BuildConductorTrack(plan));
        WriteTrack(output, BuildNoteTrack(plan));
    }

    private static byte[] BuildConductorTrack(SequencePlan plan)
    {
        using var track = new MemoryStream();
        WriteVariableLength(track, 0);
        track.Write([0xFF, 0x51, 0x03]);
        var microsecondsPerQuarter = 60_000_000 / plan.TempoBpm;
        track.WriteByte((byte)(microsecondsPerQuarter >> 16));
        track.WriteByte((byte)(microsecondsPerQuarter >> 8));
        track.WriteByte((byte)microsecondsPerQuarter);
        WriteVariableLength(track, 0);
        track.Write([0xFF, 0x58, 0x04, 0x04, 0x02, 0x18, 0x08]);
        WriteEndOfTrack(track);
        return track.ToArray();
    }

    private static byte[] BuildNoteTrack(SequencePlan plan)
    {
        using var track = new MemoryStream();
        WriteMetaText(track, 0, 0x03, "YMM4 Vocaloid Bridge");
        var cursor = 0;

        foreach (var note in plan.Notes)
        {
            WriteMetaText(track, note.StartTick - cursor, 0x05, note.Lyric);
            WriteVariableLength(track, 0);
            track.WriteByte(0x90);
            track.WriteByte((byte)note.NoteNumber);
            track.WriteByte((byte)note.Velocity);
            WriteVariableLength(track, note.DurationTicks);
            track.WriteByte(0x80);
            track.WriteByte((byte)note.NoteNumber);
            track.WriteByte(0);
            cursor = note.StartTick + note.DurationTicks;
        }

        WriteVariableLength(track, Math.Max(0, plan.TotalTicks - cursor));
        track.Write([0xFF, 0x2F, 0x00]);
        return track.ToArray();
    }

    private static void WriteMetaText(Stream stream, int delta, byte type, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVariableLength(stream, delta);
        stream.WriteByte(0xFF);
        stream.WriteByte(type);
        WriteVariableLength(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteTrack(Stream output, byte[] data)
    {
        WriteAscii(output, "MTrk");
        WriteInt32BigEndian(output, data.Length);
        output.Write(data);
    }

    private static void WriteEndOfTrack(Stream stream)
    {
        WriteVariableLength(stream, 0);
        stream.Write([0xFF, 0x2F, 0x00]);
    }

    private static void WriteVariableLength(Stream stream, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var buffer = (uint)(value & 0x7F);
        while ((value >>= 7) > 0)
        {
            buffer <<= 8;
            buffer |= (uint)((value & 0x7F) | 0x80);
        }

        while (true)
        {
            stream.WriteByte((byte)buffer);
            if ((buffer & 0x80) == 0)
            {
                break;
            }

            buffer >>= 8;
        }
    }

    private static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));

    private static void WriteInt16BigEndian(Stream stream, short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
