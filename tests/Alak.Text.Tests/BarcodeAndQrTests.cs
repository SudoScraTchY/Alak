using System;
using System.Collections.Generic;
using System.Linq;
using Alak.App.Controls;
using Shouldly;
using Xunit;

namespace Alak.Text.Tests;

public class BarcodeAndQrTests
{
    [Fact]
    public void QrCodeGenerator_NullOrEmpty_ReturnsNull()
    {
        QrCodeGenerator.GenerateMatrix(null!).ShouldBeNull();
        QrCodeGenerator.GenerateMatrix("").ShouldBeNull();
        QrCodeGenerator.GenerateMatrix("   ").ShouldBeNull();
    }

    [Fact]
    public void QrCodeGenerator_ExceedingVersion4Capacity_ReturnsNull()
    {
        var longText = new string('A', 85); // Version 4 max byte capacity is 78 bytes
        QrCodeGenerator.GenerateMatrix(longText).ShouldBeNull();
    }

    [Fact]
    public void QrCodeGenerator_Version1_DimensionsAndFunctionPatterns()
    {
        var matrix = QrCodeGenerator.GenerateMatrix("ALAK123");
        matrix.ShouldNotBeNull();
        matrix.GetLength(0).ShouldBe(21);
        matrix.GetLength(1).ShouldBe(21);

        int size = 21;

        // Top-left finder (7x7)
        matrix[0, 0].ShouldBeTrue();
        matrix[0, 6].ShouldBeTrue();
        matrix[6, 0].ShouldBeTrue();
        matrix[6, 6].ShouldBeTrue();
        matrix[1, 1].ShouldBeFalse();
        matrix[3, 3].ShouldBeTrue(); // center of finder

        // Top-right finder
        matrix[0, size - 1].ShouldBeTrue();
        matrix[0, size - 7].ShouldBeTrue();
        matrix[6, size - 1].ShouldBeTrue();
        matrix[6, size - 7].ShouldBeTrue();

        // Bottom-left finder
        matrix[size - 1, 0].ShouldBeTrue();
        matrix[size - 7, 0].ShouldBeTrue();
        matrix[size - 1, 6].ShouldBeTrue();
        matrix[size - 7, 6].ShouldBeTrue();

        // Timing patterns (row 6 and col 6)
        for (int i = 8; i < size - 8; i++)
        {
            matrix[6, i].ShouldBe(i % 2 == 0);
            matrix[i, 6].ShouldBe(i % 2 == 0);
        }

        // Dark module at (4*1 + 9, 8) = (13, 8)
        matrix[13, 8].ShouldBeTrue();

        // Format info bits: 0x77C4 (binary 0111 0111 1100 0100)
        // bit 0 = 0 -> (8, 0) == false, (size - 1, 8) == (20, 8) == false
        matrix[8, 0].ShouldBeFalse();
        matrix[20, 8].ShouldBeFalse();

        // bit 2 = 1 -> (8, 2) == true, (size - 3, 8) == (18, 8) == true
        matrix[8, 2].ShouldBeTrue();
        matrix[18, 8].ShouldBeTrue();

        // bit 6 = 1 -> (8, 7) == true, (size - 7, 8) == (14, 8) == true
        matrix[8, 7].ShouldBeTrue();
        matrix[14, 8].ShouldBeTrue();

        // bit 7 = 1 -> (8, 8) == true, (8, size - 8) == (8, 13) == true
        matrix[8, 8].ShouldBeTrue();
        matrix[8, 13].ShouldBeTrue();

        // bit 13 = 1 -> (1, 8) == true, (8, size - 2) == (8, 19) == true
        matrix[1, 8].ShouldBeTrue();
        matrix[8, 19].ShouldBeTrue();

        // bit 1 = 0 -> (8, 1) == false, (size - 2, 8) == (19, 8) == false
        matrix[8, 1].ShouldBeFalse();
        matrix[19, 8].ShouldBeFalse();

        // bit 14 = 1 -> (0, 8) == true, (8, size - 1) == (8, 20) == true
        matrix[0, 8].ShouldBeTrue();
        matrix[8, 20].ShouldBeTrue();
    }

    [Theory]
    [InlineData(10, 21, 0)]   // Version 1 (no alignment pattern)
    [InlineData(25, 25, 18)]  // Version 2 (alignment pattern at 18, 18)
    [InlineData(45, 29, 22)]  // Version 3 (alignment pattern at 22, 22)
    [InlineData(70, 33, 26)]  // Version 4 (alignment pattern at 26, 26)
    public void QrCodeGenerator_Versions_SizeAndAlignment(int length, int expectedSize, int alignCenter)
    {
        var text = new string('K', length);
        var matrix = QrCodeGenerator.GenerateMatrix(text);
        matrix.ShouldNotBeNull();
        matrix.GetLength(0).ShouldBe(expectedSize);
        matrix.GetLength(1).ShouldBe(expectedSize);

        if (alignCenter > 0)
        {
            // Alignment pattern 5x5: outer box true, inner box false, center true
            matrix[alignCenter - 2, alignCenter - 2].ShouldBeTrue();
            matrix[alignCenter + 2, alignCenter + 2].ShouldBeTrue();
            matrix[alignCenter - 1, alignCenter - 1].ShouldBeFalse();
            matrix[alignCenter + 1, alignCenter + 1].ShouldBeFalse();
            matrix[alignCenter, alignCenter].ShouldBeTrue(); // center module
        }
    }

    [Fact]
    public void Code128Encoder_NullOrEmpty_ReturnsNull()
    {
        Code128Encoder.Encode(null!).ShouldBeNull();
        Code128Encoder.Encode("").ShouldBeNull();
    }

    [Fact]
    public void Code128Encoder_ValidString_CorrectEncodingAndChecksum()
    {
        // "TEST": 'T'=84 (val 52), 'E'=69 (val 37), 'S'=83 (val 51), 'T'=84 (val 52)
        // Checksum = (104 + 52*1 + 37*2 + 51*3 + 52*4) % 103 = 591 % 103 = 76
        var codes = Code128Encoder.Encode("TEST");
        codes.ShouldNotBeNull();
        codes.Count.ShouldBe(7);
        codes[0].ShouldBe(104); // Start B
        codes[1].ShouldBe(52);  // 'T'
        codes[2].ShouldBe(37);  // 'E'
        codes[3].ShouldBe(51);  // 'S'
        codes[4].ShouldBe(52);  // 'T'
        codes[5].ShouldBe(76);  // Checksum
        codes[6].ShouldBe(106); // Stop
    }

    [Fact]
    public void Code128Encoder_Patterns_ModuleWidthsAreValid()
    {
        Code128Encoder.Patterns.Length.ShouldBe(107);

        for (int i = 0; i < 106; i++)
        {
            var p = Code128Encoder.Patterns[i];
            p.Length.ShouldBe(6);
            p.Sum(x => (int)x).ShouldBe(11); // Standard Code 128 characters are exactly 11 modules wide
        }

        // Stop pattern (index 106) has 7 elements and sums to 13 modules
        var stop = Code128Encoder.Patterns[106];
        stop.Length.ShouldBe(7);
        stop.Sum(x => (int)x).ShouldBe(13);
    }
}
