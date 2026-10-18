namespace AgentGitCredential.Entities;

using System;

/// <summary>
/// A GitHub App installation access token and the instant it expires. Tokens live about an hour.
/// </summary>
public readonly record struct InstallationToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstallationToken"/> class.
    /// </summary>
    /// <param name="value">The token text GitHub returned.</param>
    /// <param name="expiresAt">The expiry instant GitHub returned.</param>
    public InstallationToken(string value, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        this.Value = value;
        this.ExpiresAt = expiresAt;
    }

    /// <summary>Gets the token text.</summary>
    public string Value { get; }

    /// <summary>Gets the expiry instant.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Returns a fixed marker rather than the token, so a stray print cannot leak it.</summary>
    /// <returns>The text <c>[token]</c>.</returns>
    public override string ToString()
    {
        return "[token]";
    }
}
