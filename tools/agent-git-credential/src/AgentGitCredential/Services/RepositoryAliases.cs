namespace AgentGitCredential.Services;

using System;
using System.Collections.Generic;
using AgentGitCredential.Entities;

/// <summary>
/// Maps a repository, as a request or a remote names it, to the registration that answers for it. A session
/// sets the map in <c>AGENT_GIT_REPOSITORY_ALIASES</c> as <c>owner/name=owner/name#key;owner/other=…</c>, so
/// one repository can resolve to a different app per session: the store keeps one entry per title, and a
/// title with a <c>#key</c> suffix is a second entry for the same repository.
/// </summary>
public sealed class RepositoryAliases
{
    /// <summary>The environment variable that carries the map.</summary>
    public const string VariableName = "AGENT_GIT_REPOSITORY_ALIASES";

    private const char PairSeparator = ';';
    private const char KeyValueSeparator = '=';

    private readonly IReadOnlyDictionary<RepositoryName, RepositoryName> _targets;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryAliases"/> class.
    /// </summary>
    /// <param name="targets">The repository each aliased repository resolves to.</param>
    public RepositoryAliases(IReadOnlyDictionary<RepositoryName, RepositoryName> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        this._targets = targets;
    }

    /// <summary>Gets the map with no aliases, under which every repository resolves to itself.</summary>
    public static RepositoryAliases None { get; } = new RepositoryAliases(new Dictionary<RepositoryName, RepositoryName>());

    /// <summary>
    /// Parses the map from its environment form. An absent or blank value is the empty map.
    /// </summary>
    /// <param name="value">The variable's value, or <see langword="null"/> when it is not set.</param>
    /// <returns>The map.</returns>
    public static RepositoryAliases Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return None;
        }

        Dictionary<RepositoryName, RepositoryName> targets = new Dictionary<RepositoryName, RepositoryName>();
        foreach (string pair in value.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            KeyValuePair<RepositoryName, RepositoryName> alias = ParsePair(pair);
            if (!targets.TryAdd(alias.Key, alias.Value))
            {
                throw new FormatException("The repository " + alias.Key.ToString() + " is aliased twice in " + VariableName + ".");
            }
        }

        return new RepositoryAliases(targets);
    }

    /// <summary>
    /// Resolves a repository to the one whose registration answers for it.
    /// </summary>
    /// <param name="repository">The repository as named by the request or the remote.</param>
    /// <returns>The aliased repository, or the same one when it has no alias.</returns>
    public RepositoryName Resolve(RepositoryName repository)
    {
        return this._targets.TryGetValue(repository, out RepositoryName target) ? target : repository;
    }

    private static KeyValuePair<RepositoryName, RepositoryName> ParsePair(string pair)
    {
        string[] parts = pair.Split(KeyValueSeparator);
        if (parts.Length != 2)
        {
            throw new FormatException("An alias is written owner/name=owner/name#key; got '" + pair + "' in " + VariableName + ".");
        }

        return new KeyValuePair<RepositoryName, RepositoryName>(RepositoryNameParser.ParseSlug(parts[0]), RepositoryNameParser.ParseSlug(parts[1]));
    }
}
