# agent-git-credential: a coding agent pushes and opens pull requests as its own GitHub App

A git credential helper, `git-credential-github-app`, plus a `gh` shim and an installer. Together they let a
coding agent's session commit, push and work pull requests under a GitHub App identity of its own, with the
app's private key held in a KeePass database keyed to the Windows user account and never seen by the agent.

## How it fits together

| Piece | What it is | Where it lands |
|---|---|---|
| `src/AgentGitCredential` | the helper: .NET 10, standard library plus KeePassLib | `~/bin/agent-git-credential/git-credential-github-app.exe` |
| `src/AgentGitCredential.Test.Unit` | 31 MSTest tests: parsing, the JSON Web Token, the cache, the KeePass store, the dispatcher | not installed |
| `gh`, `gh.ps1` | shims that run the real GitHub CLI with the repository's app token when the session is an agent's | `~/bin/gh`, `~/bin/gh.ps1` |
| `install.sh` | publishes the helper, places the shims, creates the database | run from the repo root |

Git calls the helper on every network operation that needs a credential. The helper reads the repository
from the request (`credential.useHttpPath` on), finds that repository's registration in the database, mints a
one-hour installation token with the app's private key, caches it protected for the current user, and answers
`username=x-access-token` and the token. A repository with no registration gets no answer, so git falls
through to whatever comes next, which in an agent session is nothing: an unregistered repository cannot be
pushed under a borrowed identity.

Nothing is configured globally. A repository opts its agent sessions in through the `env` block in its
`.claude/settings.json` (REPO-SETUP.md), which names the helper, resets any other helper, turns the path on,
sets the author and committer to the app's bot identity, and marks the session with
`AGENT_GIT_IDENTITY=github-app` so the `gh` shim fetches a token. The same checkout opened in Visual Studio or
a plain terminal sees none of that and keeps the person's own identity.

## The database

`%LOCALAPPDATA%\Signalbox\agent-git-credential\github-apps.kdbx`, a KeePass 2 file with no master password. Its
key is a random 64-byte secret generated on `init`, protected with Windows data protection for the current user
and kept beside it as `user-account.key`; the helper feeds it to KeePass as a custom key. The database therefore
opens only for this account on this machine, and a copy elsewhere is unreadable by design. KeePass's own
Windows-user-account key source is not used: the cross-platform KeePassLib build backs it with per-process keys,
so a second process could not reopen the file. The KeePass desktop program cannot open this file either; `list`
is the view. One entry per repository, titled `github.com/owner/name`, with three strings: `AppId`,
`InstallationId` and `PrivateKeyPem` (protected in memory).

Cached tokens live beside it under `tokens\`, one DPAPI-protected file per repository, refreshed when under
five minutes remain.

Any process running as the same Windows user can open the database, including the agent. That is the same
trust level as the other per-agent secrets on the machine, and the token design limits what a read would give:
an app scoped to one repository with the least permissions it needs, and tokens that die within the hour. A
key is rotated from the app's settings page. A harder boundary later means re-keying the file to a service
account and pointing the helper at a service; nothing else changes.

## Commands

```
git-credential-github-app get | store | erase      the git credential protocol; git calls these
git-credential-github-app token --repository owner/name
git-credential-github-app init
git-credential-github-app set --repository owner/name --app-id N --installation-id N --private-key-file path.pem
git-credential-github-app list
```

`set` reads the private key from the file GitHub downloaded and writes it into the database; the key is never
passed on a command line. Delete the `.pem` afterwards.

## Build and test

```bash
dotnet test tools/agent-git-credential/src/AgentGitCredential.slnx
```

The code follows the house `code-csharp-standards`; the one analyzer suppression is on RS256's PKCS#1 v1.5
padding, which the JSON Web Algorithms specification requires and GitHub accepts no alternative to.
