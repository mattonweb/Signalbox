namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.IO;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using KeePassLib.Keys;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="UserAccountKeySource"/>: the key must survive a new process, which a new instance stands in for.</summary>
[TestClass]
public sealed class UserAccountKeySourceTests
{
    /// <summary>A database created with one instance opens with a fresh instance over the same location.</summary>
    [TestMethod]
    public void CreateKey_FreshInstance_OpensDatabaseCreatedByAnother()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            new RegistrationStore(store.Location, new UserAccountKeySource(store.Location)).Create();

            RegistrationStore reopened = new RegistrationStore(store.Location, new UserAccountKeySource(store.Location));

            Assert.IsEmpty(reopened.List());
            Assert.IsTrue(File.Exists(store.Location.UserKeyPath));
        }
    }

    /// <summary>The secret on disk is protected: it is not the raw key.</summary>
    [TestMethod]
    public void CreateKey_KeyFile_IsNotPlainText()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            new UserAccountKeySource(store.Location).CreateKey();

            byte[] bytes = File.ReadAllBytes(store.Location.UserKeyPath);

            Assert.IsGreaterThan(64, bytes.Length);
        }
    }

    /// <summary>A second location gets a different secret.</summary>
    [TestMethod]
    public void CreateKey_TwoLocations_DifferentKeys()
    {
        using (TemporaryStore first = new TemporaryStore())
        using (TemporaryStore second = new TemporaryStore())
        {
            new RegistrationStore(first.Location, new UserAccountKeySource(first.Location)).Create();
            StoreLocation crossed = new StoreLocation
            {
                DatabasePath = first.Location.DatabasePath,
                UserKeyPath = second.Location.UserKeyPath,
                TokenCacheDirectory = second.Location.TokenCacheDirectory,
            };

            RegistrationStore wrongKey = new RegistrationStore(crossed, new UserAccountKeySource(crossed));

            Assert.ThrowsExactly<InvalidCompositeKeyException>(() => wrongKey.List());
        }
    }
}
