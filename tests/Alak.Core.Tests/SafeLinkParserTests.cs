using Alak.Core.Linking;

namespace Alak.Core.Tests;

public class SafeLinkParserTests
{
    [Fact]
    public void ExtractsRegistrableDomain_AndStripsWwwAndSubdomains()
    {
        SafeLinkParser.TryGetDomain("https://www.shop.digikala.com/products/1", out var domain).ShouldBeTrue();
        domain.ShouldBe("digikala.com");

        SafeLinkParser.TryGetDomain("www.digikala.com", out domain).ShouldBeTrue();
        domain.ShouldBe("digikala.com");

        SafeLinkParser.TryGetDomain("https://digikala.com./x", out domain).ShouldBeTrue();
        domain.ShouldBe("digikala.com");

        SafeLinkParser.TryGetDomain("https://digikala.com:8080/x", out domain).ShouldBeTrue();
        domain.ShouldBe("digikala.com");
    }

    [Fact]
    public void KeepsSecondLevelSuffixes()
    {
        SafeLinkParser.TryGetDomain("https://shop.example.co.uk/x", out var domain).ShouldBeTrue();
        domain.ShouldBe("example.co.uk");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/x")]
    [InlineData("http://user:pass@digikala.com/x")]
    [InlineData("http://192.168.1.10/x")]
    [InlineData("http://[::1]/x")]
    [InlineData("http://localhost/x")]
    [InlineData("https://-bad-.com/x")]
    [InlineData("https://exa mple.com/x")]
    [InlineData("example.com")]
    [InlineData("")]
    public void RejectsUnsafeOrAmbiguousCandidates(string candidate)
    {
        SafeLinkParser.TryGetDomain(candidate, out var domain).ShouldBeFalse();
        domain.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractDomains_DedupesAndTrimsPunctuation()
    {
        var domains = SafeLinkParser.ExtractDomains(
            "تخفیف ویژه",
            "خرید از https://www.digikala.com/x، و https://mydigipay.ir/pay! و دوباره https://shop.digikala.com/y",
            "see (https://snapp.ir/ride).");

        domains.ShouldBe(new[] { "digikala.com", "mydigipay.ir", "snapp.ir" });
    }

    [Fact]
    public void ExtractDomains_IsBounded()
    {
        var text = string.Join(' ', Enumerable.Range(0, 25).Select(i => $"https://site{i}.ir/x"));
        SafeLinkParser.ExtractDomains(null, text).Count.ShouldBe(SafeLinkParser.MaxLinksPerNotification);
    }

    [Fact]
    public void ExtractDomains_IgnoresBareDomainsAndPlainText()
    {
        SafeLinkParser.ExtractDomains("digikala.com", "no links here").ShouldBeEmpty();
    }

    [Fact]
    public void ExtractDomains_AcceptsInternationalizedHosts()
    {
        var domains = SafeLinkParser.ExtractDomains(null, "https://مثال.ایران/مسیر");
        domains.Count.ShouldBe(1);
        domains[0].ShouldStartWith("xn--");
    }
}
