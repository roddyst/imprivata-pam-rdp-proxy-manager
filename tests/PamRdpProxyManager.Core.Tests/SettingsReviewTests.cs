using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class SettingsReviewTests
{
    private static AppSettings With(params ConnectionProfile[] profiles) => new() { Profiles = [.. profiles] };

    [Fact]
    public void Describe_ShowsParsedServerInsteadOfRawText()
    {
        var text = SettingsReview.Describe(With(
            new ConnectionProfile { Name = "Spoof", ProxyHost = "evil.example.com#@pam.example.com" },
            new ConnectionProfile { Name = "Lookalike", ProxyHost = "pаm.example.com", Port = 3390 }));

        Assert.Contains("Spoof: evil.example.com:3388", text);
        Assert.DoesNotContain("@pam.example.com", text);
        Assert.Contains("Lookalike: xn--pm-7kc.example.com:3390", text);
    }

    [Fact]
    public void Describe_HiddenLinesCannotFakeAnotherServer()
    {
        var text = SettingsReview.Describe(With(
            new ConnectionProfile { Name = "Standard\n  •  Firma", ProxyHost = "pam.example.com\n  •  Standard: pam.corp.example:3388" }));

        Assert.Single(text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("ungültige Adresse", text);
        Assert.DoesNotContain("pam.corp.example", text);
    }

    [Fact]
    public void Describe_ListsSecurityRelevantOptions()
    {
        var text = SettingsReview.Describe(With(new ConnectionProfile
        {
            Name = "P",
            ProxyHost = "pam.example.com",
            Rdp = new RdpOptions { RedirectDrives = true, EnableNla = false, AdditionalSettings = "usbdevicestoredirect:s:*\nusername:s:blocked" },
        }));

        Assert.Contains("Laufwerke werden umgeleitet", text);
        Assert.Contains("NLA ausgeschaltet", text);
        Assert.Contains("zusätzlich: usbdevicestoredirect:s:*", text);
        Assert.DoesNotContain("username", text); // never written to the .rdp file, so not shown either
    }

    [Theory]
    [InlineData("Prod‮txt.exe", "Prodtxt.exe")]
    [InlineData("  a\r\nb  ", "ab")]
    [InlineData(null, "")]
    public void Sanitize_RemovesInvisibleAndControlCharacters(string? input, string expected) =>
        Assert.Equal(expected, SettingsReview.Sanitize(input));

    [Fact]
    public void Sanitize_ShortensLongValues() =>
        Assert.Equal(81, SettingsReview.Sanitize(new string('x', 500)).Length);
}
