using System.Text.RegularExpressions;
using FluentAssertions;
using Soulsjwa.Api.Infrastructure.Auth;
using Xunit;

namespace Soulsjwa.UnitTests;

public partial class ApiKeyAuthHandlerTests
{
    [GeneratedRegex("^sk_[A-Za-z0-9_-]+$")]
    private static partial Regex UrlSafeKeyPattern();

    [Fact]
    public void GenerateApiKey_StartsWithSkPrefix()
    {
        var (fullKey, _) = ApiKeyAuthHandler.GenerateApiKey();
        fullKey.Should().StartWith("sk_");
    }

    [Fact]
    public void GenerateApiKey_PrefixIsEightCharacters()
    {
        var (_, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        prefix.Should().HaveLength(8);
    }

    [Fact]
    public void GenerateApiKey_FullKeyHasCorrectLength()
    {
        var (fullKey, _) = ApiKeyAuthHandler.GenerateApiKey();
        // "sk_" (3) + 32 chars = 35
        fullKey.Should().HaveLength(35);
    }

    [Fact]
    public void GenerateApiKey_FullKeyDoesNotContainUrlUnsafeChars()
    {
        for (var i = 0; i < 20; i++)
        {
            var (fullKey, _) = ApiKeyAuthHandler.GenerateApiKey();
            fullKey.Should().NotContain("+");
            fullKey.Should().NotContain("/");
            fullKey.Should().NotContain("=");
        }
    }

    [Fact]
    public void GenerateApiKey_ProducesUniqueKeys()
    {
        var keys = Enumerable.Range(0, 10)
            .Select(_ => ApiKeyAuthHandler.GenerateApiKey().FullKey)
            .ToHashSet();

        keys.Should().HaveCount(10);
    }

    [Fact]
    public void GenerateApiKey_PrefixMatchesKeyStart()
    {
        var (fullKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        var keyPart = fullKey["sk_".Length..];
        keyPart.Should().StartWith(prefix);
    }

    [Fact]
    public void HashApiKey_DifferentKeysProduceDifferentHashes()
    {
        var hash1 = ApiKeyAuthHandler.HashApiKey("key-one");
        var hash2 = ApiKeyAuthHandler.HashApiKey("key-two");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashApiKey_ProducesBase64Output()
    {
        var hash = ApiKeyAuthHandler.HashApiKey("test-key");

        // SHA256 produces 32 bytes → base64 is 44 characters
        hash.Should().HaveLength(44);
        hash.Should().EndWith("=");
    }

    [Fact]
    public void HashApiKey_EmptyString_DoesNotThrow()
    {
        var act = () => ApiKeyAuthHandler.HashApiKey(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void HashApiKey_ConsistentAcrossMultipleCalls()
    {
        const string key = "sk_ABCDEFGHtest123456789012345678";
        var hashes = Enumerable.Range(0, 5)
            .Select(_ => ApiKeyAuthHandler.HashApiKey(key))
            .Distinct()
            .ToList();

        hashes.Should().HaveCount(1);
    }

    [Fact]
    public void GenerateApiKey_MatchesUrlSafePattern()
    {
        // Base64Url.EncodeToString replaced the hand-rolled +/= substitution —
        // assert the real character set, not just the absence of the three old
        // unsafe characters.
        for (var i = 0; i < 20; i++)
        {
            var (fullKey, _) = ApiKeyAuthHandler.GenerateApiKey();
            UrlSafeKeyPattern().IsMatch(fullKey).Should().BeTrue($"'{fullKey}' should be sk_ followed only by URL-safe base64 characters");
        }
    }

    [Fact]
    public void HashApiKey_PreExistingHash_StillValidates()
    {
        // The Base64Url change touched key/token *generation* only; HashApiKey
        // (Convert.ToBase64String(SHA256.HashData(...))) is untouched, so a
        // pre-change hash must still match today's output — otherwise every
        // already-issued API key would stop authenticating.
        const string key = "sk_fixedTestKeyForRegressionCheck0001";
        const string expectedHash = "mhPZrkvKys108v1OqNsk9YOXZ+zXUDE9yfJTyCVv3P4=";

        ApiKeyAuthHandler.HashApiKey(key).Should().Be(expectedHash);
    }
}
