# git-guardrail: a PreToolUse hook that stops an agent running git commands its working agreement forbids

A Claude Code hook, one Python file with no dependencies beyond the standard library. It runs before every
`Bash` and `PowerShell` tool call, parses the command, and denies the call with a reason when a git command
in it matches the active profile. The agent sees the reason and the command never runs. Everything else
passes through untouched.

It lives here once and reaches each repo by a Windows junction at `.claude\hooks\git-guardrail`, the same
way the shared skills reach `.claude\skills\` ([REPO-SETUP.md](../../REPO-SETUP.md)). A copy would drift;
a junction cannot.

Precedent: `mattpocock/git-guardrails-claude-code`, a bash script that greps nine patterns and exits 2. This
one keeps its shape (PreToolUse on the shell tools, deny with a reason) and replaces the grep with a parse,
because `git -C <path> push`, `git.exe push`, `bash -c "git push"` and `git clean -xdf` all slip past a
pattern list, while `echo "git push"` and `git log --grep="git push"` get caught by one.

## Files

| File | What it is |
|---|---|
| `git_guardrail.py` | the hook |
| `test_git_guardrail.py` | 213-row rule table (82 allow, 131 deny), a 54-row branch-pinned table for the `branch` profile (21 allow, 33 deny), plus payload, fail-open, subprocess and real-repository tests |
| `settings-snippet.handback.json` | the `hooks` block to merge into a repo's `.claude/settings.json` for the `handback` profile |
| `settings-snippet.branch.json` | the same for `branch` |
| `settings-snippet.author.json` | the same for `author` |

Run the tests from this folder:

```bash
python -m unittest test_git_guardrail -v
```

## What it does

Claude Code runs the hook before every `Bash` and `PowerShell` tool call with the call's JSON on stdin.
The hook splits the command on `&&`, `||`, `;`, `|`, `&`, newlines and command substitution, strips
wrappers (`env`, `exec`, `sudo`, `timeout 30`, `VAR=x`), looks inside `bash -c "..."` / `powershell
-Command "..."` / `eval "..."`, finds every git invocation (any spelling: `git`, `git.exe`, a full path,
`& git` in PowerShell) and applies the active profile. A hit prints

```json
{"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
  "permissionDecisionReason": "git push is blocked by the git-guardrail hook (handback profile, rule: push). This repository's working agreement human-gates git history: ..."}}
```

and exits 0; Claude Code refuses the call and shows the reason to the agent. Anything else: no output,
exit 0, normal permission flow. `--log <file>` appends one JSON line per deny (timestamp, cwd, tool,
command, reason) so a wired repo's denies can be read after the fact.

**Fail open on the payload, fail closed on the command.** An unreadable payload, a non-shell tool, a
missing command: silence, never a block. A command string with an unbalanced quote: a cruder tokeniser,
so a stray quote cannot hide a push. A hook that swallows a bad payload looks broken when it is correct,
so debug-print the parse before concluding it missed.

## Profiles

| rule | `handback` (human-gated history) | `branch` (main locked, work on `feature/*` and `hotfix/*`) | `author` (commits its own work) |
|---|---|---|---|
| `commit`, `cherry-pick`, `revert`, `am` | always denied | allowed only when the target repository's checked-out branch is `feature/*` or `hotfix/*`; denied on `main`, `master`, `release/*` or any other name, and denied when the branch cannot be read | allowed |
| `merge`, `rebase` | always denied | always denied | allowed |
| `push` | always denied | denied when forced or deleting (as `author`), when wholesale (`--all`, `--branches`, `--tags`, `--follow-tags`, `--prune`), or when any destination ref is not `feature/*` or `hotfix/*`. A bare `git push`, `git push origin`, `HEAD` or `-u origin HEAD` resolves to the checked-out branch and is denied when that cannot be read; `origin main`, `x:main`, `HEAD:main` are denied outright | denied only when forced or deleting: `-f`, `--force*`, `--delete`, `-d`, `--mirror`, `+ref`, `:ref` |
| `reset` | always denied | always denied | denied only `--hard`, `--merge`, `--keep` (soft and mixed allowed) |
| `clean` | denied with `-f`/`--force` unless `-n`/`--dry-run` | same | same |
| `branch` | denied with `-D`, `-f`, `--force` (`-d` allowed) | same | same |
| `checkout` | denied for `.`, `-f`/`--force`, `-B` | same | same |
| `restore` | denied for `.`, `:/`, `*` (`--staged <file>` allowed) | same | same |
| `stash` | denied `drop`, `clear` | same | same |
| `tag` | denied `-d`/`--delete`, `-f`/`--force` | same | same |
| `remote` | denied `remove`, `rm`, `set-url`, `prune` | same | same |
| `config` | denied `alias.*`, and `--global`/`--system` writes (reads allowed) | same | same |
| `worktree` | denied `remove --force`, `prune` | same | same |
| `reflog` | denied `expire`, `delete` | same | same |
| `gc --prune`, `prune`, `filter-branch`, `filter-repo`, `replace`, `update-ref` | denied | same | same |

Everything not named is allowed: `status`, `diff`, `log`, `show`, `fetch`, `pull`, `add`, `switch`,
`checkout <branch>`, `checkout -b`, `branch -d`, `stash push/pop/list`, `clean -n`, `tag <name>`,
`worktree add`, `remote add`, `config <key> <value>` in the repo, `--version`, `help`.

**How `branch` learns the branch.** Only when a rule needs it (a commit, or a push whose destination is the
checked-out branch) the hook runs `git symbolic-ref --short HEAD` with a 10-second timeout, in the directory
named by `git -C <path>` when the command carries one (joined onto the payload's cwd, as git does), else in
the hook payload's `cwd`. Any failure (not a repository, detached HEAD, git missing, timeout) reads as
unknown, and unknown denies with a reason that says the branch could not be read. `git status`, `git push
origin feature/x` and every other profile never start that process. `--explain` takes `--cwd` for the same
purpose. The deny reason names the branch it found (`rule: commit on main, not feature/* or hotfix/*`), so a
wrong read is visible in the transcript.

The rule table in `test_git_guardrail.py` is the specification; this table is derived from it. Change
the rule, add the row, run the tests. The coverage test fails when a rule has no denying row.

## Which profile

Read the repo's working agreement (its `CLAUDE.md`) and pick the profile that matches it; the hook is a
floor under the agreement, never a replacement for it.

| profile | for |
|---|---|
| `handback` | a repo whose working agreement human-gates every commit: the agent leaves work in the tree and a person commits |
| `branch` | a repo whose `main` is protected and takes pull requests only, where the agent may commit and push on `feature/*` and `hotfix/*` as `devops-git-conventions` describes, and nothing else |
| `author` | a project folder whose agent commits and pushes its own work by routine; only the destructive shapes are denied |

A hook comes from the project directory the session was started in (plus the user-level settings). A
session started in folder A that runs `git -C <repo B> push` is governed by A's profile, not B's. If that
matters, install `author` at user level in `~/.claude/settings.json` as the floor for every session on the
machine, with the repo's own profile on top; denies combine, and a deny wins.

## Install in one repo (attended)

Wiring a repo is an attended act: the settings file is the control plane, and a hook edit made while an
agent is mid-session is invisible to it until its next session.

1. Create the junction, from this repo's `tools` folder:

   ```powershell
   .\New-SkillJunction.ps1 -AgentHome C:\Source\MyApp -SkillName git-guardrail -Source Signalbox -Kind Hook
   ```

   It creates `C:\Source\MyApp\.claude\hooks\git-guardrail` pointing at this folder and adds
   `.claude/hooks/git-guardrail/` to the repo's `.gitignore`. The junction is never committed; the
   gitignore line is.

2. Merge the `hooks` block from the matching snippet into `C:\Source\MyApp\.claude\settings.json`. If the
   file already has a `hooks.PreToolUse` array, add this as another group in it; groups are independent and
   a deny from any one of them wins. The command, shell form:

   ```
   python "${CLAUDE_PROJECT_DIR}/.claude/hooks/git-guardrail/git_guardrail.py" --profile branch --log "$HOME/.claude/git-guardrail-denies.jsonl"
   ```

   `python` must resolve to Python 3 under Git Bash on the machine; nothing else is needed.

3. Start a new session in the repo, or open `/hooks` once from an interactive `claude` terminal. The
   settings watcher may not pick up a mid-session edit.

4. Verify (next section). In a public repo `.claude/settings.json` is committed and therefore public; the
   hook holds no keys, no names, nothing about infrastructure.

Pulling this repo updates the hook in every repo junctioned to it. Add `git -C C:\Source\Signalbox pull
--ff-only` to whatever a session start already does, as REPO-SETUP.md says for the skills.

## Verify a wired repo (the discriminator)

In a session started in that repo, ask the agent to run:

```bash
git push --dry-run
```

- **Denied, reason visible in the transcript, naming the profile you expected**: wired.
- **The command runs** (a dry run is harmless either way): not wired, or wired with a profile that allows
  a plain push (`author`, or `branch` on a `feature/*` branch). Read the profile name in the snippet you
  merged and the branch the repo has checked out.
- **A `hook error` notice marked non-blocking**: the hook crashed (wrong path, no python) and failed open.
  That is the test not running, not a pass.

Offline checks, from the repo:

```bash
python .claude/hooks/git-guardrail/git_guardrail.py --profile branch --explain "git push"
```

prints `DENY ...` and exits 1 on `main`; `ALLOW` and exit 0 for `git status`. To pipe a real payload, build
it with Python, not a shell string: a shell can collapse `\\` to `\` inside a quoted JSON literal, the hook
then sees invalid JSON and correctly stays silent, which looks like a miss.

```bash
python -c "import json;print(json.dumps({'tool_name':'Bash','tool_input':{'command':'git push'}}))" | python .claude/hooks/git-guardrail/git_guardrail.py --profile handback
```

## Known limits (the hook is a reminder with teeth, not a wall)

- Not inspected: git run from another interpreter (`python -c "subprocess.run(['git','push'])"`, `node
  -e`, `dotnet run`), git read from a pipe (`echo push | xargs git`), a script file that contains git
  commands (`./deploy.sh`), git aliases that existed before install (`alias.*` writes are denied from
  then on).
- Not covered: commits made outside the tools (an IDE, a terminal), which is where a person's own commits
  happen, by design; sessions started in a different folder with this repo as an additional directory (they
  carry their own folder's profile).
- The `branch` profile reads the branch where the payload says the command runs, or under `git -C <path>`.
  A `cd <dir> && git commit` inside one command is judged by the payload's cwd, not `<dir>`; `-C` is the
  supported way to point at another repository. In PowerShell, quote a `-C` path that carries backslashes
  (or use forward slashes): the tokeniser follows POSIX quoting, so an unquoted `C:\Source\Repo` loses its
  backslashes, the branch cannot be read there, and the command is denied (closed, not open).
- False positives: a heredoc or multi-line string whose own line starts with `git push` reads as a
  command; re-issue with the text in a file. `git commit -m "docs: never git push"` is fine (quoted
  strings are only opened under `bash -c`, `sh -c`, `pwsh`, `powershell`, `cmd`, `eval`, `xargs`).
- The deny reason is advice to the agent. The working agreement is the rule; this hook makes the first
  slip visible and cheap instead of silent.
