using System;
using System.IO;
using System.Text;

namespace V81TestChn;

internal enum SpeechMessage { Ready = 1, Recognize = 2, Result = 3, Error = 4 }

// Private, versioned stdio protocol. No ports, network requests or audio files.
internal static class SpeechWire
{
    private const int Magic = 0x3154534c; // LST1
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    internal static void Header(BinaryWriter writer, SpeechMessage kind, long id)
    { writer.Write(Magic); writer.Write((int)kind); writer.Write(id); }
    internal static SpeechMessage Header(BinaryReader reader, out long id)
    {
        if (reader.ReadInt32() != Magic) throw new InvalidDataException("Incompatible speech worker.");
        var kind = (SpeechMessage)reader.ReadInt32();
        if (kind < SpeechMessage.Ready || kind > SpeechMessage.Error) throw new InvalidDataException("Invalid speech message.");
        id = reader.ReadInt64();
        return kind;
    }
    internal static void Text(BinaryWriter writer, string text)
    {
        var data = Utf8.GetBytes(text);
        if (data.Length > 32768) throw new InvalidDataException("Speech response exceeds its limit.");
        writer.Write(data.Length); writer.Write(data);
    }
    internal static string Text(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 32768) throw new InvalidDataException("Speech response exceeds its limit.");
        var data = reader.ReadBytes(count);
        if (data.Length != count) throw new EndOfStreamException();
        return Utf8.GetString(data);
    }
    internal static void Samples(BinaryWriter writer, float[] samples, int count)
    {
        if (count < 0 || count > SpeechAudioBuffer.Capacity || count > samples.Length) throw new InvalidDataException("Invalid audio length.");
        writer.Write(SpeechAudioBuffer.SampleRate); writer.Write(count);
        var bytes = new byte[8192];
        for (var offset = 0; offset < count;)
        {
            var block = Math.Min(bytes.Length / sizeof(float), count - offset);
            Buffer.BlockCopy(samples, offset * sizeof(float), bytes, 0, block * sizeof(float));
            writer.Write(bytes, 0, block * sizeof(float)); offset += block;
        }
    }
    internal static float[] Samples(BinaryReader reader, out int rate)
    {
        rate = reader.ReadInt32();
        var count = reader.ReadInt32();
        if (rate != SpeechAudioBuffer.SampleRate || count <= 0 || count > SpeechAudioBuffer.Capacity)
            throw new InvalidDataException("Invalid audio format or length.");
        var samples = new float[count];
        var bytes = new byte[8192];
        for (var offset = 0; offset < count;)
        {
            var block = Math.Min(bytes.Length / sizeof(float), count - offset);
            var wanted = block * sizeof(float);
            for (var read = 0; read < wanted;)
            {
                var n = reader.Read(bytes, read, wanted - read);
                if (n == 0) throw new EndOfStreamException();
                read += n;
            }
            Buffer.BlockCopy(bytes, 0, samples, offset * sizeof(float), wanted); offset += block;
        }
        return samples;
    }
}
