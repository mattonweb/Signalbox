namespace AgentGitCredential.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// The claims of the JSON Web Token a GitHub App presents to mint an installation token: issuer (the app
/// identifier), issued-at and expiry as Unix seconds.
/// </summary>
internal sealed record JsonWebTokenClaims
{
    /// <summary>Gets the issuer: the app identifier as digits.</summary>
    [JsonPropertyName("iss")]
    required public string Issuer { get; init; }

    /// <summary>Gets the issued-at instant in Unix seconds, set slightly in the past to absorb clock drift.</summary>
    [JsonPropertyName("iat")]
    required public long IssuedAt { get; init; }

    /// <summary>Gets the expiry instant in Unix seconds; GitHub allows at most ten minutes after issue.</summary>
    [JsonPropertyName("exp")]
    required public long ExpiresAt { get; init; }
}
