namespace AgentGitCredential.DTOs;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// The shape of a cached token file before it is protected: the token and its expiry.
/// </summary>
internal sealed record CachedToken
{
    /// <summary>Gets the token.</summary>
    [JsonPropertyName("token")]
    required public string Token { get; init; }

    /// <summary>Gets the expiry instant.</summary>
    [JsonPropertyName("expiresAt")]
    required public DateTimeOffset ExpiresAt { get; init; }
}
