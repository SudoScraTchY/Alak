using Alak.Core.Linking;

namespace Alak.Core.Tests;

public class PackageNameHumanizerTests
{
    [Theory]
    [InlineData("com.digikala", "Digikala")]
    [InlineData("com.mydigipay.app.android", "Mydigipay")]
    [InlineData("com.google.android.apps.messaging", "Messages")]
    [InlineData("com.android.mms", "Messages")]
    [InlineData("com.valvesoftware.android.steam.community", "Valvesoftware")]
    [InlineData("ir.snapp.taxi", "Snapp")]
    [InlineData("com.my_app", "My App")]
    public void HumanizesPackageSegments(string package, string expected)
    {
        PackageNameHumanizer.Humanize(package).ShouldBe(expected);
    }

    [Fact]
    public void EmptyInputReturnsEmpty()
    {
        PackageNameHumanizer.Humanize("").ShouldBeEmpty();
        PackageNameHumanizer.Humanize(null).ShouldBeEmpty();
    }
}
