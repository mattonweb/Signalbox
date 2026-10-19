# Branches

The workflow is trunk-based. `main` is the trunk; everything else is short-lived and named by what it is for.
Where a repository's default branch has another name, read `main` here as that branch.

## 1. Names

```
<kind>/<topic>
```

- `kind` is `feature` or `hotfix`; `release` only under section 4, where the topic is a version.
- `topic` is lowercase kebab-case, three to six words, naming the change the way its pull-request title will:
  `feature/county-export-endpoint`, `hotfix/paging-ceiling-off-by-one`. It describes the change, not the
  files touched.
- No initials, no author segment, no date. The author and the date are on every commit already.
- No ticket number as the whole topic. `feature/47` is a private reference; `feature/47-vendor-row` names
  the work and keeps the reference.
- English, with United States spelling (`en-US`): `feature/color-picker`, not `feature/colour-picker`.
- Never two kinds in one name, and never a bare topic with no kind.
- Characters: lowercase ASCII letters, digits, hyphens, and the one slash between kind and topic. Dots only
  inside a release version. No spaces, underscores, colons, tildes or anything else git refuses. The test is
  `git check-ref-format --branch <name>`, which exits 0 for a name git accepts; the rules above are stricter
  than git, so passing the test is necessary, not sufficient.


**Valid**

- `feature/county-export-endpoint`
- `feature/47-vendor-row`
- `hotfix/paging-ceiling-off-by-one`
- `release/2026.1016.4`

**Invalid, and why**

| Name | Why |
|---|---|
| `Feature/County-Export-Endpoint` | uppercase |
| `feature/county_export_endpoint` | underscores |
| `feature/county export endpoint` | spaces |
| `feature/county-export:v2`, `feature/county~export` | characters git refuses; `git check-ref-format` exits non-zero |
| `feature/county.export.endpoint` | dots outside a release version |
| `feature/jd/county-export-endpoint` | author segment; the author is on every commit |
| `feature/47` | a ticket number as the whole topic |
| `feature/update`, `feature/changes` | says nothing about the change |
| `county-export-endpoint` | no kind |
| `feature/hotfix/paging-ceiling` | two kinds |
| `fix/paging-ceiling-off-by-one` | `fix` is a commit type, not a branch kind: `feature/` or `hotfix/` |
| `release/v2026`, `release/2026.10` | not a full version |

## 2. `feature/<topic>`

- Cut from the tip of `main`.
- Carries one topic: a feature, a fix found in normal work, a refactor, a build or dependency change, a
  documentation change. The *commits* carry the type; the branch prefix stays `feature/` whatever they are.
- Merges to `main` by one pull request, by merge commit, and is deleted after.
- Lives days. If it cannot merge in days it is carrying more than one change: split it.
- To bring it up to date when the pull request cannot merge cleanly, merge `main` into it (a person's act until
  the pipeline's merge lane exists; a coding agent hands the branch back instead, see `author-loop.md`). Never rebase a
  pushed branch, never amend a pushed commit, and never force-push: all three rewrite history someone else
  may already hold. A mistake in a pushed commit is fixed by a new commit. Before the first push the history
  is yours alone: reordering, squashing a fix into the commit it fixes, or rewording there is fine.
- Before cutting it: the working tree is clean, `main` is at the remote tip, and the name does not already
  exist locally or on the remote.

Example, end to end:

```
git switch main
git pull --ff-only
git status --porcelain                       # must print nothing
git show-ref --quiet feature/county-export-endpoint && echo "name exists"   # must print nothing
git switch -c feature/county-export-endpoint
# ... work, commit in the house form ...
git push -u origin feature/county-export-endpoint
# open a draft pull request; mark ready when complete (auto-complete only once the pipeline mergesly once the pipeline merges)
# after the merge:
git switch main
git pull --ff-only
git branch -d feature/county-export-endpoint
git push origin --delete feature/county-export-endpoint   # unless the host deleted it on merge
```

## 3. `hotfix/<topic>`

- A fix to something already released that cannot wait for the next release from `main`.
- Cut from the line it fixes: `main` when the released line *is* `main`, or a release branch (section 4) when
  the fix is for an older major.
- Merges back to that line by pull request. When the line was a release branch, a second pull request brings
  the same fix to `main` so the trunk never lacks a fix the field has; its branch is usually a cherry-pick of
  the first branch's commits (`git cherry-pick <sha>`), which keeps the two fixes identical and the record
  linked.
- The commits inside are `fix(...)` as a rule; a hotfix that needs a `feat` commit is not a hotfix.

Example, on `main`:

```
git switch main
git pull --ff-only
git switch -c hotfix/paging-ceiling-off-by-one
# ... fix, commit ...
git push -u origin hotfix/paging-ceiling-off-by-one
# pull request to main; the pipeline tags the merge like any other
```

## 4. `release/<version>` (option)

A release branch keeps one already-shipped release patchable after `main` has moved on. It is cut at that
release's tag and named after the full version: `release/2026.1016.4`. It takes `hotfix/` branches only, each
merged by pull request and tagged by the pipeline with its own build date (`tags-and-releases.md`, section 4),
and it is deleted when the release is no longer supported. It is never where a release is made.
