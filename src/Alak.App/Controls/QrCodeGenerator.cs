using System;
using System.Collections.Generic;
using System.Text;

namespace Alak.App.Controls;

/// <summary>
/// Lightweight pure C# QR code generator (0 MB dependencies, zero SkiaSharp overhead).
/// Supports Byte mode encoding for standard text/promo strings up to Version 4 (33x33).
/// </summary>
public static class QrCodeGenerator
{
    private static readonly byte[] ExpTable = new byte[512];
    private static readonly byte[] LogTable = new byte[256];

    static QrCodeGenerator()
    {
        // Initialize Galois Field GF(256) tables with primitive polynomial 0x11D
        int val = 1;
        for (int i = 0; i < 255; i++)
        {
            ExpTable[i] = (byte)val;
            ExpTable[i + 255] = (byte)val;
            LogTable[val] = (byte)i;
            val <<= 1;
            if ((val & 0x100) != 0) val ^= 0x11D;
        }
    }

    private static byte GfMul(byte a, byte b)
    {
        if (a == 0 || b == 0) return 0;
        return ExpTable[LogTable[a] + LogTable[b]];
    }

    public static bool[,]? GenerateMatrix(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var dataBytes = Encoding.UTF8.GetBytes(text);
        int len = dataBytes.Length;

        // Determine smallest QR version (ECC Level L)
        int version;
        int totalDataBytes;
        int ecBytes;

        if (len <= 17)
        {
            version = 1;
            totalDataBytes = 19;
            ecBytes = 7;
        }
        else if (len <= 32)
        {
            version = 2;
            totalDataBytes = 34;
            ecBytes = 10;
        }
        else if (len <= 53)
        {
            version = 3;
            totalDataBytes = 55;
            ecBytes = 15;
        }
        else if (len <= 78)
        {
            version = 4;
            totalDataBytes = 80;
            ecBytes = 20;
        }
        else
        {
            // Payload exceeds Version 4 capacity (78 bytes); return null to allow fallback to Code 128
            return null;
        }

        int size = 21 + (version - 1) * 4;

        // 1. Bitstream encoding: 0100 (Byte mode) + 8-bit length + data
        var bits = new List<bool>();
        // Mode indicator 0100
        bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

        // Character count (8 bits for V1-V4 byte mode)
        for (int i = 7; i >= 0; i--)
        {
            bits.Add(((len >> i) & 1) == 1);
        }

        // Data bytes
        foreach (var b in dataBytes)
        {
            for (int i = 7; i >= 0; i--)
            {
                bits.Add(((b >> i) & 1) == 1);
            }
        }

        // Terminator up to 4 zero bits
        int maxDataBits = totalDataBytes * 8;
        int term = Math.Min(4, maxDataBits - bits.Count);
        for (int i = 0; i < term; i++) bits.Add(false);

        // Pad to byte boundary
        while (bits.Count % 8 != 0) bits.Add(false);

        // Convert to bytes
        var dataCodewords = new byte[totalDataBytes];
        int byteIdx = 0;
        for (int i = 0; i < bits.Count && byteIdx < totalDataBytes; i += 8)
        {
            byte cur = 0;
            for (int j = 0; j < 8; j++)
            {
                if (i + j < bits.Count && bits[i + j]) cur |= (byte)(1 << (7 - j));
            }
            dataCodewords[byteIdx++] = cur;
        }

        // Pad with alternating 0xEC, 0x11
        byte pad = 0xEC;
        while (byteIdx < totalDataBytes)
        {
            dataCodewords[byteIdx++] = pad;
            pad = (pad == 0xEC) ? (byte)0x11 : (byte)0xEC;
        }

        // 2. Reed-Solomon Error Correction Codewords
        var ecCodewords = CalculateReedSolomon(dataCodewords, ecBytes);

        // Total bit stream to place: dataCodewords followed by ecCodewords
        var fullCodewords = new byte[totalDataBytes + ecBytes];
        Array.Copy(dataCodewords, 0, fullCodewords, 0, totalDataBytes);
        Array.Copy(ecCodewords, 0, fullCodewords, totalDataBytes, ecBytes);

        // 3. Matrix layout
        var matrix = new bool[size, size];
        var isReserved = new bool[size, size];

        // Finder patterns
        PlaceFinder(matrix, isReserved, 0, 0, size);
        PlaceFinder(matrix, isReserved, 0, size - 7, size);
        PlaceFinder(matrix, isReserved, size - 7, 0, size);

        // Alignment patterns for version >= 2
        if (version >= 2)
        {
            int alignPos = size - 7;
            PlaceAlignment(matrix, isReserved, alignPos, alignPos);
        }

        // Timing patterns
        for (int i = 8; i < size - 8; i++)
        {
            if (!isReserved[6, i])
            {
                matrix[6, i] = (i % 2 == 0);
                isReserved[6, i] = true;
            }
            if (!isReserved[i, 6])
            {
                matrix[i, 6] = (i % 2 == 0);
                isReserved[i, 6] = true;
            }
        }

        // Dark module
        matrix[4 * version + 9, 8] = true;
        isReserved[4 * version + 9, 8] = true;

        // Reserve format info areas
        for (int i = 0; i < 9; i++)
        {
            isReserved[8, i] = true;
            isReserved[i, 8] = true;
        }
        for (int i = size - 8; i < size; i++)
        {
            isReserved[8, i] = true;
            isReserved[i, 8] = true;
        }

        // 4. Place Data and EC bits in zigzag
        int bitPos = 0;
        int totalBits = fullCodewords.Length * 8;
        int col = size - 1;
        bool upward = true;

        while (col > 0)
        {
            if (col == 6) col--; // Skip vertical timing column

            for (int r = 0; r < size; r++)
            {
                int row = upward ? (size - 1 - r) : r;
                for (int c = 0; c < 2; c++)
                {
                    int currentCol = col - c;
                    if (!isReserved[row, currentCol])
                    {
                        bool bit = false;
                        if (bitPos < totalBits)
                        {
                            int byteOffset = bitPos / 8;
                            int bitOffset = 7 - (bitPos % 8);
                            bit = ((fullCodewords[byteOffset] >> bitOffset) & 1) == 1;
                            bitPos++;
                        }

                        // Apply Mask 0: (row + col) % 2 == 0
                        if ((row + currentCol) % 2 == 0)
                        {
                            bit = !bit;
                        }

                        matrix[row, currentCol] = bit;
                    }
                }
            }

            upward = !upward;
            col -= 2;
        }

        // 5. Place Format Information (ECC Level L = 01, Mask 0 = 000 => 01000 = 8)
        // 15-bit format sequence for L + Mask 0: 0x77C4
        int formatInfo = 0x77C4;
        PlaceFormatInfo(matrix, formatInfo, size);

        return matrix;
    }

    private static void PlaceFinder(bool[,] matrix, bool[,] isReserved, int top, int left, int size)
    {
        for (int r = -1; r <= 7; r++)
        {
            for (int c = -1; c <= 7; c++)
            {
                int nr = top + r;
                int nc = left + c;
                if (nr >= 0 && nr < size && nc >= 0 && nc < size)
                {
                    isReserved[nr, nc] = true;
                    if (r >= 0 && r <= 6 && c >= 0 && c <= 6)
                    {
                        matrix[nr, nc] = (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4));
                    }
                    else
                    {
                        matrix[nr, nc] = false; // Separator
                    }
                }
            }
        }
    }

    private static void PlaceAlignment(bool[,] matrix, bool[,] isReserved, int centerRow, int centerCol)
    {
        for (int r = -2; r <= 2; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                int nr = centerRow + r;
                int nc = centerCol + c;
                isReserved[nr, nc] = true;
                matrix[nr, nc] = (Math.Abs(r) == 2 || Math.Abs(c) == 2 || (r == 0 && c == 0));
            }
        }
    }

    private static void PlaceFormatInfo(bool[,] matrix, int formatInfo, int size)
    {
        // 15 bits of format info (bit 0 is LSB, bit 14 is MSB)
        for (int i = 0; i < 15; i++)
        {
            bool bit = ((formatInfo >> i) & 1) == 1;

            // Around top-left finder
            if (i < 6)
                matrix[8, i] = bit;
            else if (i == 6)
                matrix[8, 7] = bit;
            else if (i == 7)
                matrix[8, 8] = bit;
            else if (i == 8)
                matrix[7, 8] = bit;
            else
                matrix[14 - i, 8] = bit;

            // Around top-right and bottom-left finders
            if (i < 7)
                matrix[size - 1 - i, 8] = bit;
            else
                matrix[8, size - 15 + i] = bit;
        }
    }

    private static byte[] CalculateReedSolomon(byte[] data, int ecCount)
    {
        // Generate generator polynomial
        var gen = new byte[ecCount + 1];
        gen[0] = 1;
        for (int i = 0; i < ecCount; i++)
        {
            byte root = ExpTable[i];
            for (int j = i + 1; j > 0; j--)
            {
                gen[j] = (byte)(gen[j] ^ GfMul(gen[j - 1], root));
            }
        }

        // Compute remainder (EC bytes)
        var remainder = new byte[ecCount];
        foreach (var b in data)
        {
            byte lead = (byte)(b ^ remainder[0]);
            for (int j = 0; j < ecCount - 1; j++)
            {
                remainder[j] = (byte)(remainder[j + 1] ^ GfMul(lead, gen[j + 1]));
            }
            remainder[ecCount - 1] = GfMul(lead, gen[ecCount]);
        }

        return remainder;
    }
}
