namespace AgentGitCredential.Services;

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentGitCredential.DTOs;
using AgentGitCredential.Entities;

/// <summary>
/// Calls <c>POST https://api.github.com/app/installations/{id}/access_tokens</c> with an app JSON Web Token
/// and returns the installation token GitHub mints.
/// </summary>
public sealed class InstallationTokenClient
    : IInstallationTokenClient
{
    private const string ApiVersion = "2022-11-28";
    private const string UserAgent = "git-credential-github-app";
    private static readonly Uri ApiRoot = new Uri("https://api.github.com/");

    private readonly HttpClient _httpClient;
    private readonly JsonWebTokenBuilder _tokenBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallationTokenClient"/> class.
    /// </summary>
    /// <param name="httpClient">The client; its lifetime belongs to the caller.</param>
    /// <param name="tokenBuilder">Builds the app token that authenticates the request.</param>
    public InstallationTokenClient(HttpClient httpClient, JsonWebTokenBuilder tokenBuilder)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenBuilder);
        this._httpClient = httpClient;
        this._tokenBuilder = tokenBuilder;
    }

    /// <inheritdoc/>
    public async Task<InstallationToken> MintAsync(AppRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        string appToken = this._tokenBuilder.Build(registration.AppId, registration.PrivateKey);
        using (HttpRequestMessage request = BuildRequest(registration.InstallationId, appToken))
        {
            using (HttpResponseMessage response = await this._httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                return await ReadTokenAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static HttpRequestMessage BuildRequest(InstallationId installationId, string appToken)
    {
        Uri address = new Uri(ApiRoot, "app/installations/" + installationId.ToString() + "/access_tokens");
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, address);
        HttpRequestHeaders headers = request.Headers;
        headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
        headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        headers.Add("X-GitHub-Api-Version", ApiVersion);
        headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        return request;
    }

    private static async Task<InstallationToken> ReadTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            throw new InvalidOperationException("GitHub refused the token request (" + status + "): " + body);
        }

        InstallationTokenResponse? payload = await response.Content.ReadFromJsonAsync<InstallationTokenResponse>(cancellationToken).ConfigureAwait(false);
        if (payload?.Token is null || payload.ExpiresAt is null)
        {
            throw new InvalidOperationException("GitHub's token response did not carry a token and an expiry.");
        }

        return new InstallationToken(payload.Token, payload.ExpiresAt.Value);
    }
}
