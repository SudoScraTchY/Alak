using System;
using System.Collections.Generic;

namespace Alak.App.Controls;

/// <summary>
/// Pure C# Code 128 (Subset B) encoder with ISO/IEC 15417 compliant start/stop symbols and modulo-103 checksum.
/// </summary>
public static class Code128Encoder
{
    public static List<int>? Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var codes = new List<int>
        {
            104 // Start B
        };

        int checksum = 104;
        int weight = 1;

        foreach (char ch in text)
        {
            int val = (int)ch - 32;
            if (val < 0 || val > 95)
            {
                val = 0; // Replace unsupported with space
            }

            codes.Add(val);
            checksum += val * weight;
            weight++;
        }

        checksum %= 103;
        codes.Add(checksum);
        codes.Add(106); // Stop

        return codes;
    }

    /// <summary>
    /// Standard Code 128 pattern widths (alternating bar and space widths in modules).
    /// Index 0-105 are 6 modules; index 106 (Stop) has 7 modules.
    /// </summary>
    public static readonly byte[][] Patterns = new byte[][]
    {
        new byte[] { 2, 1, 2, 2, 2, 2 }, // 0
        new byte[] { 2, 2, 2, 1, 2, 2 }, // 1
        new byte[] { 2, 2, 2, 2, 2, 1 }, // 2
        new byte[] { 1, 2, 1, 2, 2, 3 }, // 3
        new byte[] { 1, 2, 1, 3, 2, 2 }, // 4
        new byte[] { 1, 3, 1, 2, 2, 2 }, // 5
        new byte[] { 1, 2, 2, 2, 1, 3 }, // 6
        new byte[] { 1, 2, 2, 3, 1, 2 }, // 7
        new byte[] { 1, 3, 2, 2, 1, 2 }, // 8
        new byte[] { 2, 2, 1, 2, 1, 3 }, // 9
        new byte[] { 2, 2, 1, 3, 1, 2 }, // 10
        new byte[] { 2, 3, 1, 2, 1, 2 }, // 11
        new byte[] { 1, 1, 2, 2, 3, 2 }, // 12
        new byte[] { 1, 2, 2, 1, 3, 2 }, // 13
        new byte[] { 1, 2, 2, 2, 3, 1 }, // 14
        new byte[] { 1, 1, 3, 2, 2, 2 }, // 15
        new byte[] { 1, 2, 3, 1, 2, 2 }, // 16
        new byte[] { 1, 2, 3, 2, 2, 1 }, // 17
        new byte[] { 2, 2, 3, 2, 1, 1 }, // 18
        new byte[] { 2, 2, 1, 1, 3, 2 }, // 19
        new byte[] { 2, 2, 1, 2, 3, 1 }, // 20
        new byte[] { 2, 1, 3, 2, 1, 2 }, // 21
        new byte[] { 2, 2, 3, 1, 1, 2 }, // 22
        new byte[] { 3, 1, 2, 1, 3, 1 }, // 23
        new byte[] { 3, 1, 1, 2, 2, 2 }, // 24
        new byte[] { 3, 2, 1, 1, 2, 2 }, // 25
        new byte[] { 3, 2, 1, 2, 2, 1 }, // 26
        new byte[] { 3, 1, 2, 2, 1, 2 }, // 27
        new byte[] { 3, 2, 2, 1, 1, 2 }, // 28
        new byte[] { 3, 2, 2, 2, 1, 1 }, // 29
        new byte[] { 2, 1, 2, 1, 2, 3 }, // 30
        new byte[] { 2, 1, 2, 3, 2, 1 }, // 31
        new byte[] { 2, 3, 2, 1, 2, 1 }, // 32
        new byte[] { 1, 1, 1, 3, 2, 3 }, // 33
        new byte[] { 1, 3, 1, 1, 2, 3 }, // 34
        new byte[] { 1, 3, 1, 3, 2, 1 }, // 35
        new byte[] { 1, 1, 2, 3, 1, 3 }, // 36
        new byte[] { 1, 3, 2, 1, 1, 3 }, // 37
        new byte[] { 1, 3, 2, 3, 1, 1 }, // 38
        new byte[] { 2, 1, 1, 3, 1, 3 }, // 39
        new byte[] { 2, 3, 1, 1, 1, 3 }, // 40
        new byte[] { 2, 3, 1, 3, 1, 1 }, // 41
        new byte[] { 1, 1, 2, 1, 3, 3 }, // 42
        new byte[] { 1, 1, 2, 3, 3, 1 }, // 43
        new byte[] { 1, 3, 2, 1, 3, 1 }, // 44
        new byte[] { 1, 1, 3, 1, 2, 3 }, // 45
        new byte[] { 1, 1, 3, 3, 2, 1 }, // 46
        new byte[] { 1, 3, 3, 1, 2, 1 }, // 47
        new byte[] { 3, 1, 3, 1, 2, 1 }, // 48
        new byte[] { 2, 1, 1, 3, 3, 1 }, // 49
        new byte[] { 2, 3, 1, 1, 3, 1 }, // 50
        new byte[] { 2, 1, 3, 1, 1, 3 }, // 51
        new byte[] { 2, 1, 3, 3, 1, 1 }, // 52
        new byte[] { 2, 1, 3, 1, 3, 1 }, // 53
        new byte[] { 3, 1, 1, 1, 2, 3 }, // 54
        new byte[] { 3, 1, 1, 3, 2, 1 }, // 55
        new byte[] { 3, 3, 1, 1, 2, 1 }, // 56
        new byte[] { 3, 1, 2, 1, 1, 3 }, // 57
        new byte[] { 3, 1, 2, 3, 1, 1 }, // 58
        new byte[] { 3, 3, 2, 1, 1, 1 }, // 59
        new byte[] { 3, 1, 4, 1, 1, 1 }, // 60
        new byte[] { 2, 2, 1, 4, 1, 1 }, // 61
        new byte[] { 4, 3, 1, 1, 1, 1 }, // 62
        new byte[] { 1, 1, 1, 2, 2, 4 }, // 63
        new byte[] { 1, 1, 1, 4, 2, 2 }, // 64
        new byte[] { 1, 2, 1, 1, 2, 4 }, // 65
        new byte[] { 1, 2, 1, 4, 2, 1 }, // 66
        new byte[] { 1, 4, 1, 1, 2, 2 }, // 67
        new byte[] { 1, 4, 1, 2, 2, 1 }, // 68
        new byte[] { 1, 1, 2, 2, 1, 4 }, // 69
        new byte[] { 1, 1, 2, 4, 1, 2 }, // 70
        new byte[] { 1, 2, 2, 1, 1, 4 }, // 71
        new byte[] { 1, 2, 2, 4, 1, 1 }, // 72
        new byte[] { 1, 4, 2, 1, 1, 2 }, // 73
        new byte[] { 1, 4, 2, 2, 1, 1 }, // 74
        new byte[] { 2, 4, 1, 2, 1, 1 }, // 75
        new byte[] { 2, 2, 1, 1, 1, 4 }, // 76
        new byte[] { 4, 1, 3, 1, 1, 1 }, // 77
        new byte[] { 2, 4, 1, 1, 1, 2 }, // 78
        new byte[] { 1, 3, 4, 1, 1, 1 }, // 79
        new byte[] { 1, 1, 1, 2, 4, 2 }, // 80
        new byte[] { 1, 2, 1, 1, 4, 2 }, // 81
        new byte[] { 1, 2, 1, 2, 4, 1 }, // 82
        new byte[] { 1, 1, 4, 2, 1, 2 }, // 83
        new byte[] { 1, 2, 4, 1, 1, 2 }, // 84
        new byte[] { 1, 2, 4, 2, 1, 1 }, // 85
        new byte[] { 4, 1, 1, 2, 1, 2 }, // 86
        new byte[] { 4, 2, 1, 1, 1, 2 }, // 87
        new byte[] { 4, 2, 1, 2, 1, 1 }, // 88
        new byte[] { 2, 1, 2, 1, 4, 1 }, // 89
        new byte[] { 2, 1, 4, 1, 2, 1 }, // 90
        new byte[] { 4, 1, 2, 1, 2, 1 }, // 91
        new byte[] { 1, 1, 1, 1, 4, 3 }, // 92
        new byte[] { 1, 1, 1, 3, 4, 1 }, // 93
        new byte[] { 1, 3, 1, 1, 4, 1 }, // 94
        new byte[] { 1, 1, 4, 1, 1, 3 }, // 95
        new byte[] { 1, 1, 4, 3, 1, 1 }, // 96
        new byte[] { 4, 1, 1, 1, 1, 3 }, // 97
        new byte[] { 4, 1, 1, 3, 1, 1 }, // 98
        new byte[] { 1, 1, 3, 1, 4, 1 }, // 99
        new byte[] { 1, 1, 4, 1, 3, 1 }, // 100
        new byte[] { 3, 1, 1, 1, 4, 1 }, // 101
        new byte[] { 4, 1, 1, 1, 3, 1 }, // 102
        new byte[] { 2, 1, 1, 4, 1, 2 }, // 103 (Start A)
        new byte[] { 2, 1, 1, 2, 1, 4 }, // 104 (Start B)
        new byte[] { 2, 1, 1, 2, 3, 2 }, // 105 (Start C)
        new byte[] { 2, 3, 3, 1, 1, 1, 2 } // 106 (Stop - 7 elements)
    };
}
