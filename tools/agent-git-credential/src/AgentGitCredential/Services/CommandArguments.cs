namespace AgentGitCredential.Services;

using System;
using System.Collections.Generic;

/// <summary>
/// The <c>--name value</c> pairs after a command verb.
/// </summary>
public sealed class CommandArguments
{
    private const string Prefix = "--";

    private readonly IReadOnlyDictionary<string, string> _values;

    private CommandArguments(IReadOnlyDictionary<string, string> values)
    {
        this._values = values;
    }

    /// <summary>
    /// Parses the arguments that follow the verb.
    /// </summary>
    /// <param name="arguments">The full command line, verb first.</param>
    /// <returns>The pairs.</returns>
    public static CommandArguments Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index + 1 < arguments.Length; index += 2)
        {
            AddPair(values, arguments[index], arguments[index + 1]);
        }

        return new CommandArguments(values);
    }

    /// <summary>
    /// Gets a required value.
    /// </summary>
    /// <param name="name">The option name without the dashes.</param>
    /// <returns>The value.</returns>
    public string Require(string name)
    {
        if (!this._values.TryGetValue(name, out string? value))
        {
            throw new ArgumentException("Missing --" + name + ".");
        }

        return value;
    }

    private static void AddPair(Dictionary<string, string> values, string name, string value)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected an option like --repository, got '" + name + "'.");
        }

        values[name.Substring(Prefix.Length)] = value;
    }
}
