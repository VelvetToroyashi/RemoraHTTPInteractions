using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using NSec.Cryptography;

namespace RemoraHTTPInteractions.Services;

/// <summary>
/// A helper class for validating Discord request signatures.a
/// </summary>
public static class DiscordHeaders
{
    /// <summary>
    /// Extracts the requisite headers from a request (X-Signature-Timestamp and X-Signature-Ed25519).
    /// </summary>
    /// <param name="headers">The headers to extract from.</param>
    /// <param name="Timestamp">The extracted timestamp.</param>
    /// <param name="Key">The extracted public key.</param>
    /// <returns>True if the headers were successfully extracted, otherwise false.</returns>
    public static bool ExtractHeaders(IHeaderDictionary headers, out string? Timestamp, out string? Key)
    {
        Timestamp = null;
        Key = null;
        if (headers.TryGetValue("X-Signature-Timestamp", out var timestamp))
        {
            Timestamp = timestamp;
        }
        if (headers.TryGetValue("X-Signature-Ed25519", out var key))
        {
            Key = key;
        }
        return Timestamp != null && Key != null;
    }
    
    /// <summary>
    /// Verifies the signature of a request. If this fails, the request is invalid and should be rejected with a 401 status code.
    /// </summary>
    /// <param name="body">The body of the request.</param>
    /// <param name="timestamp">The timestamp of the request.</param>
    /// <param name="signingKey">The signing key from the request.</param>
    /// <param name="publicKey">The application's public key.</param>
    /// <returns>True if the request is valid, otherwise false.</returns>
    public static bool VerifySignature(string body, string timestamp, string signingKey, string publicKey)
    {
        var bytes = Encoding.UTF8.GetBytes(timestamp + body);
        var keyBytes = Encoding.UTF8.GetBytes(publicKey);
        var signBytes = Encoding.UTF8.GetBytes(signingKey);
        return Ed25519.Ed25519.Verify(PublicKey.Import(SignatureAlgorithm.Ed25519, keyBytes, KeyBlobFormat.NSecPublicKey), bytes, signBytes);
    }
    
}
