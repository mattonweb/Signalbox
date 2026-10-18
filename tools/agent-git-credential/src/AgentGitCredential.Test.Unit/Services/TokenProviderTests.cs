namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>Tests for <see cref="TokenProvider"/>.</summary>
[TestClass]
public sealed class TokenProviderTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepositoryName City = new RepositoryName("mattonweb", "City");

    /// <summary>With nothing cached, the registration is read and a token minted and cached.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetTokenAsync_RegisteredAndNotCached_MintsOnceAndCaches()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
            registrations.Create();
            registrations.Save(Registration(City));
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>();
            client.Setup(c => c.MintAsync(It.IsAny<AppRegistration>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstallationToken("ghs_minted", Now.AddHours(1)));
            TokenProvider provider = new TokenProvider(registrations, cache, client.Object);

            InstallationToken? first = await provider.GetTokenAsync(City).ConfigureAwait(false);
            InstallationToken? second = await provider.GetTokenAsync(City).ConfigureAwait(false);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual("ghs_minted", second.Value.Value);
            client.Verify(c => c.MintAsync(It.IsAny<AppRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>An unregistered repository yields no token and no call to GitHub.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetTokenAsync_Unregistered_ReturnsNullWithoutMinting()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
            registrations.Create();
            TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>(MockBehavior.Strict);
            TokenProvider provider = new TokenProvider(registrations, cache, client.Object);

            InstallationToken? token = await provider.GetTokenAsync(City).ConfigureAwait(false);

            Assert.IsNull(token);
        }
    }

    private static AppRegistration Registration(RepositoryName repository)
    {
        return new AppRegistration
        {
            Repository = repository,
            AppId = new AppId(1),
            InstallationId = new InstallationId(2),
            PrivateKey = TestKeys.NewPrivateKey(),
        };
    }
}
