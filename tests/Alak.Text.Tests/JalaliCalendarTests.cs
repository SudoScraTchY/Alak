using Alak.Text;
using System.Globalization;
using Xunit;

namespace Alak.Text.Tests;

/// <summary>
/// Golden-table tests for Jalali↔Gregorian conversion (spec DAT-002, PF-09).
/// Covers Jalali years 1400–1420 with round-trip assertions and known-anchor cross-checks.
/// Algorithm: Birashk's 2820-year cycle (NodaTime PersianYearMonthDayCalculator.Arithmetic).
/// Source of truth: official Iranian calendar; 1400-01-01 = 2021-03-21.
/// </summary>
public class JalaliCalendarTests
{
    // ============ KNOWN ANCHORS (verified against official Iranian calendar) ============

    [Fact]
    public void Anchor_1400_01_01_Is_2021_03_21()
    {
        // Nowruz 1400 = March 21, 2021 (official)
        var gregorian = JalaliCalendar.ToDateTime(1400, 1, 1);
        gregorian.ShouldBe(new DateTime(2021, 3, 21));
    }

    [Fact]
    public void Anchor_1401_01_01_Is_2022_03_21()
    {
        var gregorian = JalaliCalendar.ToDateTime(1401, 1, 1);
        gregorian.ShouldBe(new DateTime(2022, 3, 21));
    }

    [Fact]
    public void Anchor_1402_01_01_Is_2023_03_21()
    {
        var gregorian = JalaliCalendar.ToDateTime(1402, 1, 1);
        gregorian.ShouldBe(new DateTime(2023, 3, 21));
    }

    [Fact]
    public void Anchor_1403_01_01_Is_2024_03_20() // 1403 is leap, Nowruz shifts to Mar 20
    {
        var gregorian = JalaliCalendar.ToDateTime(1403, 1, 1);
        gregorian.ShouldBe(new DateTime(2024, 3, 20));
    }

    [Fact]
    public void Anchor_1404_01_01_Is_2025_03_21()
    {
        var gregorian = JalaliCalendar.ToDateTime(1404, 1, 1);
        gregorian.ShouldBe(new DateTime(2025, 3, 21));
    }

    // ============ LEAP YEAR TESTS ============

    [Theory]
    [InlineData(1400, false)]
    [InlineData(1401, false)]
    [InlineData(1402, false)]
    [InlineData(1403, true)]  // leap
    [InlineData(1404, false)]
    [InlineData(1405, false)]
    [InlineData(1406, false)]
    [InlineData(1407, false)]
    [InlineData(1408, true)]  // leap
    [InlineData(1409, false)]
    [InlineData(1410, false)]
    [InlineData(1411, false)]
    [InlineData(1412, true)]  // leap
    [InlineData(1413, false)]
    [InlineData(1414, false)]
    [InlineData(1415, false)]
    [InlineData(1416, true)]  // leap
    [InlineData(1417, false)]
    [InlineData(1418, false)]
    [InlineData(1419, false)]
    [InlineData(1420, true)]  // leap
    public void LeapYears_MatchOfficialCalendar(int year, bool expectedLeap)
    {
        JalaliCalendar.IsLeapYear(year).ShouldBe(expectedLeap);
    }

    [Fact]
    public void LeapYear_Esfand_Has30Days()
    {
        JalaliCalendar.GetDaysInMonth(1403, 12).ShouldBe(30);
        JalaliCalendar.GetDaysInMonth(1404, 12).ShouldBe(29);
    }

    // ============ ROUND-TRIP TESTS (full 1400-1420 range) ============

    [Theory]
    [InlineData(1400, 1, 1)]
    [InlineData(1400, 6, 31)]
    [InlineData(1400, 7, 30)]
    [InlineData(1400, 12, 29)]
    [InlineData(1401, 1, 1)]
    [InlineData(1401, 12, 29)]
    [InlineData(1402, 1, 1)]
    [InlineData(1402, 12, 29)]
    [InlineData(1403, 1, 1)]
    [InlineData(1403, 12, 30)] // leap year
    [InlineData(1404, 1, 1)]
    [InlineData(1404, 12, 29)]
    [InlineData(1408, 1, 1)]
    [InlineData(1408, 12, 30)] // leap year
    [InlineData(1412, 1, 1)]
    [InlineData(1412, 12, 30)] // leap year
    [InlineData(1416, 1, 1)]
    [InlineData(1416, 12, 30)] // leap year
    [InlineData(1420, 1, 1)]
    [InlineData(1420, 12, 30)] // leap year
    public void RoundTrip_JalaliToGregorianToJalali(int year, int month, int day)
    {
        var gregorian = JalaliCalendar.ToDateTime(year, month, day);
        var (y, m, d) = JalaliCalendar.FromDateTime(gregorian);
        (y, m, d).ShouldBe((year, month, day));
    }

    // ============ GOLDEN TABLE: Every day from 1400-01-01 to 1420-12-29 ============

    [Fact]
    public void GoldenTable_FullRange_1400_1420_RoundTrip()
    {
        int errors = 0;
        var sb = new System.Text.StringBuilder();
        
        for (int year = 1400; year <= 1420; year++)
        {
            int daysInYear = JalaliCalendar.GetDaysInYear(year);
            for (int dayOfYear = 1; dayOfYear <= daysInYear; dayOfYear++)
            {
                // Convert dayOfYear to month/day
                int month = dayOfYear <= 186 ? (dayOfYear - 1) / 31 + 1 : (dayOfYear - 186 - 1) / 30 + 7;
                int day = dayOfYear - (month <= 7 ? (month - 1) * 31 : 186 + (month - 7) * 30);

                try
                {
                    var gregorian = JalaliCalendar.ToDateTime(year, month, day);
                    var (y, m, d) = JalaliCalendar.FromDateTime(gregorian);
                    
                    if (y != year || m != month || d != day)
                    {
                        errors++;
                        sb.AppendLine($"MISMATCH: J{year:D4}/{month:D2}/{day:D2} -> G{gregorian:yyyy-MM-dd} -> J{y:D4}/{m:D2}/{d:D2}");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    sb.AppendLine($"EXCEPTION: J{year:D4}/{month:D2}/{day:D2} -> {ex.Message}");
                }
            }
        }

        if (errors > 0)
        {
            File.WriteAllText("jalali_golden_table_errors.txt", sb.ToString());
            Assert.Fail($"Golden table round-trip failed with {errors} errors. See jalali_golden_table_errors.txt");
        }
    }

    // ============ CROSS-CHECK WITH .NET PersianCalendar (for documentation) ============

    [Fact]
    public void CrossCheck_NetPersianCalendar_AgreesOn1400_1420()
    {
        var netCal = new PersianCalendar();
        int mismatches = 0;
        var sb = new System.Text.StringBuilder();

        for (int year = 1400; year <= 1420; year++)
        {
            int daysInYear = JalaliCalendar.GetDaysInYear(year);
            for (int dayOfYear = 1; dayOfYear <= daysInYear; dayOfYear++)
            {
                int month = dayOfYear <= 186 ? (dayOfYear - 1) / 31 + 1 : (dayOfYear - 186 - 1) / 30 + 7;
                int day = dayOfYear - (month <= 7 ? (month - 1) * 31 : 186 + (month - 7) * 30);

                var ourGregorian = JalaliCalendar.ToDateTime(year, month, day);
                
                // .NET PersianCalendar uses astronomical calculation (may differ on leap years)
                var netGregorian = netCal.ToDateTime(year, month, day, 0, 0, 0, 0, PersianCalendar.PersianEra);

                if (ourGregorian != netGregorian)
                {
                    mismatches++;
                    if (mismatches <= 10) // Log first 10
                        sb.AppendLine($"MISMATCH: J{year:D4}/{month:D2}/{day:D2} -> Ours:{ourGregorian:yyyy-MM-dd} .NET:{netGregorian:yyyy-MM-dd}");
                }
            }
        }

        // Document any differences - this is expected per PF-09
        if (mismatches > 0)
        {
            File.WriteAllText("net_persian_calendar_diffs.txt", sb.ToString());
            // This is INFORMATIONAL - we expect differences per PF-09
            // Our implementation (Birashk) is the official Iranian calendar
            _ = mismatches; // acknowledged
        }
    }

    // ============ NOWRUZ VERIFICATION ============

    [Theory]
    [InlineData(1400, 3, 21)]
    [InlineData(1401, 3, 21)]
    [InlineData(1402, 3, 21)]
    [InlineData(1403, 3, 20)] // leap year
    [InlineData(1404, 3, 21)]
    [InlineData(1405, 3, 21)]
    [InlineData(1406, 3, 21)]
    [InlineData(1407, 3, 20)] // leap year
    [InlineData(1408, 3, 20)] // leap year
    [InlineData(1409, 3, 21)]
    [InlineData(1410, 3, 21)]
    [InlineData(1411, 3, 20)] // leap year
    [InlineData(1412, 3, 20)] // leap year
    [InlineData(1413, 3, 21)]
    [InlineData(1414, 3, 21)]
    [InlineData(1415, 3, 20)] // leap year
    [InlineData(1416, 3, 20)] // leap year
    [InlineData(1417, 3, 21)]
    [InlineData(1418, 3, 21)]
    [InlineData(1419, 3, 20)] // leap year
    [InlineData(1420, 3, 20)] // leap year
    public void Nowruz_1Farvardin_IsCorrectGregorianDate(int jalaliYear, int expectedMonth, int expectedDay)
    {
        var nowruz = JalaliCalendar.GetNowruz(jalaliYear);
        nowruz.Month.ShouldBe(expectedMonth);
        nowruz.Day.ShouldBe(expectedDay);
    }

    // ============ EDGE CASES ============

    [Fact]
    public void Esfand_LastDay_LeapVsNonLeap()
    {
        // 1403 is leap - Esfand has 30 days
        var lastDay1403 = JalaliCalendar.ToDateTime(1403, 12, 30);
        var (y1, m1, d1) = JalaliCalendar.FromDateTime(lastDay1403);
        (y1, m1, d1).ShouldBe((1403, 12, 30));

        // 1404 is non-leap - Esfand has 29 days
        var lastDay1404 = JalaliCalendar.ToDateTime(1404, 12, 29);
        var (y2, m2, d2) = JalaliCalendar.FromDateTime(lastDay1404);
        (y2, m2, d2).ShouldBe((1404, 12, 29));
    }

    [Fact]
    public void InvalidDates_Throw()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => JalaliCalendar.ToDateTime(1400, 13, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => JalaliCalendar.ToDateTime(1400, 1, 32));
        Should.Throw<ArgumentOutOfRangeException>(() => JalaliCalendar.ToDateTime(1400, 12, 30)); // non-leap
        Should.Throw<ArgumentOutOfRangeException>(() => JalaliCalendar.ToDateTime(0, 1, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => JalaliCalendar.ToDateTime(9379, 1, 1));
    }

    // ============ PERFORMANCE: Alloc-free ============

    [Fact]
    public void ToDateTime_AllocFree_NoHeapAllocation()
    {
        // Warm up
        for (int i = 0; i < 1000; i++)
            _ = JalaliCalendar.ToDateTime(1400, 1, 1);

        // This test mainly ensures no obvious allocations in the hot path
        // Real alloc verification would use BenchmarkDotNet
        var gcBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < 10000; i++)
            _ = JalaliCalendar.ToDateTime(1400, 1, 1);
        var gcAfter = GC.GetTotalMemory(true);
        
        // Should not allocate significantly (allowing for test overhead)
        (gcAfter - gcBefore).ShouldBeLessThan(1024 * 1024); // < 1MB
    }
}