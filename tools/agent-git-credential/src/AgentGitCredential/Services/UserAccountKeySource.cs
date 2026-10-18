namespace AgentGitCredential.Services;

using System;
using System.IO;
using System.Security.Cryptography;
using AgentGitCredential.Entities;
using KeePassLib.Keys;

/// <summary>
/// Keys the database to the current Windows user account: a random secret, generated once, protected with
/// Windows data protection for this account and kept beside the database. The database then opens only for
/// this account on this machine, with no master password. (KeePass's own user-account key source is not used
/// because the cross-platform KeePassLib build backs it with per-process keys, which a second process cannot
/// reproduce.)
/// </summary>
public sealed class UserAccountKeySource
    : IDatabaseKeySource
{
    private const string KeyName = "agent-git-credential-user-account";
    private const int SecretLength = 64;
    private static readonly byte[] Entropy = [0x53, 0x69, 0x67, 0x6E, 0x61, 0x6C, 0x62, 0x6F, 0x78, 0x2D, 0x61, 0x67, 0x63];

    private readonly StoreLocation _location;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccountKeySource"/> class.
    /// </summary>
    /// <param name="location">Where the protected secret lives.</param>
    public UserAccountKeySource(StoreLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        this._location = location;
    }

    /// <inheritdoc/>
    public CompositeKey CreateKey()
    {
        byte[] secret = this.LoadOrCreateSecret();
        CompositeKey key = new CompositeKey();
        key.AddUserKey(new KcpCustomKey(KeyName, secret, true));
        return key;
    }

    private byte[] LoadOrCreateSecret()
    {
        string path = this._location.UserKeyPath;
        if (File.Exists(path))
        {
            byte[] protectedBytes = File.ReadAllBytes(path);
            return ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        }

        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        byte[] secret = RandomNumberGenerator.GetBytes(SecretLength);
        File.WriteAllBytes(path, ProtectedData.Protect(secret, Entropy, DataProtectionScope.CurrentUser));
        return secret;
    }
}
