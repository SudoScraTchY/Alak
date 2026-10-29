using System;
using System.Threading.Tasks;
using Alak.Core.Model;
using Alak.Storage;

namespace Alak.Storage.Tests;

public class PromoCodeRepositoryTests
{
    [Fact]
    public async Task GetActiveCodesForPackage_ExactAndWildcardMatch()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new PromoCodeRepository(con);

        var now = DateTimeOffset.UtcNow;

        var code1 = new PromoCode(
            Id: 0,
            CodeNorm: "digi100",
            RawCode: "DIGI100",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.95f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 20,
            DiscountType: DiscountType.Percent,
            Currency: "IRR",
            MinSpend: 500000,
            Cap: 100000,
            Scope: "First order",
            FirstOrder: true,
            SingleUse: false,
            ValidFrom: now.AddDays(-1),
            ValidUntil: now.AddDays(5),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        var code2 = new PromoCode(
            Id: 0,
            CodeNorm: "snapp50",
            RawCode: "SNAPP50",
            Package: "cab.snapp.passenger",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 50000,
            DiscountType: DiscountType.Amount,
            Currency: "IRR",
            MinSpend: null,
            Cap: null,
            Scope: "Ride",
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: now.AddDays(-1),
            ValidUntil: now.AddDays(2),
            StoreHint: "Snapp",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        await repo.UpsertAsync(code1);
        await repo.UpsertAsync(code2);

        // Exact match
        var digiCodes = await repo.GetActiveCodesForPackageAsync("com.digikala.app");
        digiCodes.Count.ShouldBe(1);
        digiCodes[0].RawCode.ShouldBe("DIGI100");

        // Partial / wildcard match
        var digiPartial = await repo.GetActiveCodesForPackageAsync("digikala");
        digiPartial.Count.ShouldBe(1);
        digiPartial[0].RawCode.ShouldBe("DIGI100");

        // Snapp query
        var snappCodes = await repo.GetActiveCodesForPackageAsync("cab.snapp.passenger");
        snappCodes.Count.ShouldBe(1);
        snappCodes[0].RawCode.ShouldBe("SNAPP50");

        // Unrelated package query
        var emptyCodes = await repo.GetActiveCodesForPackageAsync("com.tapsi.cab");
        emptyCodes.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetActiveCodesForPackage_FiltersExpiredDates()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new PromoCodeRepository(con);

        var now = DateTimeOffset.UtcNow;

        // Expired yesterday
        var expiredCode = new PromoCode(
            Id: 0,
            CodeNorm: "olddeal",
            RawCode: "OLDDEAL",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.95f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 10,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: now.AddDays(-10),
            ValidUntil: now.AddDays(-1),
            StoreHint: "Digikala",
            FirstSeenAt: now.AddDays(-10).ToUnixTimeMilliseconds(),
            LastSeenAt: now.AddDays(-5).ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        // Valid for 3 more days
        var validCode = new PromoCode(
            Id: 0,
            CodeNorm: "activedeal",
            RawCode: "ACTIVEDEAL",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.95f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 15,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: now.AddDays(-1),
            ValidUntil: now.AddDays(3),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        // No expiry specified (infinite)
        var infiniteCode = new PromoCode(
            Id: 0,
            CodeNorm: "alwaysgood",
            RawCode: "ALWAYSGOOD",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 5,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: null,
            ValidUntil: null,
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        await repo.UpsertAsync(expiredCode);
        await repo.UpsertAsync(validCode);
        await repo.UpsertAsync(infiniteCode);

        var activeCodes = await repo.GetActiveCodesForPackageAsync("com.digikala.app");
        activeCodes.Count.ShouldBe(2);
        activeCodes.ShouldContain(c => c.RawCode == "ACTIVEDEAL");
        activeCodes.ShouldContain(c => c.RawCode == "ALWAYSGOOD");
        activeCodes.ShouldNotContain(c => c.RawCode == "OLDDEAL");
    }

    [Fact]
    public async Task GetActiveCodesForPackage_FiltersStatuses()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new PromoCodeRepository(con);

        var now = DateTimeOffset.UtcNow;

        var active = new PromoCode(
            Id: 0,
            CodeNorm: "codeactive",
            RawCode: "CODEACTIVE",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Active,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 10,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: null,
            ValidUntil: now.AddDays(10),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        var expiring = new PromoCode(
            Id: 0,
            CodeNorm: "codeexpiring",
            RawCode: "CODEEXPIRING",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.ExpiringSoon,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 15,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: null,
            ValidUntil: now.AddHours(12),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        var used = new PromoCode(
            Id: 0,
            CodeNorm: "codeused",
            RawCode: "CODEUSED",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Used,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 20,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: null,
            ValidUntil: now.AddDays(5),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        var expired = new PromoCode(
            Id: 0,
            CodeNorm: "codeexpired",
            RawCode: "CODEEXPIRED",
            Package: "com.digikala.app",
            AccountEmail: null,
            ChannelId: null,
            Status: PromoCodeStatus.Expired,
            Confidence: 0.90f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 25,
            DiscountType: DiscountType.Percent,
            Currency: null,
            MinSpend: null,
            Cap: null,
            Scope: null,
            FirstOrder: false,
            SingleUse: false,
            ValidFrom: null,
            ValidUntil: now.AddDays(5),
            StoreHint: "Digikala",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1
        );

        await repo.UpsertAsync(active);
        await repo.UpsertAsync(expiring);
        await repo.UpsertAsync(used);
        await repo.UpsertAsync(expired);

        var codes = await repo.GetActiveCodesForPackageAsync("com.digikala.app");
        codes.Count.ShouldBe(2);
        codes.ShouldContain(c => c.RawCode == "CODEACTIVE");
        codes.ShouldContain(c => c.RawCode == "CODEEXPIRING");
        codes.ShouldNotContain(c => c.RawCode == "CODEUSED");
        codes.ShouldNotContain(c => c.RawCode == "CODEEXPIRED");
    }

    [Fact]
    public async Task NotificationId_PersistsAndLoadsCorrectly()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new PromoCodeRepository(con);

        var now = DateTimeOffset.UtcNow;
        var sbnKey = "0|cab.snapp.passenger|100|null|10050";

        var promo = new PromoCode(
            Id: 0,
            CodeNorm: "snapdiscount",
            RawCode: "SNAPDISCOUNT",
            Package: "cab.snapp.passenger",
            AccountEmail: null,
            ChannelId: "promos",
            Status: PromoCodeStatus.Active,
            Confidence: 0.98f,
            Kind: PromoCodeKind.Promo,
            DiscountValue: 30,
            DiscountType: DiscountType.Percent,
            Currency: "IRR",
            MinSpend: null,
            Cap: 200000,
            Scope: "Trips",
            FirstOrder: false,
            SingleUse: true,
            ValidFrom: now,
            ValidUntil: now.AddDays(2),
            StoreHint: "Snapp",
            FirstSeenAt: now.ToUnixTimeMilliseconds(),
            LastSeenAt: now.ToUnixTimeMilliseconds(),
            TimesSeen: 1,
            NotificationId: sbnKey
        );

        var id = await repo.UpsertAsync(promo);
        id.ShouldBeGreaterThan(0);

        var loaded = await repo.GetByCodeNormAsync("snapdiscount", "cab.snapp.passenger");
        loaded.ShouldNotBeNull();
        loaded.NotificationId.ShouldBe(sbnKey);
        loaded.RawCode.ShouldBe("SNAPDISCOUNT");

        var all = await repo.GetAllAsync();
        all.ShouldContain(c => c.NotificationId == sbnKey);
    }
}
