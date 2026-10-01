using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class UsernameBuilderTests
{
    [Fact]
    public void Build_WithToken_UsesUserTokenHostFormat() =>
        Assert.Equal("jdoe#123456#srv01.example.com", UsernameBuilder.Build("jdoe", "123456", "srv01.example.com"));

    [Fact]
    public void Build_TrimsWhitespace() =>
        Assert.Equal("jdoe#42#srv01", UsernameBuilder.Build("  jdoe ", " 42 ", " srv01 "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithoutToken_KeepsEmptyTokenSegment(string? token) =>
        Assert.Equal("jdoe##srv01", UsernameBuilder.Build("jdoe", token, "srv01"));

    [Fact]
    public void Build_AllowsDomainUser() =>
        Assert.Equal(@"EXAMPLE\jdoe##srv01", UsernameBuilder.Build(@"EXAMPLE\jdoe", null, "srv01"));

    [Theory]
    [InlineData("jd#oe", "1", "srv")]
    [InlineData("jdoe", "1#2", "srv")]
    [InlineData("jdoe", "1", "sr#v")]
    [InlineData("jd oe", "1", "srv")]
    [InlineData("jdoe", "1", "s rv")]
    [InlineData("", "1", "srv")]
    [InlineData("jdoe", "1", "")]
    public void Build_RejectsInvalidParts(string user, string token, string host) =>
        Assert.Throws<ArgumentException>(() => UsernameBuilder.Build(user, token, host));
}
