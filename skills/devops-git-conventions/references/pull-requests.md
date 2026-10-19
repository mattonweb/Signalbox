# Pull requests

A pull request is how a branch reaches `main`. Where `main` is protected it is the only way; where it is not
yet protected it is still the form, because the pipeline (see `SKILL.md`, section 1) runs its checks on the
pull request and tags the merge.

## 1. One branch, one pull request, one topic

A pull request carries one branch and one topic. A branch that has grown a second topic is split before the
pull request opens, not explained in its description. The reviewer reads a pull request as one change with one
reason; two reasons means two reviews, and one of them will get the attention.

## 2. Title

The title is a commit subject and follows the same rule: `<type>(<scope>): <subject>`, imperative, lowercase
after the colon, at most 72 characters. When the branch holds one commit the title is that commit's subject.
When it holds several, the title names the topic the way the merge commit will.


**Valid**

- `feat(catalog): add the county export endpoint`
- `build(deps): update NuGet packages to clear the audit advisories`

**Invalid, and why**

| Title | Why |
|---|---|
| `Changes`, `Fix bug`, `Updates` | says nothing |
| `Feature/county-export-endpoint` | a branch name, not a subject |
| `feat(catalog): add the export endpoint and fix the logger` | two topics; two pull requests |
| `FEAT: Add County Export Endpoint` | uppercase type; capitals after the colon |
| `feat(catalog): add the county export endpoint, the entitlement check and the CSV writer for the Monday release` | over 72 characters |

## 3. Description

Four short parts, in this order. A part that does not apply is omitted, not left as a heading and never
filled with "N/A". `assets/pull-request-template.md` holds the four headings with a comment under each;
copied to the host's template location (`.github/pull_request_template.md` on GitHub), the host pre-fills
every new pull request with it, which is the tool enforcing the shape so the reviewer need not.

1. **Why.** The reason the change exists, in a sentence or two. A link to the design note or ticket that
   asked for it, when one exists.
2. **What.** What the reviewer will find, by area, not by file. The diff lists the files.
3. **Verification.** The same line the commits carry: what was built, what was run, the counts. Anything not
   covered by the automated checks and why.
4. **Follow-ups.** Anything deliberately left out, named, so it is not read as forgotten.

## 4. Opening

- Everything the pull request should carry is committed and pushed first. `git status --porcelain` prints
  nothing, and the branch's tip is on the host. Uncommitted work is not in the pull request, whatever the
  description says.
- Open as a **draft**. The pipeline's pull-request checks run on every push to the branch. Auto-complete
  (auto-merge) is turned on only once the pipeline's merge lane owns merging, so that the merge happens without a
  second step when the checks pass and the review is in; until then a person merges, and a coding agent never
  enables it.
- Mark ready for review when the branch is complete, not when it is started. A draft is the signal that the
  checks may still be red.
- A pull request into a public repository passes whatever content scan the repository runs before it
  merges. A hit is fixed on the branch, never waved through.

With the GitHub CLI, for instance (other hosts have the same two steps under other names):

```
gh pr create --draft --base main --title "feat(catalog): add the county export endpoint" --body-file pr.md
gh pr ready            # when the branch is complete and the checks are green
gh pr edit --add-reviewer <owner>   # the code owner from .github/CODEOWNERS; not every host plan requests one for you
```

## 5. Merging

- **Merge commit, never squash, never rebase-merge.** The branch's commits are the record of how the change
  was built, and the merge commit is the record that a pull request happened. Squashing erases the first and
  rebasing erases the second.
- The merge commit keeps the host's default subject (`Merge pull request #60 from <owner>/<branch>`). It is
  the one commit on `main` exempt from the commit-message form, because the host writes it and the pipeline
  tags it.
- Every merge to `main` is tagged by the pipeline (`references/tags-and-releases.md`). Nothing about the
  merge is done by hand.
- Delete the branch after the merge, on the host and locally. The merge commit and the tag hold everything
  the branch held. Most hosts can delete the remote branch on merge; turn that on where it exists.
