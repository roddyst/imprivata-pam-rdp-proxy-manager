using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class ProxyHostParserTests
{
    [Theory]
    [InlineData("pam.example.com", "pam.example.com", null)]
    [InlineData("  pam.example.com  ", "pam.example.com", null)]
    [InlineData("pam.example.com:3390", "pam.example.com", 3390)]
    [InlineData("https://pam.example.com/", "pam.example.com", null)]
    [InlineData("https://pam.example.com:8443/login", "pam.example.com", null)]
    [InlineData("192.0.2.10", "192.0.2.10", null)]
    [InlineData("[2001:db8::1]:3389", "[2001:db8::1]", 3389)]
    public void TryParse_ValidInput(string input, string expectedHost, int? expectedPort)
    {
        Assert.True(ProxyHostParser.TryParse(input, out var host, out var port, out var error), error);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://user:pass@pam.example.com")]
    [InlineData("pam example com")]
    public void TryParse_InvalidInput(string? input) =>
        Assert.False(ProxyHostParser.TryParse(input, out _, out _, out _));
}
