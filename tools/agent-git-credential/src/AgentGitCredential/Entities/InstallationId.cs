namespace AgentGitCredential.Entities;

using System;
using System.Globalization;

/// <summary>
/// The numeric identifier of a GitHub App installation on an account, found in the installation's settings
/// address (<c>/settings/installations/{id}</c>).
/// </summary>
public readonly record struct InstallationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstallationId"/> class.
    /// </summary>
    /// <param name="value">The identifier; positive.</param>
    public InstallationId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        this.Value = value;
    }

    /// <summary>Gets the identifier.</summary>
    public long Value { get; }

    /// <summary>Returns the identifier as decimal digits.</summary>
    /// <returns>The digits.</returns>
    public override string ToString()
    {
        return this.Value.ToString(CultureInfo.InvariantCulture);
    }
}
