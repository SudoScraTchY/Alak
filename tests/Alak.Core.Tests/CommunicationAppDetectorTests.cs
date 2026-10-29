using Alak.Core.Capture;
using Shouldly;
using Xunit;

namespace Alak.Core.Tests;

public class CommunicationAppDetectorTests
{
    private static RawNotification MakeWith(
        string package = "com.example.shop",
        string? androidCategory = null,
        string? conversationTitle = null)
    {
        return new RawNotification(
            SbnKey: "test-key-1",
            Package: package,
            Id: 101,
            Tag: null,
            PostedAtUtcMs: 1_750_000_000_000L,
            TzOffsetMin: 210,
            ChannelId: "ch-1",
            ChannelName: "Notifications",
            AndroidCategory: androidCategory,
            Importance: 3,
            IsOngoing: false,
            IsGroupSummary: false,
            IsForegroundService: false,
            IsClearable: true,
            GroupKey: null,
            Title: "Hello",
            Text: "Test message body",
            BigText: null,
            SubText: null,
            TextLines: Array.Empty<string>(),
            ActionLabels: Array.Empty<string>(),
            ConversationTitle: conversationTitle,
            LooksRedactedByOs: false,
            UserHandle: 0);
    }

    [Theory]
    [InlineData("com.google.android.apps.messaging")]
    [InlineData("com.samsung.android.messaging")]
    [InlineData("com.android.mms")]
    [InlineData("org.telegram.messenger")]
    [InlineData("org.telegram.plus")]
    [InlineData("com.whatsapp")]
    [InlineData("com.whatsapp.w4b")]
    [InlineData("com.google.android.gm")]
    [InlineData("com.microsoft.office.outlook")]
    [InlineData("org.thoughtcrime.securesms")]
    [InlineData("ir.bale.messenger")]
    [InlineData("ir.resello.rubika")]
    [InlineData("ir.eitaa.messenger")]
    public void IsCommunicationApp_KnownCommunicationApps_ReturnsTrue(string package)
    {
        CommunicationAppDetector.IsCommunicationApp(package).ShouldBeTrue();
    }

    [Theory]
    [InlineData("com.digikala")]
    [InlineData("com.snapp.taxi")]
    [InlineData("com.spotify.music")]
    [InlineData("com.amazon.mShop.android.shopping")]
    [InlineData(null)]
    [InlineData("")]
    public void IsCommunicationApp_NonCommunicationApps_ReturnsFalse(string? package)
    {
        CommunicationAppDetector.IsCommunicationApp(package).ShouldBeFalse();
    }

    [Fact]
    public void IsCommunicationNotification_WhenPackageIsCommunication_ReturnsTrue()
    {
        var n = MakeWith(package: "org.telegram.messenger");
        CommunicationAppDetector.IsCommunicationNotification(n).ShouldBeTrue();
    }

    [Theory]
    [InlineData("msg")]
    [InlineData("email")]
    [InlineData("social")]
    public void IsCommunicationNotification_WhenCategoryIsCommunication_ReturnsTrue(string category)
    {
        var n = MakeWith(package: "com.random.app", androidCategory: category);
        CommunicationAppDetector.IsCommunicationNotification(n).ShouldBeTrue();
    }

    [Fact]
    public void IsCommunicationNotification_WhenConversationTitleIsPresent_ReturnsTrue()
    {
        var n = MakeWith(package: "com.random.app", conversationTitle: "Family Group");
        CommunicationAppDetector.IsCommunicationNotification(n).ShouldBeTrue();
    }

    [Fact]
    public void IsCommunicationNotification_ForRegularAppNotification_ReturnsFalse()
    {
        var n = MakeWith(package: "com.digikala", androidCategory: "promo", conversationTitle: null);
        CommunicationAppDetector.IsCommunicationNotification(n).ShouldBeFalse();
    }
}
