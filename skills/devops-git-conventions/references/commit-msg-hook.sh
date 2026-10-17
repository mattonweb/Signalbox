#!/bin/sh
# commit-msg hook: refuse a commit whose first line is not in the house form.
#
# Checks the first line only, against references/commit-messages.md sections 2 to 4:
#   <type>(<scope>)!: <subject>   type from the twelve; one lowercase kebab-case scope at most;
#                                 lowercase after the colon; no trailing full stop; at most 72 characters.
# Exempt: merge commits ("Merge ..."), which the host writes, and fixup!/squash! lines, which git rewrites.
# A "revert: <original subject>" line passes: revert is a type and the colon follows it.
#
# Install: copy to the hooks folder git uses for the repo and make it executable, or chain it from a
# machine-wide hook. It reads the message file git passes as $1 and exits non-zero to refuse.

MSG_FILE="$1"
[ -r "$MSG_FILE" ] || { echo "commit-msg: cannot read $MSG_FILE" >&2; exit 2; }

# First line that is not a comment (git strips comment lines itself, but the hook sees the raw file).
FIRST="$(grep -v '^#' "$MSG_FILE" | sed -n '1p')"

case "$FIRST" in
  "Merge "*|"fixup! "*|"squash! "*) exit 0 ;;
esac

TYPES='feat|fix|content|docs|style|refactor|perf|test|build|ci|chore|revert'
PATTERN="^($TYPES)(\([a-z0-9]+(-[a-z0-9]+)*\))?!?: [^A-Z ]"

fail() {
  echo "commit-msg: refused. $1" >&2
  echo "commit-msg:   first line: $FIRST" >&2
  echo "commit-msg:   form: <type>(<scope>)!: <subject>  -- see devops-git-conventions, commit-messages.md" >&2
  exit 1
}

[ -n "$FIRST" ] || fail "empty message."
printf '%s\n' "$FIRST" | grep -Eq "$PATTERN" \
  || fail "first line must start with one of: ${TYPES}, an optional (scope), optional !, a colon, a space, then a lowercase subject."
[ "${#FIRST}" -le 72 ] || fail "first line is ${#FIRST} characters; the limit is 72."
case "$FIRST" in
  *.) fail "no full stop at the end of the first line." ;;
esac
exit 0
