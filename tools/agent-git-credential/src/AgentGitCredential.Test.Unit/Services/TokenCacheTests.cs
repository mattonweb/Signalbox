namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.IO;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="TokenCache"/>.</summary>
[TestClass]
public sealed class TokenCacheTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepositoryName Repository = new RepositoryName("mattonweb", "City");

    /// <summary>A saved token with more than the margin left loads back.</summary>
    [TestMethod]
    public void Load_SavedWithAnHourLeft_ReturnsToken()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
            cache.Save(Repository, new InstallationToken("ghs_example", Now.AddHours(1)));

            InstallationToken? loaded = cache.Load(Repository);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("ghs_example", loaded.Value.Value);
        }
    }

    /// <summary>A token inside the five-minute margin counts as absent.</summary>
    [TestMethod]
    public void Load_SavedWithTwoMinutesLeft_ReturnsNull()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
            cache.Save(Repository, new InstallationToken("ghs_example", Now.AddMinutes(2)));

            Assert.IsNull(cache.Load(Repository));
        }
    }

    /// <summary>No file means no token.</summary>
    [TestMethod]
    public void Load_NothingSaved_ReturnsNull()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));

            Assert.IsNull(cache.Load(Repository));
        }
    }

    /// <summary>The file on disk is protected: the token text does not appear in it.</summary>
    [TestMethod]
    public void Save_WritesProtectedBytes_TokenNotInPlainText()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
            cache.Save(Repository, new InstallationToken("ghs_example_secret", Now.AddHours(1)));

            string[] files = Directory.GetFiles(store.Location.TokenCacheDirectory);
            string content = File.ReadAllText(files[0]);

            Assert.HasCount(1, files);
            Assert.IsFalse(content.Contains("ghs_example_secret", StringComparison.Ordinal));
        }
    }
}
