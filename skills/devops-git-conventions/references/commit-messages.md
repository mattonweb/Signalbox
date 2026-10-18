# Commit messages

The house form is [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/), with one
added type and a tighter subject rule. Every commit in every repository that adopts this skill is written
this way, whoever writes it, person or agent. A commit is a line in a record people search for years; the
form is what makes the search work.

## 0. One change per commit

A commit holds one logical change, and the message describes that change. Before writing the message, read
what is actually staged, `git diff --cached`, not the working tree, and apply one test: **say what the staged
change is in one or two sentences.** If that cannot be done, the commit is too big or it is two commits.

Split when a diff mixes any of these:

- a feature and a refactor;
- a formatting change and a logic change;
- tests and production code, unless the test is the one that proves the change;
- a dependency bump and a behavior change;
- two areas that a reader would review separately.

Mixed hunks in one file are split with `git add -p`. Three things to look for in the staged diff before the
message is written, because they arrive uninvited: a secret, a debug log line, formatting churn in lines the
change did not need to touch.

And some things never belong in any commit: build output, environment files, local editor settings, and
anything a `.gitignore` already names. Generated files are committed only when the project expects them, a
lock file or a migration, for instance. `.gitignore` is maintained like code, and `git check-ignore -v <path>`
is the test for whether a path is covered.

## 1. Shape

```
<type>(<scope>)!: <subject>
<blank line>
<body>
<blank line>
<footers>
```

- `type` is required and comes from the table in section 2.
- `(scope)` is optional, one scope only, from the repository's own list (section 3).
- `!` marks a breaking change and is paired with a `BREAKING CHANGE:` footer (section 5).
- `subject` is required (section 4).
- `body` and `footers` are optional for a trivial change and expected for anything else (section 5).

## 2. Types

| Type | Use when the change... | Example |
|---|---|---|
| `feat` | adds or changes behavior a user or a caller can see | `feat(catalog): add the county export endpoint` |
| `fix` | corrects behavior that was wrong | `fix(logging): reap stale activities on the UTC clock` |
| `content` | changes published page copy or data-driven text, not code and not developer docs | `content(rankings): disclose the smallest towns on the elevation pages` |
| `docs` | changes prose read by people: README, working agreements, skills, design notes | `docs(skills): add the review checklist to the C# standards skill` |
| `style` | changes formatting only, with no change to meaning (whitespace, modifier order) | `style: apply the house modifier order` |
| `refactor` | restructures code without changing behavior | `refactor(catalog): split the loader into reader and writer` |
| `perf` | makes the same behavior faster or cheaper | `perf(forecast): cache the station index per run` |
| `test` | adds or changes tests only | `test(catalog): pin the installer to the fetchers the library ships` |
| `build` | changes how the code compiles, packages or is analyzed: project files, dependencies, `.editorconfig`, `.gitattributes`, `Directory.Build.props` | `build(deps): update NuGet packages to clear the audit advisories` |
| `ci` | changes pipeline files or their callers | `ci: pin the shared pipeline caller to v1.2.0` |
| `chore` | changes repository housekeeping that is none of the above: `.gitignore`, settings, skill junctions, templates | `chore(skills): junction the C# standards skill from its home` |
| `revert` | reverts an earlier commit (section 6) | `revert: feat(catalog): add the county export endpoint` |

`content` is the house addition. It exists because a repository that publishes a site changes its copy as often
as its code, and `docs` would misdescribe that change: `docs` is read by developers, `content` is read by the
public.

**Nothing else is a type.** A word that names the area the change touched is a scope, not a type:
`chore(skills):`, `chore(gitignore):`, `build(gitattributes):`, never `skills:` or `gitignore:`.

## 3. Scope

- One scope, lowercase, kebab-case, taken from the list in the repository's own working agreement
  (`CLAUDE.md`). The list is the repository's; this skill carries none, because it is shared.
- Omit the scope when the change spans the repository or no listed scope fits. Do not invent one in the
  commit; add it to the list first, in its own commit.
- Never two scopes. Name the scope the change is *for*, and let the body say what else it touched.

## 4. Subject

- Imperative mood, as an instruction to the codebase: *add*, *remove*, *pin*, *read*, not *added* or *adds*.
- Lowercase first letter after the colon; no full stop at the end.
- **The whole first line, prefix included, is at most 72 characters.** Everything else goes in the body.
  The reasoning, the date and who directed the change are the usual overflow; all three belong in the body.
- English, with United States spelling (`en-US`), in the subject and the body alike: *behavior*, not
  *behaviour*; *initialize*, not *initialise*. One spelling is what keeps a search of the log complete.
- Say what the commit does, not what the task was: `fix(ats): size the paging ceiling to the board's stated
  total`, not `fix(ats): ticket 47`.


**Valid**

- `feat(catalog): add the county export endpoint`
- `fix: read the paging counter when the board omits a total`
- `build(deps): update NuGet packages to clear the audit advisories`

**Invalid, and why**

| First line | Why |
|---|---|
| `Add the county export endpoint` | no type |
| `feat: Add the county export endpoint.` | capital after the colon; full stop |
| `feat(catalog): added the county export endpoint` | past tense, not imperative |
| `feat(catalog,www): add the county export endpoint` | two scopes |
| `feature(catalog): add the county export endpoint` | `feature` is not a type |
| `skills: junction the standards skill` | `skills` is a scope, not a type: `chore(skills):` |
| `fix(catalog): ticket 47` | names the task, not the change |
| `feat(catalog): add the county export endpoint with the entitlement check, the CSV writer and the rate limit, 2026-10-16` | over 72 characters; three changes; a date in the subject |
| `feat(catalog)!: add the county export endpoint` with no `BREAKING CHANGE:` footer | `!` and the footer travel together |
| `wip`, `fixes`, `more changes` | not a commit message |

**Enforced by a hook.** Sections 2 to 4 are checked on the committer's machine by
`scripts/commit-msg-hook.sh`, a `commit-msg` hook that refuses a first line that breaks them. It does
not read the body and cannot judge whether the subject is a good one.

## 5. Body and footers

- Wrap at 72 characters.
- The body says **why**, and what a reader would not get from the diff: the rule being applied, the decision
  it rests on, the constraint that shaped it. It does not narrate the diff.
- **The last paragraph of the body names the verification**, in one line:
  `Verified: dotnet build 0 warnings, 0 errors; 587 tests green.` A commit with no verification line was not
  verified, and the reader should be able to tell.
- Provenance goes in the body, never the subject: who asked for the change, and when, if a later reader
  would need it.
- Footers are `Token: value` lines after a blank line. The ones in use:
  - `BREAKING CHANGE: <what breaks and what a caller does about it>`, paired with `!` in the first line.
    A library repository takes this seriously: every consumer pins a version and reads this footer to decide
    whether to move.
  - `Refs: #60` for the pull request or issue the commit belongs to; `Closes: #123` when the host should
    close that issue on merge.
  - `Co-authored-by: Name <email>` when two people wrote it.

## 6. Reverts

```
revert: feat(catalog): add the county export endpoint

This reverts commit 1a2b3c4d. The endpoint returned rows the caller
was not entitled to; the fix needs the entitlement check that lands
with the next feature, so the endpoint comes out until then.

Verified: dotnet build 0 warnings, 0 errors; 580 tests green.
```

The subject is `revert:` followed by the original subject verbatim. The body names the commit and says why.
On `main` or any pushed branch, a revert is the only undo: never a reset, never an amend, never a force-push.
History that others hold is appended to, not rewritten.

## 7. Worked examples

**A feature with a body:**

```
feat(ats): add the Atom-feed list fetcher for one more vendor

The vendor publishes postings as an Atom feed with no JSON endpoint.
The fetcher reads the feed, maps each entry to a posting row and
registers itself with the installer like the other fetchers do.

Verified: dotnet build 0 warnings, 0 errors; 612 tests green,
including the installer pin test for the new fetcher.
```

**A build change, no scope needed:**

```
build: take the house .editorconfig and .gitattributes verbatim

The shared pipeline repository is the single source for both files.
A copy that differs is drift and is fixed there first, then copied
back. Both files are byte-identical to the source after this commit.

Verified: dotnet build 0 warnings, 0 errors; cmp exits 0 on both files.
```

**A breaking change in a library:**

```
feat(data)!: require a TimeProvider on every repository constructor

Repositories stamped rows from DateTime.UtcNow, which made every
time-dependent test flaky. The constructor now takes a TimeProvider
and the framework supplies the system one at registration.

Verified: dotnet build 0 warnings, 0 errors; 341 tests green.

BREAKING CHANGE: every class deriving from RepositoryBase must pass a
TimeProvider to the base constructor. Register TimeProvider.System
once in the host and the existing registrations compile.
```
