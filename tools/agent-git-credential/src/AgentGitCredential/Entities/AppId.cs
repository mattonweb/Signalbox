namespace AgentGitCredential.Entities;

using System;
using System.Globalization;

/// <summary>
/// The numeric identifier GitHub assigns to a GitHub App, shown on the app's settings page.
/// </summary>
public readonly record struct AppId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppId"/> class.
    /// </summary>
    /// <param name="value">The identifier; positive.</param>
    public AppId(long value)
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
