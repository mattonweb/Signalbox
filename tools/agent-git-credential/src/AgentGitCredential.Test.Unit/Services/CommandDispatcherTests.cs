namespace AgentGitCredential.Test.Unit.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using AgentGitCredential.Test.Unit.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>Tests for <see cref="CommandDispatcher"/>: the git credential protocol end to end, without GitHub.</summary>
[TestClass]
public sealed class CommandDispatcherTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A get for a registered github.com repository answers with the token user name and the token.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RunAsync_GetForRegisteredRepository_WritesUsernameAndPassword()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = Registered(store, new RepositoryName("mattonweb", "City"));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>();
            client.Setup(c => c.MintAsync(It.IsAny<AppRegistration>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstallationToken("ghs_city", Now.AddHours(1)));
            using (StringReader input = new StringReader("protocol=https\nhost=github.com\npath=mattonweb/City.git\n\n"))
            using (StringWriter output = new StringWriter())
            {
                CommandDispatcher dispatcher = Dispatcher(store, registrations, client.Object, input, output);

                int exitCode = await dispatcher.RunAsync(["get"]).ConfigureAwait(false);

                Assert.AreEqual(0, exitCode);
                Assert.AreEqual("username=x-access-token" + Environment.NewLine + "password=ghs_city" + Environment.NewLine, output.ToString());
            }
        }
    }

    /// <summary>A get for a host that is not github.com writes nothing, so git moves on.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RunAsync_GetForOtherHost_WritesNothing()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = Registered(store, new RepositoryName("mattonweb", "City"));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>(MockBehavior.Strict);
            using (StringReader input = new StringReader("protocol=https\nhost=gitlab.com\npath=mattonweb/City.git\n\n"))
            using (StringWriter output = new StringWriter())
            {
                CommandDispatcher dispatcher = Dispatcher(store, registrations, client.Object, input, output);

                int exitCode = await dispatcher.RunAsync(["get"]).ConfigureAwait(false);

                Assert.AreEqual(0, exitCode);
                Assert.AreEqual(string.Empty, output.ToString());
            }
        }
    }

    /// <summary>A get for an unregistered repository writes nothing and never calls GitHub.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RunAsync_GetForUnregisteredRepository_WritesNothing()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = Registered(store, new RepositoryName("mattonweb", "City"));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>(MockBehavior.Strict);
            using (StringReader input = new StringReader("protocol=https\nhost=github.com\npath=mattonweb/Other.git\n\n"))
            using (StringWriter output = new StringWriter())
            {
                CommandDispatcher dispatcher = Dispatcher(store, registrations, client.Object, input, output);

                int exitCode = await dispatcher.RunAsync(["get"]).ConfigureAwait(false);

                Assert.AreEqual(0, exitCode);
                Assert.AreEqual(string.Empty, output.ToString());
            }
        }
    }

    /// <summary>Store and erase are accepted and ignored.</summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RunAsync_StoreAndErase_ReturnZero()
    {
        using (TemporaryStore store = new TemporaryStore())
        {
            RegistrationStore registrations = Registered(store, new RepositoryName("mattonweb", "City"));
            Mock<IInstallationTokenClient> client = new Mock<IInstallationTokenClient>(MockBehavior.Strict);
            using (StringReader input = new StringReader(string.Empty))
            using (StringWriter output = new StringWriter())
            {
                CommandDispatcher dispatcher = Dispatcher(store, registrations, client.Object, input, output);

                Assert.AreEqual(0, await dispatcher.RunAsync(["store"]).ConfigureAwait(false));
                Assert.AreEqual(0, await dispatcher.RunAsync(["erase"]).ConfigureAwait(false));
            }
        }
    }

    private static RegistrationStore Registered(TemporaryStore store, RepositoryName repository)
    {
        RegistrationStore registrations = new RegistrationStore(store.Location, new PasswordKeySource());
        registrations.Create();
        registrations.Save(new AppRegistration
        {
            Repository = repository,
            AppId = new AppId(1),
            InstallationId = new InstallationId(2),
            PrivateKey = TestKeys.NewPrivateKey(),
        });
        return registrations;
    }

    private static CommandDispatcher Dispatcher(TemporaryStore store, RegistrationStore registrations, IInstallationTokenClient client, TextReader input, TextWriter output)
    {
        TokenCache cache = new TokenCache(store.Location, new FixedTimeProvider(Now));
        TokenProvider tokens = new TokenProvider(registrations, cache, client);
        return new CommandDispatcher(registrations, tokens, input, output);
    }
}
