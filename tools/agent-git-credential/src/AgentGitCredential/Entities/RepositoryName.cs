namespace AgentGitCredential.Entities;

using System;

/// <summary>
/// A GitHub repository identified by its owner and name, as in <c>owner/name</c>.
/// </summary>
public readonly record struct RepositoryName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryName"/> class.
    /// </summary>
    /// <param name="owner">The account or organization that owns the repository.</param>
    /// <param name="name">The repository name without the <c>.git</c> suffix.</param>
    public RepositoryName(string owner, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.Owner = owner;
        this.Name = name;
    }

    /// <summary>Gets the account or organization that owns the repository.</summary>
    public string Owner { get; }

    /// <summary>Gets the repository name.</summary>
    public string Name { get; }

    /// <summary>Returns the repository as <c>owner/name</c>.</summary>
    /// <returns>The slug.</returns>
    public override string ToString()
    {
        return this.Owner + "/" + this.Name;
    }
}
