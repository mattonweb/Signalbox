namespace AgentGitCredential.Services;

using System.Threading;
using System.Threading.Tasks;
using AgentGitCredential.Entities;

/// <summary>
/// Mints an installation access token for a registered app.
/// </summary>
public interface IInstallationTokenClient
{
    /// <summary>
    /// Asks GitHub for a new installation token.
    /// </summary>
    /// <param name="registration">The app and installation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The token.</returns>
    Task<InstallationToken> MintAsync(AppRegistration registration, CancellationToken cancellationToken = default);
}
