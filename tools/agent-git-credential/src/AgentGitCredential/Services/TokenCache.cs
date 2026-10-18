namespace AgentGitCredential.Services;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using AgentGitCredential.DTOs;
using AgentGitCredential.Entities;

/// <summary>
/// Keeps one token file per repository under the token directory, protected for the current Windows user, so
/// a token is minted once an hour rather than once a command. A token with less than the margin left is
/// treated as absent.
/// </summary>
public sealed class TokenCache
{
    private static readonly TimeSpan FreshnessMargin = TimeSpan.FromMinutes(5);

    private readonly StoreLocation _location;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenCache"/> class.
    /// </summary>
    /// <param name="location">Where the token directory lives.</param>
    /// <param name="timeProvider">The clock.</param>
    public TokenCache(StoreLocation location, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this._location = location;
        this._timeProvider = timeProvider;
    }

    /// <summary>
    /// Loads a repository's cached token when one exists and will stay valid past the margin.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <returns>The token, or <see langword="null"/> when there is none worth using.</returns>
    public InstallationToken? Load(RepositoryName repository)
    {
        string path = this.PathFor(repository);
        if (!File.Exists(path))
        {
            return null;
        }

        CachedToken? cached = ReadFile(path);
        if (cached is null)
        {
            return null;
        }

        DateTimeOffset deadline = this._timeProvider.GetUtcNow().Add(FreshnessMargin);
        return cached.ExpiresAt > deadline ? new InstallationToken(cached.Token, cached.ExpiresAt) : null;
    }

    /// <summary>
    /// Stores a repository's token, replacing any earlier one.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="token">The token.</param>
    public void Save(RepositoryName repository, InstallationToken token)
    {
        Directory.CreateDirectory(this._location.TokenCacheDirectory);
        CachedToken cached = new CachedToken { Token = token.Value, ExpiresAt = token.ExpiresAt };
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(cached);
        byte[] protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(this.PathFor(repository), protectedBytes);
    }

    private static CachedToken? ReadFile(string path)
    {
        try
        {
            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<CachedToken>(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private string PathFor(RepositoryName repository)
    {
        return Path.Combine(this._location.TokenCacheDirectory, repository.Owner + "--" + repository.Name + ".token");
    }
}
