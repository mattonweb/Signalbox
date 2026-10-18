namespace AgentGitCredential;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;

/// <summary>
/// Process entry point for <c>git-credential-github-app</c>, a git credential helper that answers a request
/// for <c>github.com</c> with a GitHub App installation token for the repository named in the request. The
/// app registrations live in a KeePass database keyed to the Windows user account; tokens are cached per
/// repository under the user's local application data, protected with the same account.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the helper. The first argument is the verb: <c>get</c>, <c>store</c> and <c>erase</c> follow the
    /// git credential protocol; <c>token</c>, <c>init</c>, <c>set</c> and <c>list</c> are the operator commands.
    /// </summary>
    /// <param name="arguments">The command line, verb first.</param>
    /// <returns>Zero on success; one on any failure, which is also written to standard error.</returns>
    public static async Task<int> Main(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        StoreLocation location = StoreLocations.Default();
        using (HttpClient httpClient = new HttpClient())
        {
            CommandDispatcher dispatcher = CommandDispatcher.Create(location, httpClient, TimeProvider.System, Console.In, Console.Out);
            try
            {
                return await dispatcher.RunAsync(arguments).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Boundary: the process entry point reports any failure on standard error and exits non-zero.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                await Console.Error.WriteLineAsync("git-credential-github-app: " + exception.Message).ConfigureAwait(false);
                return 1;
            }
        }
    }
}
