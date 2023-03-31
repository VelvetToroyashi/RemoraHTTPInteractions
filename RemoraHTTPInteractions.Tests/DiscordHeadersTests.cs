using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Chaos.NaCl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RemoraHTTPInteractions.Services;

namespace RemoraHTTPInteractions.Tests;

public class DiscordHeadersTests
{
    private const string Body = """{"some_data": false}""";
    public const string Timestamp = "1680205264";
    public const string BodyAndTimestamp = Timestamp + Body;

    public static readonly byte[] PrivateKey = RandomNumberGenerator.GetBytes(32);
    public static readonly byte[] PublicKey = Ed25519.PublicKeyFromSeed(PrivateKey);
    public static readonly byte[] Signature = Ed25519.Sign(Encoding.UTF8.GetBytes(BodyAndTimestamp), Ed25519.ExpandedPrivateKeyFromSeed(PrivateKey));

    static DiscordHeadersTests()
    {
        _ = PrivateKey;
        _ = PublicKey;
        _ = Signature;
    }
    
    /// <summary>
    /// Tests that the <see cref="DiscordHeaders"/> class can validate a signature.
    /// </summary>
    [Test]
    public void VerifiesPublicKeyCorrectly()
    {
        Assert.True(DiscordHeaders.VerifySignature(Body, Timestamp, Convert.ToHexString(Signature), Convert.ToHexString(PublicKey)));
    }

    /// <summary>
    /// Tests that the <see cref="DiscordHeaders"/> class can extract a signature from a request.
    /// </summary>
    [Test]
    public void ExtractsHeadersCorrectly()
    {
        var headers = new Dictionary<string, StringValues>()
        {
            { "X-Signature-Timestamp", Timestamp },
            { "X-Signature-Ed25519", Convert.ToHexString(Signature) }
        };
        
        var exists = DiscordHeaders.TryExtractHeaders(headers, out var timestamp, out var signature);
        
        Assert.True(exists);
        
        Assert.AreEqual(Timestamp, timestamp);
        Assert.AreEqual(Convert.ToHexString(Signature), signature);
    }
}
