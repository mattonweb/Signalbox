# Reusable build release

`.github/workflows/reusable-build-release.yml` names a release after the calling workflow's run number, tags
the commit, builds the caller's deployable project once from it, and publishes the output as a GitHub
release with two assets: the zip and its SHA-256 checksum. It implements pipeline steps S36 to S44: the
release is tagged, the artifact is built once, and the immutable artifact is published.

It is a reusable workflow (`on: workflow_call`). Nothing in this repository runs it. A product repository calls
it from its own `.github/workflows/build-release.yml`, started by `workflow_dispatch`, pinned to a version tag
of this repository.

A release produced here means "requested for deployment", never "merged". The dispatch comes from the
deployment orchestrator at a leader's request, not from a merge, so the release list of a product repository
reads as its deployment history. The orchestrator records the release and its checksum, deploys the asset by
checksum, and later edits the release notes and the pre-release flag to say where the release went.

## The tag

`<YYYY.MMdd>.<run number>`: the calendar date in `time-zone`, then the calling workflow's `run_number`, which
GitHub increments by one on every run of that workflow in that repository and never reuses. The run counter is
therefore the patch number, the tag is unique without any store of its own, and tags sort in the order the
releases were requested. The counter does not reset at midnight, so a day's tags are not numbered from one.

## Inputs

| Input | Type | Required | Default | What it does |
|---|---|---|---|---|
| `project-path` | string | yes | (none) | Path to the deployable project file, relative to the repository root. The workflow passes it to `dotnet publish` as one quoted argument, so a path with spaces needs no extra quoting from the caller. |
| `artifact-name` | string | yes | (none) | The asset file stem. The zip is `<artifact-name>-<tag>.zip`; the checksum file is that name plus `.sha256`. |
| `time-zone` | string | no | `UTC` | IANA time zone the date part of the tag is taken in, so the tag carries the calendar date of the people who read it. |
| `dotnet-version` | string | no | `10.0.x` | The .NET SDK version to install, in any form `actions/setup-dotnet` accepts. |
| `configuration` | string | no | `Release` | The build configuration given to `dotnet publish`. |
| `runs-on` | string | no | `ubuntu-latest` | The runner label. A caller whose code needs Windows passes `windows-latest`. Every step runs in `bash`, which both runner images provide. |
| `stamp-version` | boolean | no | `true` | When `true`, the tag is passed as `InformationalVersion`, so the running site can report which release it is. The .NET SDK may append `+<commit>` to that value; a reader compares the prefix. |
| `self-contained` | boolean | no | `false` | When `true`, `dotnet publish` bundles the .NET runtime into the output, for a machine with no runtime installed, such as a Windows service on a bare VPS. The zip is large, because the whole runtime is inside; the project's own publish settings (single file, trimming off) still apply. Needs `runtime`. |
| `runtime` | string | no | (empty) | The runtime identifier for a self-contained publish, for example `win-x64`. The restore and the publish both receive it. Required when `self-contained` is `true`; the workflow fails before building if it is missing. Ignored otherwise. |

## Outputs

| Output | What it carries |
|---|---|
| `tag` | The tag the release was created under. |
| `digest` | The SHA-256 of the zip asset, as hex. |

## Secrets

None. The workflow declares no `secrets:` block, so a caller passes none and needs no `secrets: inherit`. The
tag and the release are created with the run's own `GITHUB_TOKEN`.

## Permissions

The workflow asks for `contents: write`, which creating a tag and a release requires. A caller must grant at
least that much; a reusable workflow cannot hold more permission than its caller gives it. Checkout runs with
`persist-credentials: false`, so the token is not left in the cloned repository for later steps.

## What it runs

One job, `build-and-release`, in this order:

1. Name the release: fail if the run was started from a tag, since this workflow creates the tag; otherwise
   compute `<date>.<run number>`.
2. Check out the dispatched commit.
3. Set up .NET at `dotnet-version`.
4. `dotnet restore <project-path>`, with `--runtime <runtime>` when `self-contained` is `true`.
5. `dotnet publish <project-path> --no-restore --configuration <configuration> --output publish`, then either
   `--self-contained false` (the default: the output is framework-dependent and portable, and the target host
   provides the runtime) or `--self-contained true --runtime <runtime>` (the runtime is bundled, for a machine
   that has none), with `-p:InformationalVersion=<tag>` when `stamp-version` is `true`.
6. Zip the publish folder so its contents sit at the root of the archive, then write
   `<zip>.sha256` in `sha256sum` form: the hex digest, two spaces, the file name.
7. Create the release as a draft, marked pre-release, titled with the tag, with `--target` set to the
   dispatched commit so the tag is created there. Upload both assets. Publish the release. The draft step
   matters: with release immutability on, a release locks when it is published, so every asset must be
   attached first. The notes carry the commit, the run id and run number, and the digest, so a reader of the
   release can find the run and a watcher of the run can find the release.
8. Write a summary line, whatever the earlier steps did.

Runs do not cancel each other: `cancel-in-progress` is off, because a half-finished release is worse than a
slow one.

## What a caller sees

- **The release.** One GitHub release named for the tag, flagged pre-release, with two assets:
  `<artifact-name>-<tag>.zip` and `<artifact-name>-<tag>.zip.sha256`. The notes state the commit, the run and
  the digest.
- **The tag**, created at the dispatched commit by the release.
- **The summary line.** One line on the run's summary page, for example:
  `Build release 2026.1025.7 for src/MyApp/MyApp.csproj: restore success, publish success, package success,
  release success.`, followed by the digest and the release URL when they exist.
- **No Actions artifact.** The zip travels as a release asset only. Release assets have no size or count quota
  and are not Actions artifacts, so nothing here draws on the account's Actions storage.

## Caller

A product repository saves this as `.github/workflows/build-release.yml`:

```yaml
name: Build release
on:
  workflow_dispatch:
permissions:
  contents: write
jobs:
  release:
    uses: mattonweb/Signalbox/.github/workflows/reusable-build-release.yml@<version tag>
    with:
      project-path: src/MyApp/MyApp.csproj
      artifact-name: my-app
      time-zone: America/Chicago
```

Pin `uses:` to a version tag of this repository, never to `main`. The tag takes the form `YYYY.MMdd.N` and
moves only when a human moves it. Dispatching the caller through the REST API needs a credential with the
Actions read-and-write permission on the repository; the orchestrator's GitHub App carries it.
