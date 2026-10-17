---
name: devops-git-conventions
description: Git conventions for every repository that adopts this skill. Use whenever creating a branch, writing or reviewing a commit message, drafting a commit message for someone else to commit, opening or reviewing a pull request, or tagging a release. Covers the trunk-based workflow, branch naming, Conventional Commits with the type table and the 72-character subject rule, the verification line every commit body carries, pull-request title and description, merge by merge commit, and the tag and version format. A pipeline tags every merge and builds once from the tag; this skill states the half a person has to get right before the pipeline sees it.
owner: Matt
version: 1.0.0
visibility: public
---

# Git conventions

These are the house rules for git in every repository that adopts this skill. Each is written so a reviewer
can answer yes or no to it on a branch, a commit or a pull request. Where the pipeline enforces a rule, this
document names the step; where nothing enforces it, this document is the rule.

**Grounding.** The workflow in section 1 is the one a build-once, promote-by-digest pipeline assumes: a
trunk, feature branches, a pull request per branch, a tag on every merge, one artifact built once from the
tag. This skill names the conventions such a pipeline leaves to the people and agents working in the
repositories. Where history disagrees with a rule here, the rule wins and history is left alone: nothing is
rewritten.

**Who it binds.** Everyone who writes to a repository that adopts it, person or agent. It says nothing about
who may commit or push; that is each repository's working agreement.

## 1. The workflow: trunk-based

- `main` is the trunk. It is always releasable: every merge to it is tagged by the pipeline and tested on QA
  in tag order, and only a tag that passed reaches production.
- Work happens on a short-lived branch cut from `main`, reaches `main` by one pull request, and the branch is
  deleted after the merge. A branch lives days, not weeks; a branch that cannot merge in days is carrying
  more than one change. Incomplete work that must ship reaches `main` behind a feature flag, not on a branch
  that waits for weeks. Where a repository's default branch has another name, read `main` as that branch.
- Nothing is merged *into* a feature branch to bring it up to date except `main` itself, and only when the
  pull request cannot merge cleanly.
- There are two kinds of branch beyond `main` (section 2) and, as an option for keeping one shipped release
  patchable, a release branch (section 2.3).

**Language.** Everything in these four artifacts is written in English with United States spelling
(`en-US`): branch topics, commit subjects and bodies, pull-request titles and descriptions. One spelling
across every repository is what keeps `git log --grep` and a search of pull requests finding every instance.
Assistants drafting any of them default to other spellings often enough that this is a rule, not a
preference: *behavior*, *analyze*, *color*, *initialize*, *canceled*.

## 2. Branches

Full syntax, lifetimes and examples in `references/branches.md`.

### 2.1 `feature/<topic>`

Every change that is not a hotfix: a feature, a fix found in normal work, a refactor, a build change, a
documentation change. The prefix is `feature/` whatever the commit type inside it; the commits carry the
type.

### 2.2 `hotfix/<topic>`

A fix to something already released that cannot wait for the next release from `main`. It is cut from the
line it fixes (`main`, or a release branch) and merges back to that line by pull request. A hotfix to a
release branch is then brought to `main` by a second pull request, so the trunk never lacks a fix the field
has.

### 2.3 `release/<version>` — an option, not a default

A release branch exists only to keep one already-shipped release patchable after `main` has moved on. It is
cut at that release's tag, named after the full version (`release/2026.1016.4`), takes hotfix branches only,
and is deleted when the release is no longer supported. It is never where a release is *made*: a release is
a tag and the artifact the pipeline built from it. A branch is mutable by nature and cannot make an artifact
immutable; the tag, the digest and the release record do that (section 5).

## 3. Commits

Full form, type table, scope rule, body, footers and worked examples in `references/commit-messages.md`.

- Conventional Commits: `<type>(<scope>)!: <subject>`, body, footers.
- Twelve types: the eleven in the specification's common set plus `content`, the house type for published
  copy. Nothing else is a type.
- One logical change per commit; the message describes the staged diff. If the staged change cannot be
  said in one or two sentences, it is two commits.
- One scope at most, from the repository's own list in its working agreement. The list is never in this
  skill.
- The first line, prefix included, is at most 72 characters. Lowercase after the colon, imperative, no full
  stop.
- The body says why, and its last paragraph is the verification line.
- Provenance (who asked, when) goes in the body, never the subject.

## 4. Pull requests

Full form in `references/pull-requests.md`.

- One branch, one topic, one pull request. Title in commit-subject form, at most 72 characters.
- Description in four parts: why, what, verification, follow-ups.
- Opened as a draft with auto-complete on, from a fully committed and pushed branch; marked ready when
  complete. `references/pull-request-template.md` gives the host the four headings to pre-fill.
- Merged by merge commit, never squash, never rebase. The merge commit is the host's and is the one commit
  on `main` exempt from the message form.
- Branch deleted after merge.

## 5. Tags and releases

Full form, the reasoning and the three accepted parser facts in `references/tags-and-releases.md`.

- The version scheme is SortVer, sortable versioning: `YYYY.MMdd.N[+hotfix]`, the build year, the
  zero-padded month and day, the build sequence within that day from 1, and `+hotfix` only on a release built
  from a `hotfix/` branch. The tag is the version string with no prefix. It sorts as text in time order; a
  parser may drop the leading zero of the month and that rendering is the same version.
- Every merge to `main` is tagged, by the pipeline, with the pipeline's credential, never by hand. The tag
  is the name of a release; the commit it points to is the identity of the source; the content digest of the
  artifact built from it is the identity of the deployable. All three travel together in the release record.
- A tag is never moved and never deleted. The host's tag protection enforces the first where the host has
  it; the pipeline's artifact store enforces the second regardless of host.
- The version lives in the tag and the build stamps it into the artifact. A version is not hand-edited in a
  project file.
- A version never says whether a change is breaking. The commit's `!` and `BREAKING CHANGE:` footer do.

## 6. What a local hook enforces

Where a tool can enforce a rule, the tool enforces it and this document names the tool. Two rules here are
enforceable at commit time on the committer's machine; the rest need a reviewer.

- **No secret in a commit.** A machine-wide `pre-commit` hook scans the staged changes and refuses the
  commit on a hit, failing closed when the scanner is missing. The install, the test that proves it refuses,
  and how it chains to a repository's own hook are in this repository's `COMPUTER-SETUP.md`. A commit that
  the scan refused is fixed by moving the value out of the repository, never by skipping the hook.
- **The commit-message form.** `references/commit-msg-hook.sh` is a `commit-msg` hook that checks the first
  line against sections 2 to 4 of the commit-messages reference: a known type, at most one scope, the `!`
  and colon, lowercase after the colon, at most 72 characters, no trailing full stop. Merge commits and
  `fixup!`/`squash!` lines are exempt. It checks form, not meaning: a subject that passes can still be a
  bad subject, which is what the checklist below is for.
- **A hook never changes what is being committed.** Formatting is the editor's job through `.editorconfig`;
  a hook that rewrites staged files makes the commit differ from what the author reviewed. Hooks here refuse
  or pass, nothing else.
- `--no-verify` is not a workflow. A hook that is wrong is fixed; a hook that is right is obeyed.

## 7. Review checklist

A reviewer answers each of these on the thing in front of them.

**On a branch**

- [ ] Named `feature/<topic>` or `hotfix/<topic>` (or `release/<version>` under section 2.3), topic in
      lowercase kebab-case, no initials, no ticket number as the whole name.
- [ ] Cut from the line it will merge to, from a clean tree, with a name that did not already exist.
- [ ] Passes `git check-ref-format --branch`; no dots outside a release version.
- [ ] One topic.

**On a commit**

- [ ] One logical change, and the message describes what was staged.
- [ ] First line is `<type>(<scope>)!: <subject>` with a type from the table and at most one scope.
- [ ] First line is at most 72 characters, lowercase after the colon, imperative, no full stop.
- [ ] Body present for anything beyond a trivial change; wrapped at 72; says why.
- [ ] Last paragraph of the body is the verification line.
- [ ] `!` and `BREAKING CHANGE:` present together or absent together.
- [ ] No provenance or date in the subject.
- [ ] English, `en-US` spelling, subject and body.

**On a pull request**

- [ ] Title in commit-subject form, at most 72 characters.
- [ ] Description has why, what, verification; follow-ups when any.
- [ ] One branch, one topic.
- [ ] English, `en-US` spelling, title and description.
- [ ] Will merge by merge commit.

**On a tag**

- [ ] Created by the pipeline, not by hand.
- [ ] `YYYY.MMdd.N`, month and day zero-padded, no `v` prefix; `+hotfix` exactly when the merged branch was
      a `hotfix/` branch, never a `-` suffix.

## Decisions record

| Date | Decision | Ruled by |
|---|---|---|
| 2026-10-15 | Conventional Commits adopted for every commit, hand-written ones included. | maintainer |
| 2026-10-15 | `content` added as the twelfth type; nothing else is a type. | maintainer |
| 2026-10-15 | Scopes are per repository, listed in that repository's working agreement, never here. One scope per commit. | maintainer |
| 2026-10-15 | 72-character first line, hard. Reasoning and provenance move to the body. | maintainer |
| 2026-10-15 | The body's last paragraph is the verification line. | maintainer |
| 2026-10-15 | Trunk-based; only `feature/` and `hotfix/` beyond `main`. | maintainer |
| 2026-10-16 | No size ceiling on a commit or a pull request. One topic is the rule; a large refactor is one topic. | maintainer |
| 2026-10-16 | Commits are not required to be signed. Review is the gate, not the signature; revisit if a host or a consumer asks for verified commits. | maintainer |
| 2026-10-16 | Two rules are hook-enforced: the secret scan (machine-wide, documented in COMPUTER-SETUP.md) and the commit-message first line (`references/commit-msg-hook.sh`); hooks refuse or pass and never rewrite staged content. | maintainer |
| 2026-10-16 | Release branches exist as an option, named after the full version they keep patchable (`release/2026.1016.4`), hotfix-only, deleted when unsupported. | maintainer |
| 2026-10-16 | Version `YYYY.MMdd.N[+hotfix]`, named SortVer (sortable versioning; no existing scheme of that name found 2026-10-16), a calendar and semantic hybrid chosen to sort as text in time order. Accepted with it: a parser may drop the month's leading zero and that is the same version; `+hotfix` as build metadata, never `-hotfix`, which would be a pre-release; the build sequence is not padded. | maintainer |
| 2026-10-15 | Pull requests merge by merge commit. | maintainer |
| 2026-10-15 | No changelog file until a release process exists to feed one. | maintainer |
| 2026-10-15 | The skill binds everyone who writes to an adopting repository and says nothing about who may commit. | maintainer |
