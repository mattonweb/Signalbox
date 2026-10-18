namespace AgentGitCredential.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AgentGitCredential.DTOs;
using AgentGitCredential.Entities;

/// <summary>
/// Routes the command verb to its handler and writes what git or the operator expects to see.
/// </summary>
public sealed class CommandDispatcher
{
    private const string GitHubHost = "github.com";
    private const string TokenUserName = "x-access-token";

    private readonly RegistrationStore _registrations;
    private readonly TokenProvider _tokens;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandDispatcher"/> class.
    /// </summary>
    /// <param name="registrations">The app registrations.</param>
    /// <param name="tokens">Supplies tokens.</param>
    /// <param name="input">Standard input, where git writes its request.</param>
    /// <param name="output">Standard output, where git reads the answer.</param>
    public CommandDispatcher(RegistrationStore registrations, TokenProvider tokens, TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        this._registrations = registrations;
        this._tokens = tokens;
        this._input = input;
        this._output = output;
    }

    /// <summary>
    /// Wires the production dependencies.
    /// </summary>
    /// <param name="location">Where the state lives.</param>
    /// <param name="httpClient">The client for GitHub.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="input">Standard input.</param>
    /// <param name="output">Standard output.</param>
    /// <returns>The dispatcher.</returns>
    public static CommandDispatcher Create(StoreLocation location, HttpClient httpClient, TimeProvider timeProvider, TextReader input, TextWriter output)
    {
        RegistrationStore registrations = new RegistrationStore(location, new UserAccountKeySource(location));
        TokenCache cache = new TokenCache(location, timeProvider);
        InstallationTokenClient client = new InstallationTokenClient(httpClient, new JsonWebTokenBuilder(timeProvider));
        TokenProvider tokens = new TokenProvider(registrations, cache, client);
        return new CommandDispatcher(registrations, tokens, input, output);
    }

    /// <summary>
    /// Runs one command.
    /// </summary>
    /// <param name="arguments">The command line, verb first.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> RunAsync(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string verb = arguments.Length == 0 ? "help" : arguments[0].ToLowerInvariant();
        return verb switch
        {
            "get" =>
                await this.GetAsync().ConfigureAwait(false),
            "store" =>
                0,
            "erase" =>
                0,
            "token" =>
                await this.TokenAsync(CommandArguments.Parse(arguments)).ConfigureAwait(false),
            "init" =>
                this.Init(),
            "set" =>
                this.Set(CommandArguments.Parse(arguments)),
            "list" =>
                this.List(),
            _ =>
                this.Help(),
        };
    }

    private static RepositoryName? RepositoryOf(GitCredentialRequest request)
    {
        if (!string.Equals(request.Host, GitHubHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return RepositoryNameParser.TryParseCredentialPath(request.Path);
    }

    private async Task<int> GetAsync()
    {
        GitCredentialRequest request = GitCredentialRequestParser.Parse(this._input);
        RepositoryName? repository = RepositoryOf(request);
        if (repository is null)
        {
            return 0;
        }

        InstallationToken? token = await this._tokens.GetTokenAsync(repository.Value).ConfigureAwait(false);
        if (token is null)
        {
            return 0;
        }

        await this._output.WriteLineAsync("username=" + TokenUserName).ConfigureAwait(false);
        await this._output.WriteLineAsync("password=" + token.Value.Value).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> TokenAsync(CommandArguments arguments)
    {
        RepositoryName repository = RepositoryNameParser.ParseSlug(arguments.Require("repository"));
        InstallationToken? token = await this._tokens.GetTokenAsync(repository).ConfigureAwait(false);
        if (token is null)
        {
            throw new InvalidOperationException("No GitHub App is registered for " + repository.ToString() + "; run 'set' first.");
        }

        await this._output.WriteLineAsync(token.Value.Value).ConfigureAwait(false);
        return 0;
    }

    private int Init()
    {
        this._registrations.Create();
        this._output.WriteLine("Created the registration database, keyed to this Windows user account.");
        return 0;
    }

    private int Set(CommandArguments arguments)
    {
        AppRegistration registration = new AppRegistration
        {
            Repository = RepositoryNameParser.ParseSlug(arguments.Require("repository")),
            AppId = new AppId(long.Parse(arguments.Require("app-id"), CultureInfo.InvariantCulture)),
            InstallationId = new InstallationId(long.Parse(arguments.Require("installation-id"), CultureInfo.InvariantCulture)),
            PrivateKey = new PrivateKeyPem(File.ReadAllText(arguments.Require("private-key-file"))),
        };
        this._registrations.Save(registration);
        this._output.WriteLine("Registered " + registration.Repository.ToString() + " with app " + registration.AppId.ToString() + ".");
        return 0;
    }

    private int List()
    {
        IReadOnlyList<RepositoryName> names = this._registrations.List();
        foreach (RepositoryName name in names)
        {
            this._output.WriteLine(name.ToString());
        }

        return 0;
    }

    private int Help()
    {
        this._output.WriteLine("git-credential-github-app: a git credential helper that answers with a GitHub App installation token.");
        this._output.WriteLine("  get | store | erase                                   the git credential protocol (git calls these)");
        this._output.WriteLine("  token --repository owner/name                         print a token, for the gh shim");
        this._output.WriteLine("  init                                                  create the registration database for this Windows user");
        this._output.WriteLine("  set --repository owner/name --app-id N --installation-id N --private-key-file path.pem");
        this._output.WriteLine("  list                                                  the repositories with a registered app");
        return 2;
    }
}
