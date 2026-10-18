namespace AgentGitCredential.Services;

using System;
using System.IO;
using AgentGitCredential.Entities;

/// <summary>
/// Resolves where the helper keeps its state for the current Windows user.
/// </summary>
public static class StoreLocations
{
    private const string FolderName = "Signalbox";
    private const string ToolFolderName = "agent-git-credential";
    private const string DatabaseFileName = "github-apps.kdbx";
    private const string TokenFolderName = "tokens";
    private const string UserKeyFileName = "user-account.key";

    /// <summary>
    /// The default location: <c>%LOCALAPPDATA%\Signalbox\agent-git-credential\github-apps.kdbx</c> for the
    /// database, <c>user-account.key</c> beside it for the protected key, and a <c>tokens</c> folder for the cache.
    /// </summary>
    /// <returns>The location.</returns>
    public static StoreLocation Default()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string root = Path.Combine(localApplicationData, FolderName, ToolFolderName);
        return new StoreLocation
        {
            DatabasePath = Path.Combine(root, DatabaseFileName),
            UserKeyPath = Path.Combine(root, UserKeyFileName),
            TokenCacheDirectory = Path.Combine(root, TokenFolderName),
        };
    }
}
