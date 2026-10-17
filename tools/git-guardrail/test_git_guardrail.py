#!/usr/bin/env python3
"""Tests for git_guardrail.py. Run:  python -m unittest test_git_guardrail -v  (from this folder).

Every row is (profile, command, expected) where expected is None for ALLOW or the rule label that
must appear in the deny reason. The table is the specification; the README's table is derived from it.
Rows whose outcome depends on the checked-out branch live in BRANCH_ROWS with the branch pinned; a row
in ROWS that tries to read a real branch fails the suite.
"""

import io
import json
import os
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import git_guardrail as gg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "git_guardrail.py")

ALLOW = None
OFF_TOPIC = ", not feature/* or hotfix/*"
UNREAD = ": the checked-out branch could not be read"

ROWS = [
    # --- handback: history commands are always denied -------------------------------------------
    ("handback", "git commit -m \"feat(api): add thing\"", "commit"),
    ("handback", "git commit", "commit"),
    ("handback", "git commit --amend --no-edit", "commit"),
    ("handback", "git push", "push"),
    ("handback", "git push origin main", "push"),
    ("handback", "git push --force-with-lease", "push"),
    ("handback", "git merge feature/x", "merge"),
    ("handback", "git rebase main", "rebase"),
    ("handback", "git reset", "reset"),
    ("handback", "git reset --soft HEAD~1", "reset"),
    ("handback", "git reset --hard", "reset"),
    ("handback", "git cherry-pick abc123", "cherry-pick"),
    ("handback", "git revert HEAD", "revert"),
    ("handback", "git am patch.mbox", "am"),
    # --- destructive shapes, every profile ------------------------------------------------------
    ("handback", "git clean -fd", "clean --force"),
    ("handback", "git clean -f", "clean --force"),
    ("handback", "git clean -xdf", "clean --force"),
    ("handback", "git clean --force", "clean --force"),
    ("author", "git clean -fd", "clean --force"),
    ("branch", "git clean -fd", "clean --force"),
    ("handback", "git branch -D feature/x", "branch -D / --force"),
    ("handback", "git branch -df feature/x", "branch -D / --force"),
    ("handback", "git branch --delete --force feature/x", "branch -D / --force"),
    ("handback", "git branch -f main abc123", "branch -D / --force"),
    ("author", "git branch -D feature/x", "branch -D / --force"),
    ("branch", "git branch -D feature/x", "branch -D / --force"),
    ("handback", "git checkout .", "checkout . / --force / -B"),
    ("handback", "git checkout -- .", "checkout . / --force / -B"),
    ("handback", "git checkout -f main", "checkout . / --force / -B"),
    ("handback", "git checkout -B main origin/main", "checkout . / --force / -B"),
    ("branch", "git checkout .", "checkout . / --force / -B"),
    ("handback", "git restore .", "restore ."),
    ("handback", "git restore --worktree .", "restore ."),
    ("handback", "git stash drop", "stash drop"),
    ("handback", "git stash clear", "stash clear"),
    ("handback", "git reflog expire --expire=now --all", "reflog expire"),
    ("handback", "git reflog delete HEAD@{1}", "reflog delete"),
    ("handback", "git gc --prune=now", "gc --prune"),
    ("handback", "git config alias.p push", "config alias"),
    ("handback", "git config --global user.name x", "config --global / --system write"),
    ("handback", "git worktree remove --force ../wt", "worktree remove --force"),
    ("handback", "git worktree prune", "worktree prune"),
    ("handback", "git tag -d v1", "tag -d / --force"),
    ("handback", "git tag -f v1", "tag -d / --force"),
    ("handback", "git remote remove origin", "remote remove"),
    ("handback", "git remote set-url origin x", "remote set-url"),
    ("handback", "git filter-branch --all", "filter-branch"),
    ("handback", "git filter-repo --path x", "filter-repo"),
    ("handback", "git replace abc def", "replace"),
    ("handback", "git update-ref -d refs/heads/x", "update-ref"),
    ("handback", "git prune", "prune"),
    # --- author: its own commits and pushes are fine, forced and destroying ones are not ---------
    ("author", "git commit -m \"docs(index): item 7\"", ALLOW),
    ("author", "git push", ALLOW),
    ("author", "git push origin main", ALLOW),
    ("author", "git push -u origin feature/x", ALLOW),
    ("author", "git push --force", "push --force / --delete"),
    ("author", "git push -f origin main", "push --force / --delete"),
    ("author", "git push --force-with-lease", "push --force / --delete"),
    ("author", "git push --force-if-includes", "push --force / --delete"),
    ("author", "git push origin --delete feature/x", "push --force / --delete"),
    ("author", "git push origin -d feature/x", "push --force / --delete"),
    ("author", "git push origin :feature/x", "push --force / --delete"),
    ("author", "git push origin +main", "push --force / --delete"),
    ("author", "git push --mirror", "push --force / --delete"),
    ("author", "git merge feature/x", ALLOW),
    ("author", "git rebase main", ALLOW),
    ("author", "git reset", ALLOW),
    ("author", "git reset --soft HEAD~1", ALLOW),
    ("author", "git reset --hard HEAD", "reset --hard"),
    ("author", "git reset --merge", "reset --hard"),
    ("author", "git reset --keep", "reset --hard"),
    ("author", "git cherry-pick abc123", ALLOW),
    # --- branch: main is locked. merge/rebase/reset as handback; a push is judged by where it lands ---
    # (rows that need the checked-out branch are in BRANCH_ROWS; these resolve without reading it)
    ("branch", "git merge feature/x", "merge"),
    ("branch", "git merge main", "merge"),
    ("branch", "git rebase main", "rebase"),
    ("branch", "git reset", "reset"),
    ("branch", "git reset --soft HEAD~1", "reset"),
    ("branch", "git reset --hard", "reset"),
    ("branch", "git push origin main", "push to main" + OFF_TOPIC),
    ("branch", "git push origin master", "push to master" + OFF_TOPIC),
    ("branch", "git push -u origin release/2026.1017.1", "push to release/2026.1017.1" + OFF_TOPIC),
    ("branch", "git push origin feature/x:main", "push to main" + OFF_TOPIC),
    ("branch", "git push origin HEAD:main", "push to main" + OFF_TOPIC),
    ("branch", "git push origin HEAD:refs/heads/main", "push to main" + OFF_TOPIC),
    ("branch", "git push origin feature/x develop", "push to develop" + OFF_TOPIC),
    ("branch", "git push origin refs/heads/*:refs/heads/*", "push to *" + OFF_TOPIC),
    ("branch", "git push origin v1", "push to v1" + OFF_TOPIC),
    ("branch", "git push -u origin feature/x", ALLOW),
    ("branch", "git push origin hotfix/y", ALLOW),
    ("branch", "git push origin feature/x:feature/x", ALLOW),
    ("branch", "git push origin HEAD:feature/x", ALLOW),
    ("branch", "git push origin HEAD:refs/heads/feature/x", ALLOW),
    ("branch", "git push -o ci.skip origin feature/x", ALLOW),  # the option's value is not the remote
    ("branch", "git push --force origin feature/x", "push --force / --delete"),
    ("branch", "git push -f origin feature/x", "push --force / --delete"),
    ("branch", "git push --force-with-lease origin feature/x", "push --force / --delete"),
    ("branch", "git push origin :feature/x", "push --force / --delete"),
    ("branch", "git push origin --delete feature/x", "push --force / --delete"),
    ("branch", "git push origin +feature/x", "push --force / --delete"),
    ("branch", "git push --mirror origin", "push --force / --delete"),
    ("branch", "git push --all origin", "push --all / --tags / --prune"),
    ("branch", "git push --tags origin", "push --all / --tags / --prune"),
    ("branch", "git push --follow-tags origin feature/x", "push --all / --tags / --prune"),
    ("branch", "git push --prune origin feature/x", "push --all / --tags / --prune"),
    ("branch", "git status", ALLOW),
    ("branch", "git fetch --all", ALLOW),
    ("branch", "git pull --ff-only", ALLOW),
    ("branch", "git add -A", ALLOW),
    ("branch", "git checkout -b feature/x", ALLOW),
    ("branch", "git switch -c hotfix/y", ALLOW),
    ("branch", "git checkout main", ALLOW),
    ("branch", "git branch -d feature/x", ALLOW),
    ("branch", "git stash pop", ALLOW),
    # --- everyday read and write commands pass in every profile -----------------------------------
    ("handback", "git status", ALLOW),
    ("handback", "git status --short", ALLOW),
    ("handback", "git diff", ALLOW),
    ("handback", "git log --oneline -5", ALLOW),
    ("handback", "git show HEAD", ALLOW),
    ("handback", "git fetch --all", ALLOW),
    ("handback", "git pull --ff-only", ALLOW),
    ("handback", "git add -A", ALLOW),
    ("handback", "git branch", ALLOW),
    ("handback", "git branch -a", ALLOW),
    ("handback", "git branch -d feature/x", ALLOW),
    ("handback", "git branch -m old new", ALLOW),
    ("handback", "git checkout main", ALLOW),
    ("handback", "git checkout -b feature/x", ALLOW),
    ("handback", "git checkout -- README.md", ALLOW),
    ("handback", "git restore --staged README.md", ALLOW),
    ("handback", "git restore README.md", ALLOW),
    ("handback", "git switch main", ALLOW),
    ("handback", "git stash", ALLOW),
    ("handback", "git stash push -m wip", ALLOW),
    ("handback", "git stash pop", ALLOW),
    ("handback", "git stash list", ALLOW),
    ("handback", "git clean -n", ALLOW),
    ("handback", "git clean -nd", ALLOW),
    ("handback", "git clean -fn", ALLOW),
    ("handback", "git clean --dry-run -d", ALLOW),
    ("handback", "git reflog", ALLOW),
    ("handback", "git gc", ALLOW),
    ("handback", "git config user.name", ALLOW),
    ("handback", "git config --global --get user.name", ALLOW),
    ("handback", "git config --global --list", ALLOW),
    ("handback", "git config core.autocrlf true", ALLOW),
    ("handback", "git tag", ALLOW),
    ("handback", "git tag v1", ALLOW),
    ("handback", "git remote -v", ALLOW),
    ("handback", "git remote add upstream x", ALLOW),
    ("handback", "git worktree list", ALLOW),
    ("handback", "git worktree add ../wt feature/x", ALLOW),
    ("handback", "git worktree remove ../wt", ALLOW),
    ("handback", "git blame README.md", ALLOW),
    ("handback", "git rev-parse HEAD", ALLOW),
    ("handback", "git ls-files", ALLOW),
    ("handback", "git describe --tags", ALLOW),
    ("handback", "git --version", ALLOW),
    ("handback", "git help commit", ALLOW),
    ("handback", "git log --grep=\"git push\"", ALLOW),
    # --- global options, paths, spellings --------------------------------------------------------
    ("handback", "git -C C:\\Source\\Repo push", "push"),
    ("handback", "git -C \"C:\\Source\\Repo Name\" commit -m x", "commit"),
    ("handback", "git -c core.autocrlf=false push", "push"),
    ("handback", "git --git-dir=.git --work-tree=. push", "push"),
    ("handback", "git --no-pager push", "push"),
    ("handback", "git -C /c/Source/Repo log --oneline", ALLOW),
    ("handback", "git.exe push", "push"),
    ("handback", "GIT.EXE push", "push"),
    ("handback", "/usr/bin/git push", "push"),
    ("handback", "\"C:\\Program Files\\Git\\cmd\\git.exe\" push", "push"),
    ("handback", "'C:\\Program Files\\Git\\cmd\\git.exe' push", "push"),
    ("handback", "echo `git push`", "push"),
    ("handback", "git log --format=\"%h (%an)\" -3", ALLOW),
    # --- compound commands and wrappers -------------------------------------------------------------
    ("handback", "git add -A && git commit -m x && git push", "commit"),
    ("handback", "git status; git push", "push"),
    ("handback", "git status || git push", "push"),
    ("handback", "git log | head && git push", "push"),
    ("handback", "git status\ngit push", "push"),
    ("handback", "git status\r\ngit push", "push"),
    ("handback", "git \\\n  push", "push"),
    ("handback", "cd C:\\Source\\Repo && git commit -m x", "commit"),
    ("handback", "echo $(git push)", "push"),
    ("handback", "(git push)", "push"),
    ("handback", "{ git push; }", "push"),
    ("handback", "env GIT_SSH=x git push", "push"),
    ("handback", "GIT_TRACE=1 git push", "push"),
    ("handback", "exec git push", "push"),
    ("handback", "command git push", "push"),
    ("handback", "nohup git push &", "push"),
    ("handback", "timeout 30 git push", "push"),
    ("handback", "time git push", "push"),
    ("handback", "bash -c \"git push\"", "push"),
    ("handback", "sh -c 'git add -A; git commit -m x'", "commit"),
    ("handback", "bash -lc \"cd x && git push origin main\"", "push"),
    ("handback", "eval \"git push\"", "push"),
    ("handback", "echo push | xargs git", ALLOW),  # not detectable; documented limit
    ("handback", "git commit -F - <<'EOF'\nfeat: x\n\nbody\nEOF", "commit"),
    ("handback", "git commit -m \"unbalanced", "commit"),
    ("handback", "git status && echo \"unbalanced", ALLOW),
    # --- PowerShell shapes ----------------------------------------------------------------------
    ("handback", "& git push", "push"),
    ("handback", "& \"C:\\Program Files\\Git\\cmd\\git.exe\" push", "push"),
    ("handback", "git status; git push", "push"),
    ("handback", "git push `\n  origin main", "push"),
    ("handback", "powershell -Command \"git push\"", "push"),
    ("handback", "pwsh -c \"git commit -m x\"", "commit"),
    ("handback", "cmd /c \"git push\"", "push"),
    ("handback", "Get-ChildItem; git status", ALLOW),
    # --- text that mentions git but does not run it ----------------------------------------------
    ("handback", "echo \"git push\"", ALLOW),
    ("handback", "grep -rn \"git push\" docs/", ALLOW),
    ("handback", "cat git-push-notes.md", ALLOW),
    ("author", "git commit -m \"docs: never git push from an agent\"", ALLOW),
    ("handback", "python tools/gitlab.py push", ALLOW),
    ("handback", "ls", ALLOW),
    ("handback", "", ALLOW),
]

# (checked-out branch as read_branch reports it, command, expected) — all under the branch profile.
BRANCH_ROWS = [
    ("feature/x", "git commit -m \"feat(api): add thing\"", ALLOW),
    ("feature/x", "git commit --amend --no-edit", ALLOW),
    ("feature/x", "git cherry-pick abc123", ALLOW),
    ("feature/x", "git revert HEAD", ALLOW),
    ("feature/x", "git am patch.mbox", ALLOW),
    ("feature/x", "git push", ALLOW),
    ("feature/x", "git push origin", ALLOW),
    ("feature/x", "git push origin HEAD", ALLOW),
    ("feature/x", "git push -u origin HEAD", ALLOW),
    ("feature/x", "git push --set-upstream origin HEAD", ALLOW),
    ("feature/x", "git add -A && git commit -m x && git push -u origin HEAD", ALLOW),
    ("feature/x", "git -C C:\\Source\\Repo commit -m x", ALLOW),
    ("feature/x", "bash -c \"git commit -m x && git push\"", ALLOW),
    ("feature/x", "git push origin main", "push to main" + OFF_TOPIC),
    ("feature/x", "git push origin HEAD:main", "push to main" + OFF_TOPIC),
    ("feature/x", "git push --force", "push --force / --delete"),
    ("feature/x", "git merge main", "merge"),
    ("feature/x", "git rebase main", "rebase"),
    ("feature/x", "git reset --hard", "reset"),
    ("hotfix/urgent", "git commit -m x", ALLOW),
    ("hotfix/urgent", "git push", ALLOW),
    ("hotfix/urgent", "git push -u origin HEAD", ALLOW),
    ("main", "git commit -m x", "commit on main" + OFF_TOPIC),
    ("main", "git commit --amend --no-edit", "commit on main" + OFF_TOPIC),
    ("main", "git cherry-pick abc123", "cherry-pick on main" + OFF_TOPIC),
    ("main", "git revert HEAD", "revert on main" + OFF_TOPIC),
    ("main", "git am patch.mbox", "am on main" + OFF_TOPIC),
    ("main", "git push", "push on main" + OFF_TOPIC),
    ("main", "git push origin", "push on main" + OFF_TOPIC),
    ("main", "git push origin HEAD", "push on main" + OFF_TOPIC),
    ("main", "git push -u origin HEAD", "push on main" + OFF_TOPIC),
    ("main", "git add -A && git commit -m x", "commit on main" + OFF_TOPIC),
    ("main", "git push origin feature/x", ALLOW),  # the destination is what is locked, not the seat
    ("main", "git checkout -b feature/x", ALLOW),
    ("main", "git status", ALLOW),
    ("master", "git commit -m x", "commit on master" + OFF_TOPIC),
    ("master", "git push", "push on master" + OFF_TOPIC),
    ("release/2026.1017.1", "git commit -m x", "commit on release/2026.1017.1" + OFF_TOPIC),
    ("release/2026.1017.1", "git push", "push on release/2026.1017.1" + OFF_TOPIC),
    ("release/2026.1017.1", "git push origin HEAD", "push on release/2026.1017.1" + OFF_TOPIC),
    ("develop", "git commit -m x", "commit on develop" + OFF_TOPIC),
    ("features", "git commit -m x", "commit on features" + OFF_TOPIC),  # the prefix needs its slash
    (None, "git commit -m x", "commit" + UNREAD),
    (None, "git commit --amend --no-edit", "commit" + UNREAD),
    (None, "git cherry-pick abc123", "cherry-pick" + UNREAD),
    (None, "git revert HEAD", "revert" + UNREAD),
    (None, "git am patch.mbox", "am" + UNREAD),
    (None, "git push", "push" + UNREAD),
    (None, "git push origin", "push" + UNREAD),
    (None, "git push origin HEAD", "push" + UNREAD),
    (None, "git push -u origin HEAD", "push" + UNREAD),
    (None, "git push origin main", "push to main" + OFF_TOPIC),
    (None, "git push origin feature/x", ALLOW),  # an explicit topic destination needs no branch read
    (None, "git status", ALLOW),
]


def forbid_real_branch(repo_dir):
    raise AssertionError("a ROWS entry read the checked-out branch of %r; move it to BRANCH_ROWS" % repo_dir)


def assert_decision(test, reason, expected):
    if expected is ALLOW:
        test.assertIsNone(reason, "expected ALLOW, got: %s" % reason)
        return
    test.assertIsNotNone(reason, "expected DENY (%s), got ALLOW" % expected)
    test.assertIn("rule: %s)" % expected, reason)


class RuleTable(unittest.TestCase):
    def setUp(self):
        self.real_read_branch = gg.read_branch
        gg.read_branch = forbid_real_branch

    def tearDown(self):
        gg.read_branch = self.real_read_branch

    def test_every_row(self):
        for profile, command, expected in ROWS:
            with self.subTest(profile=profile, command=command):
                assert_decision(self, gg.decision(command, profile), expected)

    def test_table_covers_both_branches(self):
        """The table must be able to produce both outcomes, or it is not an instrument."""
        self.assertTrue(any(e is ALLOW for _, _, e in ROWS))
        self.assertTrue(any(e is not ALLOW for _, _, e in ROWS))
        self.assertTrue(any(e is ALLOW for _, _, e in BRANCH_ROWS))
        self.assertTrue(any(e is not ALLOW for _, _, e in BRANCH_ROWS))

    def test_every_rule_in_each_profile_has_a_denying_row(self):
        gg.read_branch = lambda repo_dir: None  # an unreadable branch, so the branch rules can deny

        def denied_subcommands(profile):
            hits = (gg.find_violation(c, profile) for _, c, e in ROWS if e is not ALLOW)
            return {hit[0] for hit in hits if hit}

        for profile, rules in gg.PROFILES.items():
            missing = set(rules) - denied_subcommands(profile)
            self.assertEqual(set(), missing, "rules with no denying row in %s" % profile)


class BranchRows(unittest.TestCase):
    """The branch profile with the checked-out branch pinned; no git process runs here."""

    def setUp(self):
        self.real_read_branch = gg.read_branch
        self.reads = []

    def tearDown(self):
        gg.read_branch = self.real_read_branch

    def pin(self, branch):
        def read(repo_dir):
            self.reads.append(repo_dir)
            return branch
        gg.read_branch = read

    def test_every_branch_row(self):
        for branch, command, expected in BRANCH_ROWS:
            with self.subTest(branch=branch, command=command):
                self.pin(branch)
                assert_decision(self, gg.decision(command, "branch"), expected)

    def test_every_pinned_branch_appears(self):
        branches = {b for b, _, _ in BRANCH_ROWS}
        self.assertTrue({"feature/x", "main", "release/2026.1017.1", None} <= branches, branches)

    def test_branch_is_read_in_the_target_directory(self):
        self.pin("feature/x")
        gg.decision("git commit -m x", "branch", cwd="C:\\Source\\Repo")
        gg.decision("git -C \"C:\\Source\\Other\" commit -m x", "branch", cwd="C:\\Source\\Repo")
        gg.decision("git -C sub push", "branch", cwd="C:\\Source\\Repo")
        gg.decision("git -C 'C:\\Source\\Repo' -C sub push origin HEAD", "branch")  # a -C path is quoted, as in a shell
        gg.decision("git commit -m x", "branch")
        expected = ["C:\\Source\\Repo", "C:\\Source\\Other", os.path.join("C:\\Source\\Repo", "sub"),
                    os.path.join("C:\\Source\\Repo", "sub"), None]
        self.assertEqual(expected, self.reads)

    def test_branch_is_read_only_when_a_rule_needs_it(self):
        self.pin("main")
        for command in ("git status", "git push origin feature/x", "git push origin main", "git merge x", "ls"):
            gg.decision(command, "branch", cwd="C:\\Source\\Repo")
        self.assertEqual([], self.reads)

    def test_other_profiles_never_read_the_branch(self):
        gg.read_branch = forbid_real_branch
        for profile in ("handback", "author"):
            for _, command, _ in BRANCH_ROWS:
                gg.decision(command, profile, cwd="C:\\Source\\Repo")


class Payloads(unittest.TestCase):
    def run_hook(self, payload_text, profile="handback"):
        out = io.StringIO()
        real = sys.stdout
        sys.stdout = out
        try:
            code = gg.run_hook(profile, None, io.StringIO(payload_text))
        finally:
            sys.stdout = real
        return code, out.getvalue()

    def payload(self, command, tool="Bash"):
        return json.dumps({"hook_event_name": "PreToolUse", "tool_name": tool, "cwd": "C:\\Source\\Repo",
                           "tool_input": {"command": command, "description": "x"}})

    def test_deny_is_well_formed_json(self):
        code, out = self.run_hook(self.payload("git push origin main"))
        self.assertEqual(0, code)
        body = json.loads(out)["hookSpecificOutput"]
        self.assertEqual("PreToolUse", body["hookEventName"])
        self.assertEqual("deny", body["permissionDecision"])
        self.assertIn("git push is blocked", body["permissionDecisionReason"])
        self.assertIn("hand it back", body["permissionDecisionReason"])

    def test_allow_is_silent(self):
        code, out = self.run_hook(self.payload("git status"))
        self.assertEqual((0, ""), (code, out))

    def test_powershell_tool_is_covered(self):
        code, out = self.run_hook(self.payload("git push", tool="PowerShell"))
        self.assertIn("deny", out)

    def test_other_tools_are_ignored(self):
        code, out = self.run_hook(self.payload("git push", tool="Edit"))
        self.assertEqual((0, ""), (code, out))

    def test_bad_payload_fails_open(self):
        for text in ("", "not json", "[]", "42", json.dumps({"tool_name": "Bash"}),
                     json.dumps({"tool_name": "Bash", "tool_input": {}}),
                     json.dumps({"tool_name": "Bash", "tool_input": {"command": 7}})):
            with self.subTest(text=text):
                self.assertEqual((0, ""), self.run_hook(text))

    def test_author_profile_lets_own_commit_through(self):
        code, out = self.run_hook(self.payload("git commit -m x && git push"), profile="author")
        self.assertEqual((0, ""), (code, out))

    def test_branch_profile_reads_the_payload_cwd(self):
        real, reads = gg.read_branch, []
        gg.read_branch = lambda repo_dir: reads.append(repo_dir) or "main"
        try:
            code, out = self.run_hook(self.payload("git commit -m x"), profile="branch")
        finally:
            gg.read_branch = real
        self.assertEqual(["C:\\Source\\Repo"], reads)
        self.assertIn("commit on main, not feature/* or hotfix/*", out)
        self.assertIn("pull request", out)

    def test_log_appends_one_line_per_deny(self):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "deny.jsonl")
            real = sys.stdout
            sys.stdout = io.StringIO()
            try:
                gg.run_hook("handback", path, io.StringIO(self.payload("git push")))
                gg.run_hook("handback", path, io.StringIO(self.payload("git status")))
                gg.run_hook("handback", path, io.StringIO(self.payload("git commit")))
            finally:
                sys.stdout = real
            with open(path, encoding="utf-8") as handle:
                rows = [json.loads(line) for line in handle if line.strip()]
            self.assertEqual(["git push", "git commit"], [r["command"] for r in rows])
            self.assertEqual("C:\\Source\\Repo", rows[0]["cwd"])


class Subprocess(unittest.TestCase):
    """The hook as Claude Code runs it: a real process, stdin payload, stdout JSON, exit 0."""

    def test_pipe_deny(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "git push origin main"}})
        result = subprocess.run([sys.executable, SCRIPT, "--profile", "handback"], input=payload,
                                capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("deny", json.loads(result.stdout)["hookSpecificOutput"]["permissionDecision"])

    def test_pipe_allow(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "git status"}})
        result = subprocess.run([sys.executable, SCRIPT], input=payload, capture_output=True, text=True)
        self.assertEqual((0, ""), (result.returncode, result.stdout))

    def test_explain_exit_codes(self):
        deny = subprocess.run([sys.executable, SCRIPT, "--explain", "git push"], capture_output=True, text=True)
        allow = subprocess.run([sys.executable, SCRIPT, "--explain", "git status"], capture_output=True, text=True)
        self.assertEqual(1, deny.returncode)
        self.assertTrue(deny.stdout.startswith("DENY"))
        self.assertEqual(0, allow.returncode)
        self.assertEqual("ALLOW", allow.stdout.strip())


class RealRepository(unittest.TestCase):
    """The branch profile against a real repository: the hook process reads the branch with git."""

    def hook(self, command, cwd):
        payload = json.dumps({"tool_name": "Bash", "cwd": cwd, "tool_input": {"command": command}})
        result = subprocess.run([sys.executable, SCRIPT, "--profile", "branch"], input=payload,
                                capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        return result.stdout

    def repo_on(self, folder, branch):
        subprocess.run(["git", "init", "-q", folder], check=True, capture_output=True)
        subprocess.run(["git", "-C", folder, "symbolic-ref", "HEAD", "refs/heads/" + branch], check=True,
                       capture_output=True)

    def test_feature_branch_commit_allowed_push_to_main_denied(self):
        with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as folder:
            self.repo_on(folder, "feature/x")
            self.assertEqual("feature/x", gg.read_branch(folder))
            self.assertEqual("", self.hook("git commit -m x", folder))
            self.assertEqual("", self.hook("git -C \"%s\" commit -m x" % folder, HERE))
            denied = json.loads(self.hook("git push origin main", folder))["hookSpecificOutput"]
            self.assertEqual("deny", denied["permissionDecision"])
            self.assertIn("rule: push to main, not feature/* or hotfix/*)", denied["permissionDecisionReason"])

    def test_main_checked_out_denies_commit(self):
        with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as folder:
            self.repo_on(folder, "main")
            self.assertEqual("main", gg.read_branch(folder))
            self.assertIn("rule: commit on main, not feature/* or hotfix/*)", self.hook("git commit -m x", folder))
            self.assertIn("rule: push on main, not feature/* or hotfix/*)", self.hook("git push", folder))

    def test_not_a_repository_denies_commit_saying_so(self):
        with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as folder:
            self.assertIsNone(gg.read_branch(folder))
            self.assertIsNone(gg.read_branch(os.path.join(folder, "missing")))
            self.assertIn("the checked-out branch could not be read", self.hook("git commit -m x", folder))
            self.assertIn("the checked-out branch could not be read", self.hook("git push", folder))
            self.assertEqual("", self.hook("git status", folder))


if __name__ == "__main__":
    unittest.main()
