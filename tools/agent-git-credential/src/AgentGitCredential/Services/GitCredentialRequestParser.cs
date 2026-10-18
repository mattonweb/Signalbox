namespace AgentGitCredential.Services;

using System;
using System.Collections.Generic;
using System.IO;
using AgentGitCredential.DTOs;

/// <summary>
/// Reads the <c>key=value</c> lines git writes to a credential helper's standard input, up to the blank line
/// that ends them.
/// </summary>
internal static class GitCredentialRequestParser
{
    /// <summary>
    /// Parses a request.
    /// </summary>
    /// <param name="input">The helper's standard input.</param>
    /// <returns>The request.</returns>
    public static GitCredentialRequest Parse(TextReader input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Dictionary<string, string> fields = ReadFields(input);
        return new GitCredentialRequest
        {
            Protocol = fields.GetValueOrDefault("protocol", string.Empty),
            Host = fields.GetValueOrDefault("host", string.Empty),
            Path = fields.GetValueOrDefault("path"),
        };
    }

    private static Dictionary<string, string> ReadFields(TextReader input)
    {
        Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? line = input.ReadLine();
        while (!string.IsNullOrEmpty(line))
        {
            AddField(fields, line);
            line = input.ReadLine();
        }

        return fields;
    }

    private static void AddField(Dictionary<string, string> fields, string line)
    {
        int separator = line.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return;
        }

        fields[line.Substring(0, separator)] = line.Substring(separator + 1);
    }
}
