namespace AgentGitCredential.Entities;

using System;

/// <summary>
/// The RSA private key GitHub generates for a GitHub App, in PEM form as downloaded. It never leaves the
/// process and is never written anywhere but the KeePass database.
/// </summary>
public readonly record struct PrivateKeyPem
{
    private const string Marker = "PRIVATE KEY";

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateKeyPem"/> class.
    /// </summary>
    /// <param name="value">The PEM text, including the BEGIN and END lines.</param>
    public PrivateKeyPem(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.Contains(Marker, StringComparison.Ordinal))
        {
            throw new ArgumentException("The value is not a PEM private key.", nameof(value));
        }

        this.Value = value;
    }

    /// <summary>Gets the PEM text.</summary>
    public string Value { get; }

    /// <summary>Returns a fixed marker rather than the key, so a stray print cannot leak it.</summary>
    /// <returns>The text <c>[private key]</c>.</returns>
    public override string ToString()
    {
        return "[private key]";
    }
}
