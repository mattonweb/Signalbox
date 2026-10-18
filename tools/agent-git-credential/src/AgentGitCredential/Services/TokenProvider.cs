namespace AgentGitCredential.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using AgentGitCredential.Entities;

/// <summary>
/// Answers "a token for this repository" from the cache when it can and from GitHub when it must.
/// </summary>
public sealed class TokenProvider
{
    private readonly RegistrationStore _registrations;
    private readonly TokenCache _cache;
    private readonly IInstallationTokenClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenProvider"/> class.
    /// </summary>
    /// <param name="registrations">The app registrations.</param>
    /// <param name="cache">The token cache.</param>
    /// <param name="client">Mints tokens.</param>
    public TokenProvider(RegistrationStore registrations, TokenCache cache, IInstallationTokenClient client)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(client);
        this._registrations = registrations;
        this._cache = cache;
        this._client = client;
    }

    /// <summary>
    /// Gets a usable token for a repository.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="cancellationToken">Cancels a mint.</param>
    /// <returns>The token, or <see langword="null"/> when no app is registered for the repository.</returns>
    public async Task<InstallationToken?> GetTokenAsync(RepositoryName repository, CancellationToken cancellationToken = default)
    {
        InstallationToken? cached = this._cache.Load(repository);
        if (cached is not null)
        {
            return cached;
        }

        AppRegistration? registration = this._registrations.Find(repository);
        if (registration is null)
        {
            return null;
        }

        InstallationToken minted = await this._client.MintAsync(registration, cancellationToken).ConfigureAwait(false);
        this._cache.Save(repository, minted);
        return minted;
    }
}
