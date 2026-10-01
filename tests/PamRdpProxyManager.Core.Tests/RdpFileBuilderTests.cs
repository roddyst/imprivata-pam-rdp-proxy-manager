using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class RdpFileBuilderTests
{
    private static Dictionary<string, string> Parse(string content) =>
        content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(':', 3))
            .ToDictionary(p => p[0], p => p[2]);

    [Theory]
    [InlineData(3388)]
    [InlineData(3389)]
    [InlineData(4000)]
    public void Build_AlwaysWritesExplicitPort(int port)
    {
        var rdp = Parse(RdpFileBuilder.Build("pam.example.com", port, new RdpOptions()));
        Assert.Equal($"pam.example.com:{port}", rdp["full address"]);
        Assert.Equal("0", rdp["prompt for credentials"]);
    }

    [Fact]
    public void Build_NeverContainsCredentials()
    {
        var options = new RdpOptions { AdditionalSettings = "password 51:b:ABCDEF\nusername:s:evil\nfull address:s:other.example.com\nkeyboardhook:i:2" };
        var content = RdpFileBuilder.Build("pam.example.com", 3388, options);

        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("other.example.com", content);
        Assert.Contains("keyboardhook:i:2\r\n", content);
    }

    [Fact]
    public void Build_MapsOptions()
    {
        var options = new RdpOptions
        {
            DisplayMode = DisplayMode.Windowed,
            DesktopWidth = 1280,
            DesktopHeight = 800,
            UseMultiMonitor = true,
            ColorDepth = 16,
            RedirectClipboard = false,
            RedirectDrives = true,
            RedirectPrinters = true,
            AudioMode = AudioMode.DoNotPlay,
            EnableNla = false,
            AuthenticationLevel = ServerAuthenticationLevel.DoNotConnect,
        };

        var rdp = Parse(RdpFileBuilder.Build("pam.example.com", 3388, options));

        Assert.Equal("1", rdp["screen mode id"]);
        Assert.Equal("1280", rdp["desktopwidth"]);
        Assert.Equal("800", rdp["desktopheight"]);
        Assert.Equal("1", rdp["use multimon"]);
        Assert.Equal("16", rdp["session bpp"]);
        Assert.Equal("0", rdp["redirectclipboard"]);
        Assert.Equal("*", rdp["drivestoredirect"]);
        Assert.Equal("1", rdp["redirectprinters"]);
        Assert.Equal("2", rdp["audiomode"]);
        Assert.Equal("0", rdp["enablecredsspsupport"]);
        Assert.Equal("1", rdp["authentication level"]);
    }

    [Fact]
    public void Build_Fullscreen_OmitsResolution()
    {
        var rdp = Parse(RdpFileBuilder.Build("pam.example.com", 3388, new RdpOptions { DisplayMode = DisplayMode.Fullscreen }));
        Assert.Equal("2", rdp["screen mode id"]);
        Assert.False(rdp.ContainsKey("desktopwidth"));
    }

    [Fact]
    public void Build_AdditionalSettingOverridesGeneratedValue()
    {
        var content = RdpFileBuilder.Build("pam.example.com", 3388, new RdpOptions { AdditionalSettings = "session bpp:i:24" });
        Assert.Single(content.Split("\r\n"), l => l.StartsWith("session bpp:", StringComparison.Ordinal));
        Assert.Equal("24", Parse(content)["session bpp"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void Build_InvalidPort_Throws(int port) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RdpFileBuilder.Build("pam.example.com", port, new RdpOptions()));

    [Theory]
    [InlineData("server01.example.com", "server01.example.com.rdp")]
    [InlineData("  server01  ", "server01.rdp")]
    [InlineData("srv:3389", "srv_3389.rdp")]
    [InlineData("a/b\\c*?", "a_b_c__.rdp")]
    [InlineData("server.", "server.rdp")]
    [InlineData("CON", "_CON.rdp")]
    [InlineData("", "Remotedesktop.rdp")]
    [InlineData(null, "Remotedesktop.rdp")]
    public void FileNameFor_ProducesWindowsSafeNames(string? host, string expected)
    {
        Assert.Equal(expected, RdpFileBuilder.FileNameFor(host));
    }

    [Fact]
    public void FileNameFor_LimitsLength()
    {
        Assert.Equal(104, RdpFileBuilder.FileNameFor(new string('a', 300)).Length);
    }
}
