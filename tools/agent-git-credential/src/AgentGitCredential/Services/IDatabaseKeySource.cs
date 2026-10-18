namespace AgentGitCredential.Services;

using KeePassLib.Keys;

/// <summary>
/// Supplies the composite key that opens the registration database. The helper uses the Windows user account;
/// tests use a password.
/// </summary>
public interface IDatabaseKeySource
{
    /// <summary>
    /// Creates a fresh composite key. KeePassLib consumes a key on open, so a new one is built per call.
    /// </summary>
    /// <returns>The key.</returns>
    CompositeKey CreateKey();
}
