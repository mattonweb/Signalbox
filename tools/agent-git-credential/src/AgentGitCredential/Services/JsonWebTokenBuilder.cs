namespace AgentGitCredential.Services;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentGitCredential.DTOs;
using AgentGitCredential.Entities;

/// <summary>
/// Builds the short-lived, RS256-signed JSON Web Token a GitHub App presents to the API. GitHub accepts an
/// expiry at most ten minutes after issue; this builder issues one minute in the past and expires nine minutes
/// in the future, so a clock a little ahead or behind GitHub's still passes.
/// </summary>
public sealed class JsonWebTokenBuilder
{
    private const string Header = "{\"alg\":\"RS256\",\"typ\":\"JWT\"}";
    private static readonly TimeSpan IssuedBackdate = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(9);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebTokenBuilder"/> class.
    /// </summary>
    /// <param name="timeProvider">The clock.</param>
    public JsonWebTokenBuilder(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

    /// <summary>
    /// Builds a token for an app.
    /// </summary>
    /// <param name="appId">The app.</param>
    /// <param name="privateKey">The app's private key.</param>
    /// <returns>The compact serialization: header, claims and signature, base64url, dot-separated.</returns>
    public string Build(AppId appId, PrivateKeyPem privateKey)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        JsonWebTokenClaims claims = new JsonWebTokenClaims
        {
            Issuer = appId.ToString(),
            IssuedAt = now.Subtract(IssuedBackdate).ToUnixTimeSeconds(),
            ExpiresAt = now.Add(Lifetime).ToUnixTimeSeconds(),
        };
        string signingInput = Base64Url(Encoding.UTF8.GetBytes(Header)) + "." + Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        byte[] signature = Sign(signingInput, privateKey);
        return signingInput + "." + Base64Url(signature);
    }

    private static byte[] Sign(string signingInput, PrivateKeyPem privateKey)
    {
        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(privateKey.Value);
#pragma warning disable S5542 // RS256 is RSASSA-PKCS1-v1_5 by the JSON Web Algorithms specification, and GitHub accepts no other algorithm for app tokens.
            return rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
#pragma warning restore S5542
        }
    }

    private static string Base64Url(byte[] bytes)
    {
        string base64 = Convert.ToBase64String(bytes);
        base64 = base64.TrimEnd('=');
        base64 = base64.Replace('+', '-');
        return base64.Replace('/', '_');
    }
}
