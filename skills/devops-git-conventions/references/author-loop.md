# The author loop: how a coding agent delivers a change

This is the author's half of the pipeline (steps S10 to S16 and S20 to S24 of the step contract) as a coding agent
runs it today, before the pipeline owns merging. A repository's `CLAUDE.md` points here and adds only what is
specific to that repository: the name of its GitHub App, what a merge changes downstream, and whether `main` is
protected on the host yet. Everything below applies to every repository that has adopted this skill and has an
app identity. Where this reference and a repository's `CLAUDE.md` disagree, the `CLAUDE.md` wins for that
repository and the disagreement is reported in the next hand-back.

## 1. The loop, in order

1. **Start from `main`.** `git fetch --prune origin`, then `git pull --ff-only` on a clean `main`. If you are on a
   `feature/` or `hotfix/` branch, that is your own unfinished work from an earlier session: say so in your first
   message, name the branch and its pull request if one is open, and carry on with it before claiming anything new.
   If the tree is dirty on `main`, or a fast-forward is refused, stop and say what `git status` showed.
2. **Branch.** `git checkout -b feature/<topic>` (or `hotfix/<topic>`), one topic per branch, named as section 2 of
   the skill says. Never work on `main`.
3. **Commit as you go**, in the house form (section 3 of the skill): `<type>(<scope>): <subject>`, subject at most 72
   characters, body with the why and a verification line last, scope from the repository's list. For a message longer
   than the subject, write it to a file in your session's scratchpad and run `git commit -F <that file>`. Your
   commits are authored as the app automatically; never set `user.name` or `user.email` yourself. A machine hook
   refuses a subject that breaks the form and a commit that carries a secret: fix the cause, never bypass
   (`--no-verify` is not a workflow).
4. **Build green and run the tests before you push.** Record the counts; the hand-back states them.
5. **Push the branch and open a DRAFT pull request in the same hand-off**: `git push -u origin feature/<topic>`, then
   `gh pr create --draft --title "<the commit subject form>" --body-file <a file in your scratchpad>`. The
   description has four parts, why, what, verification, follow-ups, in the shape of
   `assets/pull-request-template.md` in this skill. `gh` acts as the app in your session. Do not enable
   auto-merge; until the pipeline owns the merge lane a human merges.
6. **Wait for the checks once, without polling**: `gh pr checks <number> --watch` blocks until every check on the
   pull request has finished and exits non-zero if any is red. That one command is the sanctioned wait; do not
   read the checks repeatedly, and do not read run logs unless a check is red. Green means every check on the pull
   request, not only the first one listed.
7. **Read the review**: `gh pr view <number> --comments`. A red check or a requested change is yours: fix it, commit,
   push the same branch (never force), and the checks run again.
8. **When the work is complete and the checks are green, mark it ready**: `gh pr ready <number>`. A draft requests
   no reviewer; marking it ready is the moment the repository's code owner is asked for review, so nobody sees the
   pull request until this step. Then hand back on the hub, naming the pull request number, the branch, and the
   build and test counts before and after.
9. **After the merge** (you learn of it on the hub or by `gh pr view`): `git fetch --prune origin`, `git checkout
   main`, `git pull --ff-only`, then `git branch -d feature/<topic>`, which deletes only a branch that is fully
   merged. The remote branch is deleted by the person who merges.

## 2. What you never do

Push to `main`; merge, rebase or reset; force-push, amend or rewrite a pushed commit; delete a branch that is not
merged; merge, close or approve a pull request; enable auto-merge; run `gh auth login`; reach any repository but
your own through git or `gh`; change `.claude/settings.json`, your `CLAUDE.md` or any skill. The git-guardrail
hook refuses most of these before git runs, and a refusal that names a rule is a stop: hand back, do not retry.

**A branch that no longer merges cleanly with `main` is handed back, not repaired.** The skill's branches reference
allows merging `main` into a branch; for an agent that permission arrives with the pipeline's merge lane, not
before. Until then, say in the hand-back that the branch conflicts, and the person who merges resolves it.

## 3. Practicalities that earlier runs taught

- **Run `gh` from the Bash tool, never from PowerShell.** The identity reaches `gh` through a shim in `~/bin` that
  Git Bash puts first on its path; PowerShell resolves `gh` to the bare GitHub CLI, which then asks you to log in.
  Seen 2026-10-18: a second run opened its pull request from PowerShell and stopped at "please run gh auth login".
  That message from PowerShell means "use Bash", not "the wiring is broken".
- **Run every git command plain**: no redirects (`2>&1`, `> file`) and no pipes (`| tail`) on the line. The
  guardrail hook reads the whole line; a redirect token could once be mistaken for an argument, and a plain command
  is also the one whose output you actually read. If the hook refuses a command and its reason names the command's
  *shape* rather than a rule, re-issue it once in the plain form.
- **Reading another checkout.** "Reach any repository but your own" is about git remotes and tokens. Read-only
  reads of the local Signalbox checkout, the home of the skills and of the pipeline's workflows, are allowed when
  you need to know what a check runs; editing it never is. The pull-request check builds the whole solution in
  Release on an Ubuntu runner and runs every test, with no filter; the exact file is
  `.github/workflows/reusable-pull-request-checks.yml` in that checkout.
- **Addressing the hub.** The owner tag in a charter (`@CTOv2`) is the Action Items form; the hub's `hub_send`
  wants the roster's display name, `CTO v2`, with the space.
- **Provenance in a public repository.** The order's number (for example `BO-058`) may appear in a commit body and
  in the pull-request description. Nothing else about the company's agents, hubs or seats goes into a public
  repository's history.
- **Naming the change.** A pure rename or move with no behaviour change is `refactor`; `chore` is for maintenance
  that touches no product code.
- **If git or `gh` ever asks you to authenticate**, the identity wiring is broken: stop, report the exact command
  and the message, and do not try to log in.

## 4. What this loop does not yet cover

Review agents at S24 (a person reviews for now), the merge lane and tagging (S30 to S36), and the release and
deployment steps after them. When those arrive, this reference changes and the charters that point here do not.
