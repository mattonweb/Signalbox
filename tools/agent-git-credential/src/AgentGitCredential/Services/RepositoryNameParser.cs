namespace AgentGitCredential.Services;

using System;
using AgentGitCredential.Entities;

/// <summary>
/// Reads a <see cref="RepositoryName"/> out of the forms it arrives in: an <c>owner/name</c> slug, the path git
/// passes in a credential request, or a remote address.
/// </summary>
public static class RepositoryNameParser
{
    private const string GitSuffix = ".git";

    /// <summary>
    /// Parses <c>owner/name</c>.
    /// </summary>
    /// <param name="slug">The slug.</param>
    /// <returns>The repository name.</returns>
    public static RepositoryName ParseSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        string trimmed = slug.Trim().Trim('/');
        if (trimmed.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - GitSuffix.Length);
        }

        string[] parts = trimmed.Split('/');
        if (parts.Length != 2)
        {
            throw new FormatException("A repository is written owner/name; got '" + slug + "'.");
        }

        return new RepositoryName(parts[0], parts[1]);
    }

    /// <summary>
    /// Parses the <c>path</c> field of a git credential request, which is <c>owner/name.git</c> when
    /// <c>credential.useHttpPath</c> is on.
    /// </summary>
    /// <param name="path">The path field.</param>
    /// <returns>The repository name, or <see langword="null"/> when the path is absent or not a repository path.</returns>
    public static RepositoryName? TryParseCredentialPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return ParseSlug(path);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a remote address in either form git prints for GitHub:
    /// <c>https://github.com/owner/name.git</c> or <c>git@github.com:owner/name.git</c>.
    /// </summary>
    /// <param name="remoteUrl">The remote address.</param>
    /// <returns>The repository name.</returns>
    public static RepositoryName ParseRemoteUrl(string remoteUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        string trimmed = remoteUrl.Trim();
        int colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase) && colon > 0)
        {
            return ParseSlug(trimmed.Substring(colon + 1));
        }

        Uri uri = new Uri(trimmed, UriKind.Absolute);
        return ParseSlug(uri.AbsolutePath);
    }
}
