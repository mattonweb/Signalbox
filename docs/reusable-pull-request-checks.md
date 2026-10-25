# Reusable pull request checks

`.github/workflows/reusable-pull-request-checks.yml` restores, builds and runs the unit tests of a caller's .NET
solution on every pull request and reports green or red. It implements pipeline step S20: unmetered checks on
every push.

It is a reusable workflow (`on: workflow_call`). Nothing in this repository runs it. A product repository calls
it from its own `.github/workflows/pull-request-checks.yml`, pinned to a version tag of this repository.

## Inputs

| Input | Type | Required | Default | What it does |
|---|---|---|---|---|
| `solution-path` | string | yes | (none) | Path to the solution or project file, relative to the repository root. The workflow passes it to `dotnet` as one quoted argument, so a path with spaces needs no extra quoting from the caller. |
| `dotnet-version` | string | no | `10.0.x` | The .NET SDK version to install, in any form `actions/setup-dotnet` accepts. |
| `configuration` | string | no | `Release` | The build configuration given to `dotnet build` and `dotnet test`. |
| `runs-on` | string | no | `ubuntu-latest` | The runner label. A caller whose code needs Windows passes `windows-latest`. Every step runs in `bash`, which both runner images provide. |
| `test-filter` | string | no | empty | Passed to `dotnet test --filter` when non-empty. Empty runs every test. |
| `enforce-format` | boolean | no | `false` | When `true`, runs `dotnet format --verify-no-changes` on the solution and fails on any difference. Off by default until the house `.editorconfig` is enforced in each caller's build. |

## Secrets

None. The workflow declares no `secrets:` block, so a caller passes none and needs no `secrets: inherit`. Every
value it needs arrives as an input above, and it defaults no private value.

## Permissions

The workflow asks for `contents: read` and nothing more. Checkout runs with `persist-credentials: false`, so the
token is not left in the cloned repository for later steps.

## What it runs

One job, `build-and-test`, in this order:

1. Check out the caller's commit.
2. Set up .NET at `dotnet-version`.
3. `dotnet restore <solution-path>`
4. `dotnet build <solution-path> --no-restore --configuration <configuration>`. A caller that sets
   `TreatWarningsAsErrors` fails here on any analyzer warning, with no extra flag.
5. `dotnet test <solution-path> --no-build --configuration <configuration> --logger trx --results-directory
   test-results`, with `--filter <test-filter>` when given. The `trx` logger is a VSTest logger: a
   solution switched to the Microsoft.Testing.Platform runner in `global.json` needs its own logger setup.
6. `dotnet format <solution-path> --verify-no-changes --no-restore`, only when `enforce-format` is `true`.
7. Upload the `.trx` files, whatever the earlier steps did.
8. Write a summary line, whatever the earlier steps did.

A new push to the same pull request cancels the run for the old head: the concurrency group is the calling
workflow's name plus `github.ref`. A caller should not set a concurrency group of its own with the same key.

## What a caller sees

- **The check.** One check on the pull request, named `<caller job id> / build-and-test`. With the snippet
  below that is `checks / build-and-test`. That is the name to require in branch protection.
- **The summary line.** One line on the run's summary page, for example:
  `Pull request checks for src/MyApp.slnx: restore success, build success, test failure, format not enforced.`
  Each outcome is `success`, `failure`, `cancelled` or `skipped`.
- **The `test-results` artifact.** The `.trx` files from `dotnet test`, uploaded even when tests fail and kept
  for seven days, since nothing reads them after the run and artifact storage is metered per account. It is
  missing, with a warning, when the run stopped before the tests ran.

## Caller

A product repository saves this as `.github/workflows/pull-request-checks.yml`:

```yaml
name: Pull request checks
on:
  pull_request:
permissions:
  contents: read
jobs:
  checks:
    uses: mattonweb/Signalbox/.github/workflows/reusable-pull-request-checks.yml@<version tag>
    with:
      solution-path: src/MyApp.slnx
```

Pin `uses:` to a version tag of this repository, never to `main`. The tag takes the form `YYYY.MMdd.N` and
moves only when a human moves it. Add any of the optional inputs under `with:`.
