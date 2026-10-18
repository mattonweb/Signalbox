namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="JsonWebTokenBuilder"/>.</summary>
[TestClass]
public sealed class JsonWebTokenBuilderTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The token has three dot-separated parts and the signature verifies with the public key.</summary>
    [TestMethod]
    public void Build_ValidKey_SignatureVerifies()
    {
        PrivateKeyPem key = TestKeys.NewPrivateKey();
        JsonWebTokenBuilder builder = new JsonWebTokenBuilder(new FixedTimeProvider(Now));

        string token = builder.Build(new AppId(12345), key);

        string[] parts = token.Split('.');
        Assert.HasCount(3, parts);
        Assert.IsTrue(Verify(key, parts[0] + "." + parts[1], parts[2]));
    }

    /// <summary>The claims carry the app id as issuer, issue one minute back and expiry nine minutes ahead.</summary>
    [TestMethod]
    public void Build_Claims_IssuerAndWindowAreAsSpecified()
    {
        JsonWebTokenBuilder builder = new JsonWebTokenBuilder(new FixedTimeProvider(Now));

        string token = builder.Build(new AppId(12345), TestKeys.NewPrivateKey());

        string[] parts = token.Split('.');
        using (JsonDocument claims = JsonDocument.Parse(FromBase64Url(parts[1])))
        {
            JsonElement root = claims.RootElement;
            Assert.AreEqual("12345", root.GetProperty("iss").GetString());
            Assert.AreEqual(Now.AddMinutes(-1).ToUnixTimeSeconds(), root.GetProperty("iat").GetInt64());
            Assert.AreEqual(Now.AddMinutes(9).ToUnixTimeSeconds(), root.GetProperty("exp").GetInt64());
        }
    }

    private static bool Verify(PrivateKeyPem key, string signingInput, string signature)
    {
        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(key.Value);
            return rsa.VerifyData(Encoding.ASCII.GetBytes(signingInput), FromBase64Url(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+');
        padded = padded.Replace('_', '/');
        int remainder = padded.Length % 4;
        if (remainder > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - remainder), '=');
        }

        return Convert.FromBase64String(padded);
    }
}
