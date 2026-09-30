# Repo setup: reach a shared skill by junction point

One skill, one home, no copies. A skill lives in the repo whose owner maintains it, and every other repo
that needs it holds a Windows junction point at `.claude\skills\<name>` pointing at that folder. Every
agent then reads the same bytes, always the latest the owner has committed and your machine has pulled.
The worked example below is the Roadbed skill, `code-roadbed-csharp`, which lives in the public
[Roadbed](https://github.com/mattonweb/Roadbed) repo at `skills\code-roadbed-csharp`.

Machine-level setup (the secrets pre-commit hook) is in [COMPUTER-SETUP.md](COMPUTER-SETUP.md). Do that
once per computer; this document is once per repo.

## Why a junction and not a copy

- Four repos held copies of the Roadbed skill in September 2026. Three matched. One was five months
  stale, a third shorter, with four reference files missing, and nothing had noticed. A copy drifts
  silently. A junction cannot.
- The skill changes in the same commit as the code it documents. A copy in another repo lags by design.
- The owner can only edit a file in a repo they work in. A copy behind someone else's wall is unowned.

## What you need

- Windows. Junctions are a Windows feature and need no administrator rights.
- Windows PowerShell 5.1 or later.
- The skill's home repo cloned on this machine, by default under `C:\Source`. For the example:
  `C:\Source\Roadbed`, containing `skills\code-roadbed-csharp\SKILL.md`.
- Your own repo cloned, for example `C:\Source\MyApp`.

## Steps

### 1. Clone or pull the home repo

```powershell
git -C C:\Source\Roadbed pull --ff-only
```

If you do not have it yet, clone it under `C:\Source` so the default paths in the script apply.

### 2. Create the junction

From this repo's `tools` folder:

```powershell
.\New-SkillJunction.ps1 -AgentHome C:\Source\MyApp -SkillName code-roadbed-csharp -Source Roadbed
```

If your clones live somewhere other than `C:\Source`, add `-SourceRoot D:\Repos` or wherever they are.

The script does two things and reports each:

```
Created junction: C:\Source\MyApp\.claude\skills\code-roadbed-csharp -> C:\Source\Roadbed\skills\code-roadbed-csharp
Added to C:\Source\MyApp\.gitignore: .claude/skills/code-roadbed-csharp/
```

It will not overwrite a real folder. If your repo already holds a copy of the skill, the script stops and
tells you. Delete or rename the copy first, then run it again.

⛔ **If the copy is tracked by git, the order is: remove the copy, commit, merge, pull, and only then
create the junction.** Git deletes and restores tracked files *through* a junction. If a junction exists
while any branch you check out still tracks the copy, the next pull, merge or branch switch that removes
those files removes them from the skill's home. Seen 2026-09-29: a pull that merged a copy's deletion
emptied all seventeen files from the home repo's folder, which was restored only because the home tracks
them. The script refuses to create a junction while the current branch still tracks files at that path,
and that refusal is the guard, not an obstacle. On a machine where the repo is public and the removal
travels by pull request, wait for the merge and pull it before running the script.

### 3. Commit the gitignore line

The junction itself is never committed. It is a pointer that only exists on this machine. The gitignore
line is what keeps git from treating the junction's contents as files in your repo.

```powershell
git -C C:\Source\MyApp add .gitignore
git -C C:\Source\MyApp commit -m "gitignore: junction point to the shared code-roadbed-csharp skill"
```

### 4. Verify

That it is a junction and points at the home:

```powershell
Get-Item C:\Source\MyApp\.claude\skills\code-roadbed-csharp -Force | Select-Object LinkType, Target
```

That git ignores it:

```powershell
git -C C:\Source\MyApp status --porcelain
```

Nothing should be listed under `.claude/skills/`.

That the agent sees it: open a Claude Code session in `C:\Source\MyApp` and ask what skills are
available. `code-roadbed-csharp` should be in the list. This was tested on 2026-09-27: a repo with no
copy of the skill listed it the moment the junction existed.

### 5. Keep it current

Pulling your own repo does not pull the home repo. Add the home repo's pull to whatever your session
start already does, or the junction serves stale bytes while looking fresh:

```powershell
git -C C:\Source\Roadbed pull --ff-only
```

## The home repo needs a junction too

Claude Code discovers skills only under `.claude\skills\`. A skill stored at a repo's root `skills\` folder
is not found by that repo's own agent. Tested 2026-09-27: Roadbed's agent did not list its own skill until
this junction existed:

```powershell
.\New-SkillJunction.ps1 -AgentHome C:\Source\Roadbed -SkillName code-roadbed-csharp -Source Roadbed
```

The root `skills\` folder is the storage location. `.claude\skills\` is the discovery location. Both are
needed, and the junction joins them.

## Rules

- **Never copy a skill into another repo.** A copy is the drift this document exists to remove.
- **Decide where a skill lives when it is created.** Moving it later changes its path and breaks every
  junction pointing at it until each machine is re-pointed.
- **Every skill's `SKILL.md` carries `owner:` and `version:` in its frontmatter.** The owner is the one
  party who edits it. The version is what a reviewer and an author can both name, so a mismatch is
  visible on a pull request.
- **A public repo's agent never junctions to a private repo.** If an agent's own repo is public,
  everything reachable from its session must be public too.

## Removing a junction

Use a non-recursive delete. It removes the junction and cannot touch the folder it points at:

```powershell
[System.IO.Directory]::Delete('C:\Source\MyApp\.claude\skills\code-roadbed-csharp', $false)
```

A recursive delete through a junction can empty the skill's home. Do not use `Remove-Item -Recurse` on a
junction.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Script says the target is a real folder | Your repo holds a copy of the skill | Delete or rename the copy, rerun |
| Script says the skill is not found | Home repo not cloned under `-SourceRoot`, or a typo | Clone it, or pass `-SourceRoot` |
| `git status` shows `.claude/skills/` as untracked | Gitignore line not yet committed on this branch | Commit it, or pull the branch that has it |
| Agent does not list the skill | Skill is at a root `skills\` folder with no junction under `.claude\skills\` | Create the home repo's own junction |
| Junction exists but the skill is stale | Home repo not pulled | Pull the home repo |
