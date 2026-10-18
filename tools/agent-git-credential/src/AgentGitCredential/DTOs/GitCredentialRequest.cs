namespace AgentGitCredential.DTOs;

/// <summary>
/// The fields git passes to a credential helper on standard input, one <c>key=value</c> per line, ended by a
/// blank line. Only the three the helper reads are kept.
/// </summary>
internal sealed record GitCredentialRequest
{
    /// <summary>Gets the protocol, normally <c>https</c>.</summary>
    required public string Protocol { get; init; }

    /// <summary>Gets the host, normally <c>github.com</c>.</summary>
    required public string Host { get; init; }

    /// <summary>Gets the path, present only when <c>credential.useHttpPath</c> is on; <c>owner/name.git</c>.</summary>
    public string? Path { get; init; }
}
