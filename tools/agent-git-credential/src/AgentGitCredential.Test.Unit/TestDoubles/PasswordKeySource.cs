namespace AgentGitCredential.Test.Unit.TestDoubles;

using AgentGitCredential.Services;
using KeePassLib.Keys;

/// <summary>
/// Keys a test database with a fixed password, so tests do not depend on the Windows account.
/// </summary>
public sealed class PasswordKeySource
    : IDatabaseKeySource
{
    private const string Password = "unit-test-only"; // betterleaks:allow - a fixed password for a throwaway test database, not a secret

    /// <inheritdoc/>
    public CompositeKey CreateKey()
    {
        CompositeKey key = new CompositeKey();
        key.AddUserKey(new KcpPassword(Password));
        return key;
    }
}
