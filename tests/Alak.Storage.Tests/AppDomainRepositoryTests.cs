using Alak.Core.Model;
using Alak.Storage;

namespace Alak.Storage.Tests;

public class AppDomainRepositoryTests : IDisposable
{
    private readonly TestDatabase _db;
    private readonly StorageConnection _connection;
    private readonly AppDomainRepository _domains;
    private readonly NotificationRepository _notes;

    public AppDomainRepositoryTests()
    {
        _db = new TestDatabase();
        _connection = _db.Open();
        _domains = new AppDomainRepository(_connection);
        _notes = new NotificationRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Learn_RequiresTwoSightingsBeforeAttribution()
    {
        await _domains.LearnAsync(new[] { "digikala.com" }, "com.digikala");
        (await _domains.ResolvePackageAsync(new[] { "digikala.com" })).ShouldBeNull();

        await _domains.LearnAsync(new[] { "digikala.com" }, "com.digikala");
        (await _domains.ResolvePackageAsync(new[] { "digikala.com" })).ShouldBe("com.digikala");
    }

    [Fact]
    public async Task Resolve_IsNullWhenTwoPackagesShareADomain()
    {
        await _domains.LearnAsync(new[] { "shop.example.com" }, "com.a");
        await _domains.LearnAsync(new[] { "shop.example.com" }, "com.a");
        await _domains.LearnAsync(new[] { "shop.example.com" }, "com.b");
        await _domains.LearnAsync(new[] { "shop.example.com" }, "com.b");

        (await _domains.ResolvePackageAsync(new[] { "shop.example.com" })).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_MatchesAcrossDomainsFromOneNotification()
    {
        await _domains.LearnAsync(new[] { "digikala.com" }, "com.digikala");
        await _domains.LearnAsync(new[] { "digikala.com" }, "com.digikala");
        await _domains.LearnAsync(new[] { "mydigipay.ir" }, "com.digikala");
        await _domains.LearnAsync(new[] { "mydigipay.ir" }, "com.digikala");

        (await _domains.ResolvePackageAsync(new[] { "digikala.com", "mydigipay.ir" }))
            .ShouldBe("com.digikala");
    }

    [Fact]
    public async Task CommunicationPackages_AreNeverLearnedOrAttributed()
    {
        await _domains.LearnAsync(new[] { "digikala.com" }, "com.google.android.apps.messaging");
        await _domains.LearnAsync(new[] { "digikala.com" }, "com.google.android.apps.messaging");

        (await _domains.ResolvePackageAsync(new[] { "digikala.com" })).ShouldBeNull();
    }

    [Fact]
    public async Task AttributedPackage_RoundTripsThroughNotificationRepository()
    {
        await _notes.AddAsync(new Notification(
            Id: "sms-1",
            Package: "com.google.android.apps.messaging",
            ChannelName: "messages",
            Title: "Digikala",
            Text: "کد تخفیف: X1 https://digikala.com/x",
            Category: "promo",
            Timestamp: DateTimeOffset.UtcNow,
            IsRead: false,
            IsPersian: true,
            ExtractedCode: "X1",
            AttributedPackage: "com.digikala"));

        var stored = await _notes.GetByIdAsync("sms-1");
        stored.ShouldNotBeNull();
        stored!.AttributedPackage.ShouldBe("com.digikala");
        stored.SourcePackage.ShouldBe("com.digikala");
        stored.IsAttributed.ShouldBeTrue();
    }

    [Fact]
    public async Task AppInfo_IsUpsertedAndRead()
    {
        await _notes.UpsertAppInfoAsync("com.digikala", "Digikala", new byte[] { 1, 2, 3 });
        await _notes.UpsertAppInfoAsync("com.digikala", null, null);

        var infos = await _notes.GetAppInfosAsync(new[] { "com.digikala" });
        infos.ShouldContainKey("com.digikala");
        infos["com.digikala"].Label.ShouldBe("Digikala");
        infos["com.digikala"].IconPng.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task PackagesWithoutLabel_ExcludesLabelledApps()
    {
        await _notes.UpsertAppInfoAsync("com.a", "App A", null);
        await _notes.AddAsync(new Notification(
            "n1", "com.b", "", "", "", "other", DateTimeOffset.UtcNow, false, false));

        var packages = await _notes.GetPackagesWithoutLabelAsync(10);
        packages.ShouldContain("com.b");
        packages.ShouldNotContain("com.a");
    }
}
