namespace AgentGitCredential.Entities;

/// <summary>
/// Where the helper keeps its state on this machine: the KeePass database of app registrations, the protected
/// secret that keys it, and the directory of cached tokens. All three are per Windows user.
/// </summary>
public sealed record StoreLocation
{
    /// <summary>Gets the full path of the KeePass database file.</summary>
    required public string DatabasePath { get; init; }

    /// <summary>Gets the full path of the file holding the database key, protected for the current user.</summary>
    required public string UserKeyPath { get; init; }

    /// <summary>Gets the directory that holds one cached token file per repository.</summary>
    required public string TokenCacheDirectory { get; init; }
}
