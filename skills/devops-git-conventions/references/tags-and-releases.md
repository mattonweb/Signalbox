# Tags and releases

A release is a tag on `main` and the one artifact built from it. The tag is the release's name, the commit it
points to is the identity of the source, and the content digest of the artifact is the identity of the
deployable. The three travel together in the release record; none of them is ever changed after the fact.

## 1. The version format: SortVer

```
<YYYY>.<MMdd>.<N>[+hotfix]
```

| Part | Rule | Examples |
|---|---|---|
| `YYYY` | the four-digit year of the build | `2026` |
| `MMdd` | month and day of the build, each zero-padded to two digits | `0116` for 16 January, `1016` for 16 October |
| `N` | the build sequence within that day, starting at 1, not padded | `1`, `4`, `12` |
| `+hotfix` | build metadata, present only on a release built from a `hotfix/` branch | `2026.1016.4+hotfix` |

The tag is the version string with no prefix: `2026.1016.4`, not `v2026.1016.4`.

**Why this shape, and the name.** The scheme is **SortVer**, sortable versioning. It is a hybrid of calendar
versioning and semantic versioning chosen so the string sorts in the same order as time wherever a system
stores it as text, which many do. The year sorts first, the zero-padded month and day sort within the year,
and the build sequence orders a day's releases. The name is this skill's own and is unrelated to .NET's
`SortVersion` class (the Unicode comparison version) and to Perl's `Sort::Versions` module (a comparator for
version strings). A version never says whether a change is breaking; that is the job of the commit's `!`
marker and `BREAKING CHANGE:` footer (`commit-messages.md`, section 5) and of the release notes.

**Three facts to know about the format, each accepted on purpose:**

1. **A parser may render the version without the leading zero.** `System.Version` and NuGet both read
   `2026.0116.3` and write it back as `2026.116.3`, and semantic versioning forbids leading zeros in numeric
   parts. So a release can appear as two strings: the tag and the artifact name carry `2026.0116.3`, the
   assembly and the package carry `2026.116.3`. **These are the same version.** The tag string is canonical;
   equality is numeric; a stripped rendering is not a defect and is not filed as one.
2. **`+hotfix`, never `-hotfix`.** A `-` suffix is a pre-release marker in semantic versioning and NuGet: it
   sorts *below* the bare version and is hidden from any consumer on stable-only, which is the default. A
   hotfix is the release consumers most need to see. `+` is build metadata: kept in the string, ignored for
   precedence. `System.Version` cannot hold it; the tag, the artifact name and the informational version can,
   and the assembly version carries the three numeric parts only.
3. **The build sequence is not padded, so a tenth build in one day sorts as text between the first and the
   second.** Numeric sort is always right, and the date parts keep every day in order. Padding the sequence
   would reopen fact 1 for a third part. Accepted.

Four numeric parts, each at most 65534, are what an assembly version can hold; `YYYY.MMdd.N.0` fits.


**Valid**

- `2026.0116.1`
- `2026.1016.4`
- `2026.1016.4+hotfix`

**Invalid, and why**

| Tag | Why |
|---|---|
| `v2026.1016.4` | prefix |
| `2026.116.4` as a tag | the month is zero-padded in the canonical string; `2026.116.4` is only how a parser renders it |
| `2026.1016.04` | padded sequence |
| `2026.10.16.4` | four parts; month and day are one part |
| `2026.1016.4-hotfix` | `-` is a pre-release marker: sorts below the base, hidden from stable-only consumers |
| `2026.1016.4+hotfix` on a merge from a `feature/` branch | the suffix means the branch was `hotfix/` |
| `1.4.0` | semantic versioning, not this scheme |
| any tag created by hand | the pipeline tags |

## 2. Who tags

- **The pipeline tags, never a person.** Every merge to `main` is tagged by the pipeline at the merge, with
  the pipeline's own credential so the host emits the tag event that starts the release build.
- The pipeline computes the version: the build date, the next sequence number for that date, and `+hotfix`
  when the merged branch was a `hotfix/` branch.
- A tag is never moved and never deleted. Where the host has tag protection, it is on; the artifact store
  holds the digest regardless of host.
- The version lives in the tag. It is not hand-edited in a project file; the build stamps the tag's version
  into the artifact.

## 3. The release record

For every tag, the release record (the host's release object, or the pipeline's own store) holds at least:

| Field | Example |
|---|---|
| version (the tag) | `2026.1016.4+hotfix` |
| commit | `1a2b3c4d…` |
| artifact digest | `sha256:9f8e…` |
| merged pull request | `#60` |
| built at | `2026-10-16T14:02:11Z` |

The host's release object is a pointer to the artifact, not a second copy of it. A release is immutable
from the moment the artifact is stored; "immutable releases" is turned on where the host offers it.

## 4. Release branches: `release/<version>`

A release branch is an option with one job: to keep an already-shipped release patchable after `main` has
moved on. It is **never where a release is made**. A release is a tag and the artifact built from it; a
branch is mutable by nature and marks nothing.

- Named after the full version it was cut from: `release/2026.1016.4`. Not a year, not a prefix: the branch
  keeps one release alive, and its name says which.
- Cut at that tag, takes `hotfix/` branches only, no feature ever lands on it.
- Hotfixes merged to it are tagged like any other merge, by their own build date, so the tag never collides
  with the branch name: a fix to `release/2026.1016.4` built on 20 October is `2026.1020.1+hotfix`.
- Each hotfix also reaches `main` by its own pull request, so the trunk never lacks a fix the field has.
- Deleted when the release is no longer supported. The tags are the record; the branch is not.

```
git switch -c release/2026.1016.4 2026.1016.4
git push -u origin release/2026.1016.4
git switch -c hotfix/station-index-null-key release/2026.1016.4
# ... fix, commit, pull request to release/2026.1016.4; the pipeline tags 2026.1020.1+hotfix ...
git switch -c hotfix/station-index-null-key-main main
# ... same fix, pull request to main; the pipeline tags 2026.1020.2+hotfix ...
```

## 5. Checklist for a tag

- [ ] Created by the pipeline, not by hand.
- [ ] `YYYY.MMdd.N`, month and day zero-padded, no `v` prefix.
- [ ] `+hotfix` present exactly when the merged branch was a `hotfix/` branch; no `-` suffix ever.
- [ ] Points at a merge commit on `main` or on a `release/<version>` branch.
- [ ] Has a release record with commit, digest, pull request and build time.
