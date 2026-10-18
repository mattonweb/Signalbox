namespace AgentGitCredential.Entities;

/// <summary>
/// One repository's GitHub App: the app, its installation on the owning account, and the private key that
/// signs the app's requests. One entry in the KeePass database holds one of these.
/// </summary>
public sealed record AppRegistration
{
    /// <summary>Gets the repository the app acts for.</summary>
    required public RepositoryName Repository { get; init; }

    /// <summary>Gets the app identifier.</summary>
    required public AppId AppId { get; init; }

    /// <summary>Gets the installation identifier.</summary>
    required public InstallationId InstallationId { get; init; }

    /// <summary>Gets the app's private key.</summary>
    required public PrivateKeyPem PrivateKey { get; init; }
}
