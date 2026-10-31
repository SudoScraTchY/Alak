using System.Buffers.Binary;
using System.Text;

namespace Alak.Core.Models;

/// <summary>Metadata read from a GGUF header (spec MOD-002).</summary>
public sealed record GgufMetadata(
    uint Version,
    string Architecture,
    int ContextLength,
    bool HasChatTemplate,
    long VocabularySize,
    long TensorCount,
    long ParameterCount,
    string Quantization)
{
    public double ParameterCountBillions => ParameterCount / 1_000_000_000d;
}

/// <summary>
/// Minimal, bounded GGUF metadata parser (no native code). Reads the header,
/// metadata key/values and tensor infos to report architecture, context length,
/// chat-template presence, vocabulary size, tensor count, parameter count and the
/// dominant tensor quantization. Strings/arrays are skipped with hard bounds so a
/// malformed or hostile file cannot allocate unbounded memory.
/// </summary>
public static class GgufMetadataReader
{
    private const uint Magic = 0x46554747; // "GGUF"
    private const int MaxKvCount = 1_000_000;
    private const int MaxTensorCount = 1_000_000;
    private const int MaxKeyLength = 16 * 1024;
    private const int MaxStringValueLength = 16 * 1024 * 1024;
    private const long MaxArrayElements = 10_000_000;

    private static readonly Dictionary<uint, string> s_tensorTypes = new()
    {
        [0] = "F32", [1] = "F16", [2] = "Q4_0", [3] = "Q4_1", [6] = "Q5_0", [7] = "Q5_1",
        [8] = "Q8_0", [9] = "Q8_1", [10] = "Q2_K", [11] = "Q3_K", [12] = "Q4_K", [13] = "Q5_K",
        [14] = "Q6_K", [15] = "Q8_K", [16] = "IQ2_XXS", [17] = "IQ2_XS", [18] = "IQ3_XXS",
        [19] = "IQ1_S", [20] = "IQ4_NL", [21] = "IQ3_S", [22] = "IQ2_S", [23] = "IQ4_XS",
        [24] = "I8", [25] = "I16", [26] = "I32", [27] = "I64", [28] = "F64", [29] = "IQ1_M",
        [30] = "BF16", [31] = "Q4_0_4_4", [32] = "Q4_0_4_8", [33] = "Q4_0_8_8",
        [34] = "TQ1_0", [35] = "TQ2_0", [36] = "IQ4_NL_4_4", [37] = "IQ4_NL_4_8", [38] = "IQ4_NL_8_8"
    };

    public static GgufMetadata Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("Invalid GGUF file: incorrect magic header.");
        }

        var version = reader.ReadUInt32();
        if (version is 0 or > 3)
        {
            throw new InvalidDataException($"Unsupported GGUF version {version}.");
        }

        var tensorCount = ReadU64(reader, "tensor count");
        var kvCount = ReadU64(reader, "metadata count");
        if (tensorCount > (ulong)MaxTensorCount || kvCount > (ulong)MaxKvCount)
        {
            throw new InvalidDataException("GGUF header declares an unreasonable number of entries.");
        }

        string? architecture = null;
        int contextLength = 0;
        bool hasChatTemplate = false;
        long vocabularySize = 0;

        for (ulong i = 0; i < kvCount; i++)
        {
            var key = ReadString(reader, MaxKeyLength);
            var valueType = reader.ReadUInt32();

            switch (valueType)
            {
                case 8: // string
                {
                    var length = ReadU64(reader, "string length");
                    if (length > MaxStringValueLength) throw new InvalidDataException("GGUF string value is too large.");

                    if (key == "tokenizer.chat_template")
                    {
                        hasChatTemplate = length > 0;
                        Skip(reader, (long)length);
                    }
                    else if (key == "general.architecture")
                    {
                        architecture = ReadStringBody(reader, (int)length);
                    }
                    else
                    {
                        Skip(reader, (long)length);
                    }
                    break;
                }

                case 9: // array
                {
                    var elementType = reader.ReadUInt32();
                    var count = ReadU64(reader, "array length");
                    if (count > (ulong)MaxArrayElements) throw new InvalidDataException("GGUF array is too large.");

                    if (key == "tokenizer.ggml.tokens")
                    {
                        vocabularySize = (long)count;
                    }

                    SkipArray(reader, elementType, count);
                    break;
                }

                default:
                {
                    var value = ReadScalar(reader, valueType);
                    if (key.EndsWith(".context_length", StringComparison.Ordinal) && value is not null)
                    {
                        contextLength = (int)Math.Min(value.Value, (ulong)int.MaxValue);
                    }
                    break;
                }
            }
        }

        architecture ??= "unknown";

        long parameterCount = 0;
        var typeCounts = new Dictionary<uint, long>();

        for (ulong i = 0; i < tensorCount; i++)
        {
            ReadString(reader, MaxKeyLength); // tensor name
            var dims = reader.ReadUInt32();
            if (dims > 16) throw new InvalidDataException("GGUF tensor has too many dimensions.");

            long elements = 1;
            for (var d = 0; d < dims; d++)
            {
                var dim = ReadU64(reader, "tensor dimension");
                if (dim == 0) throw new InvalidDataException("GGUF tensor dimension is zero.");
                elements = elements > long.MaxValue / (long)dim ? long.MaxValue : elements * (long)dim;
            }

            var tensorType = reader.ReadUInt32();
            ReadU64(reader, "tensor offset");

            parameterCount = parameterCount > long.MaxValue - elements ? long.MaxValue : parameterCount + elements;
            typeCounts[tensorType] = typeCounts.TryGetValue(tensorType, out var existing) ? existing + 1 : 1;
        }

        var dominantType = typeCounts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
        var quantization = s_tensorTypes.TryGetValue(dominantType, out var name) ? name : "unknown";

        return new GgufMetadata(
            Version: version,
            Architecture: architecture,
            ContextLength: contextLength,
            HasChatTemplate: hasChatTemplate,
            VocabularySize: vocabularySize,
            TensorCount: (long)tensorCount,
            ParameterCount: parameterCount,
            Quantization: quantization);
    }

    private static ulong ReadU64(BinaryReader reader, string what)
    {
        var value = reader.ReadUInt64();
        if (value > long.MaxValue) throw new InvalidDataException($"GGUF {what} is out of range.");
        return value;
    }

    private static string ReadString(BinaryReader reader, int maxLength)
    {
        var length = ReadU64(reader, "string length");
        if (length > (ulong)maxLength) throw new InvalidDataException("GGUF string is too large.");
        return ReadStringBody(reader, (int)length);
    }

    private static string ReadStringBody(BinaryReader reader, int length)
    {
        if (length == 0) return string.Empty;
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException("Truncated GGUF string.");
        return Encoding.UTF8.GetString(bytes);
    }

    private static void Skip(BinaryReader reader, long count)
    {
        if (count < 0 || reader.BaseStream.Position + count > reader.BaseStream.Length)
        {
            throw new EndOfStreamException("Truncated GGUF file.");
        }
        reader.BaseStream.Seek(count, SeekOrigin.Current);
    }

    private static void SkipArray(BinaryReader reader, uint elementType, ulong count)
    {
        switch (elementType)
        {
            case 8: // strings
                for (ulong i = 0; i < count; i++)
                {
                    var length = ReadU64(reader, "string length");
                    if (length > MaxStringValueLength) throw new InvalidDataException("GGUF string is too large.");
                    Skip(reader, (long)length);
                }
                break;

            case 9:
                throw new InvalidDataException("Nested GGUF arrays are not supported.");

            default:
            {
                if (count == 0) break;
                ReadScalar(reader, elementType);
                var size = elementType switch
                {
                    0 or 1 or 7 => 1,
                    2 or 3 => 2,
                    4 or 5 or 6 => 4,
                    10 or 11 or 12 => 8,
                    _ => throw new InvalidDataException($"Unknown GGUF value type {elementType}.")
                };
                Skip(reader, checked((long)count * size - size));
                break;
            }
        }
    }

    private static ulong? ReadScalar(BinaryReader reader, uint valueType) => valueType switch
    {
        0 => reader.ReadByte(),
        1 => unchecked((ulong)(long)reader.ReadSByte()),
        2 => reader.ReadUInt16(),
        3 => unchecked((ulong)(long)reader.ReadInt16()),
        4 => reader.ReadUInt32(),
        5 => unchecked((ulong)(long)reader.ReadInt32()),
        6 => BitConverter.SingleToUInt32Bits(reader.ReadSingle()),
        7 => reader.ReadByte(),
        10 => reader.ReadUInt64(),
        11 => unchecked((ulong)reader.ReadInt64()),
        12 => unchecked((ulong)BitConverter.DoubleToInt64Bits(reader.ReadDouble())),
        _ => throw new InvalidDataException($"Unknown GGUF value type {valueType}.")
    };
}
