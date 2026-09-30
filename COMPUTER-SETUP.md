# Computer setup: a fail-closed secrets scan on every commit

One install per machine puts a pre-commit hook in front of every git repo on it. The hook runs
[Betterleaks](https://github.com/betterleaks/betterleaks) over the staged changes and refuses the commit
if anything looks like a secret. It fails closed: if the scanner is missing, the commit is refused too. An
unscanned commit that passes silently is the failure this hook exists to prevent.

Per-repo setup (reaching shared skills by junction point) is in [REPO-SETUP.md](REPO-SETUP.md).

## What you need

- Windows with Git for Windows. The install runs in Git Bash, which ships with it, and uses `curl`,
  `unzip` and `sha256sum`, which also ship with it.
- Internet access once, to download the pinned Betterleaks release.

## Steps

### 1. Clone this repo

```powershell
git clone https://github.com/mattonweb/Signalbox.git C:\Source\Signalbox
```

### 2. Run the installer

From the repo root, in Git Bash:

```bash
sh tools/secret-scan/install.sh
```

Or from PowerShell, which has no `sh` of its own, using the one Git for Windows ships:

```powershell
& "$(Split-Path (Split-Path (Get-Command git).Source))\bin\sh.exe" tools/secret-scan/install.sh
```

The installer is safe to re-run. It:

| Does | Where | On re-run |
|---|---|---|
| Downloads the pinned Betterleaks release and checks its SHA-256 against the value written in the script | `~/bin/betterleaks.exe` | Skipped if that version is already there |
| Installs the pre-commit hook and pass-through hooks | `~/.githooks/` | Refreshed |
| Installs the generic base config | `~/.githooks/betterleaks.toml` | Kept if a config is already there |
| Points git at that hooks folder for every repo | `git config --global core.hooksPath` | Re-set |
| Lists repos whose own `core.hooksPath` would bypass the machine hook | printed | Re-checked |

The SHA-256 lives in the script, not fetched from anywhere, so a swapped release asset fails the install.

### 3. Verify

That git is wired:

```bash
git config --global --get core.hooksPath
```

Expect `~/.githooks` spelled out as a full path.

That the scanner runs:

```bash
~/bin/betterleaks.exe version
```

Expect `1.8.1`, the pinned version.

That the hook refuses a secret. In a scratch repo, stage a file holding a GitHub-token-shaped value and
try to commit. The value is generated on the spot, so this document holds no token-shaped literal, and
neither should yours: the hook that guards this repo refused the first draft of this section for exactly
that reason.

```bash
mkdir /tmp/scan-check && cd /tmp/scan-check && git init -q .
printf 'token = "ghp_%s"\n' "$(head -c 40 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 36)" > t.txt
git add t.txt && git commit -m "should be refused"
```

Expect `leaks found: 1` and `secret-scan: commit refused`. Delete the file and the commit goes through.
Delete the scratch repo afterwards.

### 4. Add your own rules without touching the base

The base config is the scanner's default rules and nothing else. Do not edit it. Write a private config
that extends it, and place that file at `~/.githooks/betterleaks.toml`, where the installer will leave it
alone on every future run:

```toml
title = "my private overlay"

[extend]
path = "C:/Source/Signalbox/tools/secret-scan/betterleaks.toml"

# Rules for your own secret formats. Shape only; never a real value.
[[rules]]
id = "my-service-api-key"
description = "My service API key"
regex = '''(?i)\bmysvc_([A-Za-z0-9]{32})\b'''
secretGroup = 1
keywords = ["mysvc_"]

# Allowlists for exposures you have accepted on purpose, scoped as tightly as you can.
[[allowlists]]
description = "A committed public token in one file, rotated periodically"
condition = "AND"
paths = ['''(?:^|/)src/MyApp/appsettings\.json$''']
regexTarget = "line"
regexes = ['''"PublicToken"\s*:''']
```

Tested 2026-09-29 with Betterleaks 1.8.1: an overlay written this way fires its own rule and the base's
default rules in the same scan. Keep the private overlay in a private repo of your own and copy it into
place by hand on each machine. It describes the shape of your secrets, which is itself worth keeping
private.

## How the hook chooses a config

1. A repo's own `.betterleaks.toml` or `.gitleaks.toml`, if present. This replaces the machine config
   entirely, so a repo config that should keep your private rules must extend them itself.
2. Otherwise `~/.githooks/betterleaks.toml`: the base, or your overlay.
3. Otherwise the scanner's built-in defaults.

After the scan passes, the hook runs the repo's own `.git/hooks/pre-commit` if one exists, so a repo's
local hooks still work. The pass-through hooks do the same for pre-push, post-checkout, post-commit,
post-merge, commit-msg and prepare-commit-msg, which Git LFS and similar tools rely on.

## When it refuses a commit

- **A real secret.** Move the value out of the repo: a secrets store, user-secrets, or an environment
  variable. Then commit.
- **A placeholder in documentation.** `Password=secret` in an example is refused, because a literal word
  after `Password=` is what the rule exists to catch. Use a form the scanner recognises as a placeholder,
  such as `Password={password}`. Tested: `{password}`, `<password>` and `********` pass; `secret`,
  `default`, `YOUR_PASSWORD` and `example-password` do not.
- **A genuine false positive on one line.** Add a `betterleaks:allow` comment on that line. Prefer this
  over an allowlist, because it is visible next to the thing it excuses.
- **Something you accept on purpose in one file.** An allowlist in your overlay, scoped to that path and
  that line pattern, as in the example above. Never widen the base.

## Things worth knowing

- **Only staged changes are scanned.** Secrets already in history are not found by this hook. Sweep
  history separately, with `betterleaks git <repo>` over the whole repo or another history scanner.
- **A broken `.gitattributes` refuses every commit.** The scanner asks git to read the repo, and if git
  rejects the repo's `.gitattributes`, the scan fails and the hook fails closed. Seen 2026-09-27: four
  trailing comments after attribute lines, which git does not allow, blocked every commit in one repo for
  three days. Put comments on their own lines.
- **A repo-local `core.hooksPath` wins over the global one.** The installer prints any repo where that is
  set. Remove the local setting or that repo is unscanned.
- **Skipping the hook is a decision, not a convenience.** `git commit --no-verify` bypasses everything
  above. If a team member needs it, they should be able to say why.
- **Upgrading the scanner** means changing `VERSION` and `SHA256` in `tools/secret-scan/install.sh`
  together, from the official release page, and re-running the installer.
