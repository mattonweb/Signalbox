namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.Collections.Generic;
using System.IO;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="RegistrationStore"/> against a real KeePass file keyed by password.</summary>
[TestClass]
public sealed class RegistrationStoreTests
{
    private static readonly RepositoryName City = new RepositoryName("mattonweb", "City");

    /// <summary>Create writes a database file; a second create is refused.</summary>
    [TestMethod]
    public void Create_Twice_SecondIsRefused()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());

            registrations.Create();

            Assert.IsTrue(File.Exists(store.Location.DatabasePath));
            Assert.ThrowsExactly<InvalidOperationException>(registrations.Create);
        }
    }

    /// <summary>A saved registration reads back field for field.</summary>
    [TestMethod]
    public void Save_ThenFind_RoundTrips()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
            registrations.Create();
            PrivateKeyPem key = TestKeys.NewPrivateKey();
            registrations.Save(Registration(City, 11, 22, key));

            AppRegistration? found = registrations.Find(City);

            Assert.IsNotNull(found);
            Assert.AreEqual(11, found.AppId.Value);
            Assert.AreEqual(22, found.InstallationId.Value);
            Assert.AreEqual(key.Value, found.PrivateKey.Value);
        }
    }

    /// <summary>Saving the same repository again replaces the entry rather than adding a second.</summary>
    [TestMethod]
    public void Save_SameRepositoryTwice_OneEntryWithNewValues()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
            registrations.Create();
            registrations.Save(Registration(City, 11, 22, TestKeys.NewPrivateKey()));
            registrations.Save(Registration(City, 33, 44, TestKeys.NewPrivateKey()));

            IReadOnlyList<RepositoryName> names = registrations.List();
            AppRegistration? found = registrations.Find(City);

            Assert.HasCount(1, names);
            Assert.IsNotNull(found);
            Assert.AreEqual(33, found.AppId.Value);
        }
    }

    /// <summary>A repository with no entry finds nothing.</summary>
    [TestMethod]
    public void Find_Unregistered_ReturnsNull()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
            registrations.Create();

            Assert.IsNull(registrations.Find(new RepositoryName("mattonweb", "Nowhere")));
        }
    }

    /// <summary>Find before init says so instead of creating anything.</summary>
    [TestMethod]
    public void Find_NoDatabase_ThrowsNamingInit()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => registrations.Find(City));

            Assert.IsTrue(exception.Message.Contains("init", StringComparison.Ordinal));
        }
    }

    private static AppRegistration Registration(RepositoryName repository, long appId, long installationId, PrivateKeyPem key)
    {
        return new AppRegistration
        {
            Repository = repository,
            AppId = new AppId(appId),
            InstallationId = new InstallationId(installationId),
            PrivateKey = key,
        };
    }
}
