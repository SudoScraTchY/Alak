using Alak.Text;

namespace Alak.Text.Tests;

// §8.2 normalization: two parallel views + span map.
public class NormalizationTests
{
    [Fact]
    public void NRM001_Nfkc_FoldsFullwidthAndMathBold()
    {
        var n = Normalizer.Normalize("ＳＡＬＥ and 𝐒𝐀𝐋𝐄");
        n.Text.ShouldContain("SALE");
        n.Text.ShouldNotContain("Ｓ");
    }

    [Fact]
    public void NRM002_BidiControls_KeptInText_StrippedFromFolded()
    {
        // "SAVE20" glued to a Right-to-Left Mark must not break extraction
        var raw = "code SAVE20\u200F now";
        var n = Normalizer.Normalize(raw);
        n.Text.ShouldContain("SAVE20");
        n.Folded.ShouldNotContain('\u200F');
    }

    [Fact]
    public void NRM003_Zwnj_BecomesSpace_InFoldedView()
    {
        // ZWNJ maps to a space in the matching view (NRM-003); the lexicon's '~'
        // optional-separator is what unifies the three spellings at match time.
        var a = Normalizer.Normalize("می‌شود").Folded;
        a.ShouldContain(" ");
        var c = Normalizer.Normalize("می شود").Folded;
        (a == c).ShouldBeTrue();
    }

    [Fact]
    public void NRM004_ArabicLetters_UnifiedToPersian()
    {
        // Arabic keyboard 'كد تخفيف' must match Persian lexicon term 'کد تخفیف'
        var ar = Normalizer.Normalize("كد تخفيف").Folded;
        var fa = Normalizer.Normalize("کد تخفیف").Folded;
        ar.ShouldBe(fa);
    }

    [Fact]
    public void NRM004_TatweelAndHarakat_RemovedFromFolded()
    {
        var n = Normalizer.Normalize("تخــفـیف");
        n.Folded.ShouldBe(Normalizer.Normalize("تخفیف").Folded);
    }

    [Fact]
    public void NRM005_PersianDigits_Separators_ToAscii()
    {
        var n = Normalizer.Normalize("۵۰٪");
        n.Folded.ShouldContain("50%");

        var big = Normalizer.Normalize("۲٬۰۰۰٬۰۰۰");
        big.Folded.ShouldContain("2,000,000");
    }

    [Fact]
    public void NRM006_CasePreservedInText_FoldedLowercased()
    {
        var n = Normalizer.Normalize("SaveNow10");
        n.Text.ShouldContain("SaveNow10");
        n.Folded.ShouldContain("savenow10");
    }

    [Fact]
    public void NRM007_Elongation_CollapsedInFolded()
    {
        var n = Normalizer.Normalize("SAAALE!!!");
        n.Folded.ShouldContain("sale");
        n.Folded.ShouldNotContain("saaale");
    }

    [Fact]
    public void NRM008_Whitespace_Collapsed_NewlinesPreserved()
    {
        var n = Normalizer.Normalize("a\u00A0b\tc   d\ne");
        n.Folded.ShouldBe("a b c d\ne");
    }

    [Fact]
    public void NRM009_EmojiRemovedFromFolded_CountedIntoSignals()
    {
        var n = Normalizer.Normalize("🔥💥 free shipping 📦 tracking ⏰");
        n.Folded.ShouldNotContain("🔥");
        n.Folded.ShouldNotContain("📦");
        n.EmojiSignals.Promo.ShouldBeGreaterThanOrEqualTo(2);
        n.EmojiSignals.Delivery.ShouldBeGreaterThanOrEqualTo(1);
        n.EmojiSignals.Urgency.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void NRM010_SpanMap_RoundTripsVisibleGlyph()
    {
        var raw = "کد SAVE20 را وارد کنید";
        var n = Normalizer.Normalize(raw);
        // find "SAVE20" in Folded, map back to original
        var idx = n.Folded.IndexOf("save20", StringComparison.Ordinal);
        idx.ShouldBeGreaterThanOrEqualTo(0);
        var originalIdx = n.Map[idx];
        raw[n.Map[idx]..].ShouldStartWith("SAVE20");
    }

    [Fact]
    public void NRM010_OriginalSpan_Helper()
    {
        var raw = "discount 50% today";
        var n = Normalizer.Normalize(raw);
        var folded = n.Folded;
        var start = folded.IndexOf("50%", StringComparison.Ordinal);
        var (os, oe) = n.OriginalSpan(start, start + 3);
        raw[os..oe].ShouldBe("50%");
    }
}

// §8.3 script runs + language detection (advisory).
public class ScriptLanguageTests
{
    [Fact]
    public void LNG001_MixedScript_SplitsIntoRuns()
    {
        var runs = ScriptLanguage.SplitIntoRuns("کد SAVE20 را وارد کنید");
        runs.Length.ShouldBeGreaterThanOrEqualTo(3);
        runs[0].Script.ShouldBe(Script.Arab);
        runs[1].Script.ShouldBe(Script.Latn);
        runs[2].Script.ShouldBe(Script.Arab);
    }

    [Fact]
    public void LNG002_PersianDetected_ByLettersAndFunctionWords()
    {
        for (var i = 0; i < 10; i++)
        {
            var g = ScriptLanguage.Guess("کد تخفیف شما برای اولین خرید است");
            g.Language.ShouldBe(LanguageFa.Persian);
            g.Probability.ShouldBeGreaterThan(0.5f);
        }
    }

    [Fact]
    public void LNG002_ArabicDetected_ByTaaMarbutaAndFunctionWords()
    {
        var g = ScriptLanguage.Guess("خصم خاص على جميع المنتجات والخدمات");
        g.Language.ShouldBe(LanguageFa.Arabic);
        g.Probability.ShouldBeGreaterThan(0.5f);
    }

    [Fact]
    public void LNG004_DetectionIsAdvisory_NoHardFailure()
    {
        // nonsense Arabic-script text must still return a guess, never throw
        var g = ScriptLanguage.Guess("؛،؛،");
        g.ShouldNotBeNull();
    }

    [Fact]
    public void LNG005_FirstStrongDirection()
    {
        ScriptLanguage.FirstStrongDirection("کد تخفیف SAVE20")
            .ShouldBe(TextDirection.Rtl);
        ScriptLanguage.FirstStrongDirection("code کد تخفیف")
            .ShouldBe(TextDirection.Ltr);
        ScriptLanguage.FirstStrongDirection("123 codes")
            .ShouldBe(TextDirection.Ltr); // no strong char -> default LTR
    }

    [Fact]
    public void PF01_FoldSimd_FoldsSpanEquivalentToFoldChar()
    {
        // 1. Span shorter than vector length (< 8)
        char[] shortBuf = "ABC۱۲۳".ToCharArray();
        Normalizer.FoldSimd(shortBuf);
        new string(shortBuf).ShouldBe("abc123");

        // 2. Exact 8 chars
        char[] exactBuf = "ABCDEFG1".ToCharArray();
        Normalizer.FoldSimd(exactBuf);
        new string(exactBuf).ShouldBe("abcdefg1");

        // 3. Multi-chunk (> 8 chars) with mixed ASCII, Persian/Arabic digits, Arabic Yeh/Kaf
        char[] multiBuf = "SALE ۵۰% كد تخفيف ١٢٣".ToCharArray();
        Normalizer.FoldSimd(multiBuf);
        // 'SALE' -> 'sale', '۵۰' -> '50', 'كد' -> 'کد', 'تخفيف' -> 'تخفیف', '١٢٣' -> '123'
        new string(multiBuf).ShouldBe("sale 50% کد تخفیف 123");

        // 4. Verify identical behavior between FoldSimd and sequential FoldChar
        string testInput = "PROMO CODE: كد تخفيف ۱۲۳۴۵ و ١٢٣٤٥ NOW!";
        char[] simdResult = testInput.ToCharArray();
        Normalizer.FoldSimd(simdResult);

        char[] scalarResult = testInput.ToCharArray();
        for (int i = 0; i < scalarResult.Length; i++)
        {
            var f = Normalizer.FoldChar(scalarResult[i]);
            if (f.HasValue) scalarResult[i] = f.Value;
        }

        new string(simdResult).ShouldBe(new string(scalarResult));
    }
}

