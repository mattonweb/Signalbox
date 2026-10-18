namespace AgentGitCredential.Test.Unit.TestDoubles;

using System.Security.Cryptography;
using AgentGitCredential.Entities;

/// <summary>
/// Generates throwaway RSA keys in the PEM form GitHub downloads.
/// </summary>
public static class TestKeys
{
    /// <summary>
    /// Creates a new 2048-bit key.
    /// </summary>
    /// <returns>The key in PEM form.</returns>
    public static PrivateKeyPem NewPrivateKey()
    {
        using (RSA rsa = RSA.Create(2048))
        {
            return new PrivateKeyPem(rsa.ExportRSAPrivateKeyPem());
        }
    }
}
