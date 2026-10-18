namespace AgentGitCredential.Test.Unit.TestDoubles;

using System;
using System.IO;
using AgentGitCredential.Entities;

/// <summary>
/// A store location under a fresh temporary folder, deleted on dispose.
/// </summary>
public sealed class TemporaryStore
    : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryStore"/> class.
    /// </summary>
    public TemporaryStore()
    {
        this._root = Path.Combine(Path.GetTempPath(), "agent-git-credential-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._root);
        this.Location = new StoreLocation
        {
            DatabasePath = Path.Combine(this._root, "github-apps.kdbx"),
            UserKeyPath = Path.Combine(this._root, "user-account.key"),
            TokenCacheDirectory = Path.Combine(this._root, "tokens"),
        };
    }

    /// <summary>Gets the location.</summary>
    public StoreLocation Location { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(this._root, true);
    }
}
