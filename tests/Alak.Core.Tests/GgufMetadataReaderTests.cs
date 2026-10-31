using System.Text;
using Alak.Core.Models;

namespace Alak.Core.Tests;

public class GgufMetadataReaderTests
{
    [Fact]
    public void ReadsHeaderMetadataAndTensorSummary()
    {
        var path = WriteFixture(kvCount: 4, tensorCount: 2);
        try
        {
            var metadata = GgufMetadataReader.Read(path);

            metadata.Version.ShouldBe(3u);
            metadata.Architecture.ShouldBe("llama");
            metadata.ContextLength.ShouldBe(4096);
            metadata.HasChatTemplate.ShouldBeTrue();
            metadata.VocabularySize.ShouldBe(3);
            metadata.TensorCount.ShouldBe(2);
            metadata.ParameterCount.ShouldBe(32 + 6);
            metadata.Quantization.ShouldBe("Q4_K");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsWrongMagic()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(path))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(0xDEADBEEFu);
            }

            Should.Throw<InvalidDataException>(() => GgufMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsUnsupportedVersion()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(path))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(0x46554747u);
                writer.Write(9u);
                writer.Write(0ul);
                writer.Write(0ul);
            }

            Should.Throw<InvalidDataException>(() => GgufMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsTruncatedMetadata()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(path))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(0x46554747u);
                writer.Write(3u);
                writer.Write(0ul);
                writer.Write(1ul);
                WriteString(writer, "general.architecture");
                writer.Write(8u);          // string
                writer.Write(100ul);       // declares 100 bytes, none follow
            }

            Should.Throw<EndOfStreamException>(() => GgufMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteFixture(ulong kvCount, ulong tensorCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"alak-gguf-{Guid.NewGuid():N}.gguf");
        using var fs = File.Create(path);
        using var writer = new BinaryWriter(fs);

        writer.Write(0x46554747u); // magic
        writer.Write(3u);          // version
        writer.Write(tensorCount);
        writer.Write(kvCount);

        WriteString(writer, "general.architecture");
        writer.Write(8u);
        WriteString(writer, "llama");

        WriteString(writer, "llama.context_length");
        writer.Write(4u);          // uint32
        writer.Write(4096u);

        WriteString(writer, "tokenizer.chat_template");
        writer.Write(8u);
        WriteString(writer, "{{ .Prompt }}");

        WriteString(writer, "tokenizer.ggml.tokens");
        writer.Write(9u);          // array
        writer.Write(8u);          // of strings
        writer.Write(3ul);
        WriteString(writer, "a");
        WriteString(writer, "b");
        WriteString(writer, "c");

        WriteTensor(writer, "token_embd.weight", new ulong[] { 8, 4 }, type: 12);
        WriteTensor(writer, "output.weight", new ulong[] { 2, 3 }, type: 0);

        return path;
    }

    private static void WriteTensor(BinaryWriter writer, string name, ulong[] dims, uint type)
    {
        WriteString(writer, name);
        writer.Write((uint)dims.Length);
        foreach (var dim in dims) writer.Write(dim);
        writer.Write(type);
        writer.Write(0ul);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((ulong)bytes.Length);
        writer.Write(bytes);
    }
}
