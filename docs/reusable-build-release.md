# Reusable build release

`.github/workflows/reusable-build-release.yml` builds a caller's deployable project once from a tag and publishes
the output as a GitHub release with two assets: the zip and its SHA-256 checksum. It implements pipeline steps
S40 to S44: the tag event fires, the artifact is built once, and the immutable artifact is published.

It is a reusable workflow (`on: workflow_call`). Nothing in this repository runs it. A product repository calls
it from its own `.github/workflows/build-release.yml` on a tag push, pinned to a version tag of this repository.

A release produced here means "requested for deployment", never "merged". The tag that starts it is created by
the deployment orchestrator at a leader's request, not by a merge, so the release list of a product repository
reads as its deployment history. The orchestrator records the release and its checksum, deploys the asset by
checksum, and later edits the release notes and the pre-release flag to say where the release went.

## Inputs

| Input | Type | Required | Default | What it does |
|---|---|---|---|---|
| `project-path` | string | yes | (none) | Path to the deployable project file, relative to the repository root. The workflow passes it to `dotnet publish` as one quoted argument, so a path with spaces needs no extra quoting from the caller. |
| `artifact-name` | string | yes | (none) | The asset file stem. The zip is `<artifact-name>-<tag>.zip`; the checksum file is that name plus `.sha256`. |
| `dotnet-version` | string | no | `10.0.x` | The .NET SDK version to install, in any form `actions/setup-dotnet` accepts. |
| `configuration` | string | no | `Release` | The build configuration given to `dotnet publish`. |
| `runs-on` | string | no | `ubuntu-latest` | The runner label. A caller whose code needs Windows passes `windows-latest`. Every step runs in `bash`, which both runner images provide. |
| `stamp-version` | boolean | no | `true` | When `true`, the tag is passed as `InformationalVersion`, so the running site can report which release it is. The .NET SDK may append `+<commit>` to that value; a reader compares the prefix. |

## Secrets

None. The workflow declares no `secrets:` block, so a caller passes none and needs no `secrets: inherit`. The
release is created with the run's own `GITHUB_TOKEN`.

## Permissions

The workflow asks for `contents: write`, which creating a release requires. A caller must grant at least that
much; a reusable workflow cannot hold more permission than its caller gives it. Checkout runs with
`persist-credentials: false`, so the token is not left in the cloned repository for later steps.

## What it runs

One job, `build-and-release`, in this order:

1. Fail unless the run was started from a tag. The tag name is the release name and the version stamp.
2. Check out the tagged commit.
3. Set up .NET at `dotnet-version`.
4. `dotnet restore <project-path>`
5. `dotnet publish <project-path> --no-restore --configuration <configuration> --output publish
   --self-contained false`, with `-p:InformationalVersion=<tag>` when `stamp-version` is `true`. The output is
   framework-dependent and portable: the target host provides the runtime.
6. Zip the publish folder so its contents sit at the root of the archive, then write
   `<zip>.sha256` in `sha256sum` form: the hex digest, two spaces, the file name.
7. Create the release as a draft, marked pre-release, titled with the tag, with `--verify-tag` so a missing tag
   fails here rather than creating one. Upload both assets. Publish the release. The draft step matters: with
   release immutability on, a release locks when it is published, so every asset must be attached first.
8. Write a summary line, whatever the earlier steps did.

Runs for different tags do not cancel each other: the concurrency group is per tag and `cancel-in-progress` is
off, because a half-finished release is worse than a slow one.

## What a caller sees

- **The release.** One GitHub release named for the tag, flagged pre-release, with two assets:
  `<artifact-name>-<tag>.zip` and `<artifact-name>-<tag>.zip.sha256`. The notes state the commit and the digest.
- **The summary line.** One line on the run's summary page, for example:
  `Build release 2026.1025.1 for src/MyApp/MyApp.csproj: restore success, publish success, package success,
  release success.`, followed by the digest and the release URL when they exist.
- **No Actions artifact.** The zip travels as a release asset only. Release assets have no size or count quota
  and are not Actions artifacts, so nothing here draws on the account's Actions storage.

## Caller

A product repository saves this as `.github/workflows/build-release.yml`:

```yaml
name: Build release
on:
  push:
    tags:
      - "[0-9][0-9][0-9][0-9].[0-9]*.[0-9]*"
permissions:
  contents: write
jobs:
  release:
    uses: mattonweb/Signalbox/.github/workflows/reusable-build-release.yml@<version tag>
    with:
      project-path: src/MyApp/MyApp.csproj
      artifact-name: my-app
```

Pin `uses:` to a version tag of this repository, never to `main`. The tag takes the form `YYYY.MMdd.N` and
moves only when a human moves it. The tag filter above matches the same form, so an unrelated tag starts no
release. A tag pushed under a GitHub App's installation token starts this workflow; a tag pushed from inside
another workflow with its own `GITHUB_TOKEN` does not.
