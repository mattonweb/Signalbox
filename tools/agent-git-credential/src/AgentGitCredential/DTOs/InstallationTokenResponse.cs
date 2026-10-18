namespace AgentGitCredential.DTOs;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// The body GitHub returns from <c>POST /app/installations/{id}/access_tokens</c>, reduced to the two fields
/// the helper uses.
/// </summary>
internal sealed record InstallationTokenResponse
{
    /// <summary>Gets the token.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>Gets the expiry instant.</summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }
}
