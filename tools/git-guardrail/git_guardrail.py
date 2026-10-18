#!/usr/bin/env python3
"""git-guardrail: a Claude Code PreToolUse hook that denies git commands an agent must not run.

Reads the hook payload on stdin, finds every git invocation in the Bash or PowerShell command,
and answers with a JSON deny when one matches the active profile. Anything else is silence
(exit 0, no output), which leaves the normal permission flow untouched.

Profiles
    handback  for homes whose working agreement human-gates git history (the coding agents):
              commit, push, merge, rebase, reset, cherry-pick, revert, am are always denied,
              plus the destructive shapes below.
    branch    for homes that may commit and push on a feature/* or hotfix/* branch but never touch
              main, master or release/*: commit, cherry-pick, revert and am are allowed only when the
              target repository has a feature/* or hotfix/* branch checked out; push only when it is
              not forced or deleting AND every destination ref is feature/* or hotfix/* (a bare push,
              or HEAD, resolves to the checked-out branch); merge, rebase, reset and the destructive
              shapes are denied as in handback. A branch that cannot be read (not a repository,
              detached HEAD, git missing) denies commit and bare push.
    author    for homes that commit and push their own work: only
              the destructive shapes are denied - force push, remote delete, reset --hard,
              clean --force, branch -D, checkout ., restore ., stash drop, history rewrites.

Usage (as a hook, shell form, see README.md)
    python git_guardrail.py --profile handback
Manual check
    python git_guardrail.py --profile branch --explain "git commit -m x" --cwd C:\\Source\\Repo

Design rules
    * Fail OPEN on a bad payload: an unreadable or non-command payload never blocks real work.
    * Fail CLOSED on an unparseable command string: fall back to a cruder tokeniser rather than
      let a stray quote hide a push.
    * The reason tells the agent what to do instead, in the words of its own charter.
    * No dependencies beyond the standard library; nothing to install.
"""

import argparse
import json
import os
import re
import shlex
import subprocess
import sys
from datetime import datetime, timezone

VERSION = "1.1.1"
HOOK_EVENT = "PreToolUse"
SEPARATORS = {"&&", "||", ";", "|", "&", "|&", ";;"}
INTERPRETERS = {"bash", "sh", "zsh", "dash", "ksh", "pwsh", "powershell", "cmd", "eval", "xargs"}
WRAPPERS = {"exec", "command", "env", "sudo", "nohup", "time", "nice", "builtin"}
WRAPPERS_WITH_VALUE = {"timeout": 1}
GIT_GLOBAL_WITH_VALUE = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--config-env", "--super-prefix"}
READ_ONLY_CONFIG = {"--get", "--get-all", "--get-regexp", "--list", "-l", "--show-origin", "--show-scope"}
PUSH_OPTIONS_WITH_VALUE = {"-o", "--push-option", "--repo", "--receive-pack", "--exec"}
PUSH_WHOLESALE = {"--all", "--branches", "--tags", "--follow-tags", "--prune"}
TOPIC_PREFIXES = ("feature/", "hotfix/")
BRANCH_TIMEOUT_SECONDS = 10
MAX_DEPTH = 3


# ----------------------------------------------------------------------------------------------
# Rules. Each returns a short rule label when the invocation is denied, else None.
# `shorts` is the set of single letters from combined short flags (-fdx -> {f, d, x}); `args` is
# every token after the subcommand; `words` is the positional tokens (not starting with "-");
# `repo_dir` is the directory git runs in (`git -C <path>` folded onto the payload's cwd, or None
# for the process's own cwd). Only the branch profile's rules read it.
# ----------------------------------------------------------------------------------------------

def _always(label):
    return lambda shorts, args, words, repo_dir: label


def _clean(shorts, args, words, repo_dir):
    dry = "n" in shorts or "--dry-run" in args
    forced = "f" in shorts or "--force" in args
    return None if dry or not forced else "clean --force"


def _branch(shorts, args, words, repo_dir):
    if "D" in shorts or "f" in shorts or "--force" in args:
        return "branch -D / --force"
    return None


def _checkout(shorts, args, words, repo_dir):
    if "." in words or "f" in shorts or "--force" in args or "B" in shorts:
        return "checkout . / --force / -B"
    return None


def _restore(shorts, args, words, repo_dir):
    return "restore ." if "." in words or ":/" in words or "*" in words else None


def _stash(shorts, args, words, repo_dir):
    return "stash %s" % words[0] if words and words[0] in {"drop", "clear"} else None


def _reflog(shorts, args, words, repo_dir):
    return "reflog %s" % words[0] if words and words[0] in {"expire", "delete"} else None


def _gc(shorts, args, words, repo_dir):
    return "gc --prune" if any(a.startswith("--prune") for a in args) else None


def _config(shorts, args, words, repo_dir):
    if any(a.startswith("alias.") for a in args):
        return "config alias"
    reads = any(a in READ_ONLY_CONFIG for a in args)
    if ("--global" in args or "--system" in args) and not reads:
        return "config --global / --system write"
    return None


def _worktree(shorts, args, words, repo_dir):
    if words and words[0] == "remove" and ("f" in shorts or "--force" in args):
        return "worktree remove --force"
    return "worktree prune" if words and words[0] == "prune" else None


def _tag(shorts, args, words, repo_dir):
    if "d" in shorts or "--delete" in args or "f" in shorts or "--force" in args:
        return "tag -d / --force"
    return None


def _remote(shorts, args, words, repo_dir):
    if words and words[0] in {"remove", "rm", "set-url", "prune"}:
        return "remote %s" % words[0]
    return None


def _push_forced(shorts, args, words, repo_dir):
    forced = "f" in shorts or "d" in shorts or "--force" in args or "--delete" in args or "--mirror" in args
    forced = forced or any(a.startswith("--force-with-lease") or a.startswith("--force-if-includes") for a in args)
    forced = forced or any(a.startswith("+") or a.startswith(":") for a in words)
    return "push --force / --delete" if forced else None


def _reset_hard(shorts, args, words, repo_dir):
    return "reset --hard" if any(a in {"--hard", "--merge", "--keep"} for a in args) else None


# --- branch profile: the checked-out branch decides -------------------------------------------

def read_branch(repo_dir):
    """Checked-out branch of the repository at repo_dir (None: this process's cwd), or None.

    None covers every failure alike: not a repository, detached HEAD, git missing, a timeout. The
    tests replace this function to pin the branch, so the rules below call it by this module name.
    """
    try:
        result = subprocess.run(["git", "symbolic-ref", "--short", "HEAD"], cwd=repo_dir, capture_output=True,
                                text=True, timeout=BRANCH_TIMEOUT_SECONDS)
    except (OSError, subprocess.SubprocessError):
        return None
    branch = result.stdout.strip()
    return branch if result.returncode == 0 and branch else None


def is_topic_branch(ref):
    return ref.startswith(TOPIC_PREFIXES)


def off_topic_label(name, branch):
    """Deny label for `name` on the checked-out `branch`; None when it is feature/* or hotfix/*."""
    if branch is None:
        return "%s: the checked-out branch could not be read" % name
    return None if is_topic_branch(branch) else "%s on %s, not feature/* or hotfix/*" % (name, branch)


def _history_on_topic(name):
    return lambda shorts, args, words, repo_dir: off_topic_label(name, read_branch(repo_dir))


def push_destinations(args, words):
    """Destination refs a push writes to; "HEAD" stands for the checked-out branch."""
    option_values = {args[i + 1] for i, a in enumerate(args[:-1]) if a in PUSH_OPTIONS_WITH_VALUE}
    refspecs = [w for w in words if w not in option_values][1:]
    return [spec.split(":", 1)[-1] for spec in refspecs] or ["HEAD"]


def push_destination_label(destination, repo_dir):
    if destination == "HEAD":
        return off_topic_label("push", read_branch(repo_dir))
    ref = destination[len("refs/heads/"):] if destination.startswith("refs/heads/") else destination
    return None if is_topic_branch(ref) else "push to %s, not feature/* or hotfix/*" % ref


def _push_to_topic(shorts, args, words, repo_dir):
    forced = _push_forced(shorts, args, words, repo_dir)
    if forced:
        return forced
    if any(a in PUSH_WHOLESALE for a in args):
        return "push --all / --tags / --prune"
    labels = (push_destination_label(d, repo_dir) for d in push_destinations(args, words))
    return next((label for label in labels if label), None)


DESTRUCTIVE = {
    "clean": _clean,
    "branch": _branch,
    "checkout": _checkout,
    "restore": _restore,
    "stash": _stash,
    "reflog": _reflog,
    "gc": _gc,
    "config": _config,
    "worktree": _worktree,
    "tag": _tag,
    "remote": _remote,
    "filter-branch": _always("filter-branch"),
    "filter-repo": _always("filter-repo"),
    "replace": _always("replace"),
    "update-ref": _always("update-ref"),
    "prune": _always("prune"),
}

HISTORY = {name: _always(name) for name in ("commit", "push", "merge", "rebase", "reset", "cherry-pick", "revert", "am")}
TOPIC_HISTORY = {name: _history_on_topic(name) for name in ("commit", "cherry-pick", "revert", "am")}

PROFILES = {
    "handback": {**DESTRUCTIVE, **HISTORY},
    "branch": {**DESTRUCTIVE, **HISTORY, **TOPIC_HISTORY, "push": _push_to_topic},
    "author": {**DESTRUCTIVE, "push": _push_forced, "reset": _reset_hard},
}

ADVICE = {
    "handback": (
        "This repository's working agreement human-gates git history: do not commit, push, merge, rebase or "
        "reset. Leave finished work uncommitted in the working tree and hand it back; drafting the commit "
        "message is fine, running the commit is not. If the maintainer asked for this exact command in the "
        "current request, say so in your reply and let them run it."
    ),
    "branch": (
        "This repository locks main: commit and push only on a feature/<topic> or hotfix/<topic> branch, and "
        "bring the work to main by pull request. Cut one with git checkout -b feature/<topic>, commit there and "
        "push that branch; merge, rebase and reset are a human's call. If the branch could not be read, run "
        "git status, say what you see in your reply, and hand back rather than retrying."
    ),
    "author": (
        "This command rewrites history or discards work, which is a human's call, not the agent's. Stop, say "
        "what you were about to run and why, and let a human run it if it is really needed."
    ),
}


# ----------------------------------------------------------------------------------------------
# Tokenising
# ----------------------------------------------------------------------------------------------

def normalise(command):
    """Join line continuations, turn newlines into separators, drop grouping punctuation."""
    text = command.replace("\r\n", "\n").replace("\r", "\n")
    text = re.sub(r"\\\n", " ", text)          # bash continuation
    text = re.sub(r"`\n", " ", text)           # PowerShell continuation
    text = text.replace("\n", " ; ")
    text = re.sub(r"\$\(|[(){}`]", " ; ", text)  # $( ... ), ( ... ), { ... }, backticks start a command
    return text


def tokenise(command):
    """Tokens with quotes resolved; separators survive as their own tokens."""
    text = normalise(command)
    try:
        lexer = shlex.shlex(text, posix=True, punctuation_chars=";&|")
        lexer.whitespace_split = True
        return list(lexer)
    except ValueError:
        return crude_tokens(text)


def crude_tokens(text):
    """Fallback for an unbalanced quote: strip quotes, split separators, split on whitespace."""
    text = re.sub(r"""["']""", " ", text)
    text = re.sub(r"(\|\||&&|;|\||&)", r" \1 ", text)
    return text.split()


def segments(tokens):
    """Split a token stream on command separators."""
    current, out = [], []
    for token in tokens:
        if token in SEPARATORS or (token and set(token) <= set(";&|")):
            out.append(current)
            current = []
            continue
        current.append(token)
    out.append(current)
    return [s for s in out if s]


def program_name(token):
    """git, /usr/bin/git, C:\\Program Files\\Git\\cmd\\git.exe, GIT.EXE -> git."""
    name = re.split(r"[\\/]", token)[-1].lower()
    return name[:-4] if name.endswith(".exe") else name


REDIRECT = re.compile(r"^(\d*[<>]{1,2}&?\d*|&>>?)(\S*)$")


def strip_redirects(segment):
    """Drop shell redirections (2>&1, > file, 2>/dev/null, &>> log) so a redirect is never read as an argument.

    A bare operator such as ">" or "2>" takes the next token as its target; a complete form such as "2>&1" or
    "2>/dev/null" stands alone. Seen 2026-10-18: a coding agent's `git push -u origin feature/x 2>&1 | tail -8`
    was refused with "push to 2>" because the operator survived into the push's destination list.
    """
    out, skip = [], False
    for token in segment:
        if skip:
            skip = False
            continue
        match = REDIRECT.match(token)
        if not match:
            out.append(token)
            continue
        operator, target = match.group(1), match.group(2)
        if target == "" and not operator.rstrip("0123456789").endswith("&"):
            skip = True
    return out


def strip_wrappers(segment):
    """Drop leading env assignments and wrapper programs (env, exec, sudo, timeout 5, ...)."""
    index = 0
    while index < len(segment):
        name = program_name(segment[index])
        if re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", segment[index]):
            index += 1
            continue
        if name in WRAPPERS:
            index += 1
            continue
        if name in WRAPPERS_WITH_VALUE:
            index += 1 + WRAPPERS_WITH_VALUE[name]
            continue
        break
    return segment[index:]


def split_git(segment, cwd=None):
    """Return (subcommand, args, repo_dir) when the segment is a git invocation, else None.

    repo_dir starts at cwd and each `-C <path>` is joined onto it, the way git itself resolves them.
    """
    if not segment or program_name(segment[0]) != "git":
        return None
    repo_dir, index = cwd, 1
    while index < len(segment):
        token = segment[index]
        if token == "-C" and index + 1 < len(segment):
            repo_dir = os.path.join(repo_dir or "", segment[index + 1])
            index += 2
            continue
        if token in GIT_GLOBAL_WITH_VALUE:
            index += 2
            continue
        if token.startswith("-"):
            index += 1
            continue
        return token, segment[index + 1:], repo_dir
    return None


def short_flags(args):
    letters = set()
    for arg in args:
        if re.match(r"^-[A-Za-z]+$", arg):
            letters.update(arg[1:])
    return letters


# ----------------------------------------------------------------------------------------------
# Evaluation
# ----------------------------------------------------------------------------------------------

def find_violation(command, profile, cwd=None, depth=0):
    """First (subcommand, rule label) the profile denies in this command, else None."""
    if depth > MAX_DEPTH:
        return None
    rules = PROFILES[profile]
    for raw in segments(tokenise(command)):
        segment = strip_wrappers(strip_redirects(raw))
        hit = check_git(segment, rules, cwd)
        if hit:
            return hit
        hit = check_nested(segment, profile, cwd, depth)
        if hit:
            return hit
    return None


def check_git(segment, rules, cwd):
    parsed = split_git(segment, cwd)
    if parsed is None:
        return None
    subcommand, args, repo_dir = parsed
    rule = rules.get(subcommand)
    if rule is None:
        return None
    words = [a for a in args if not a.startswith("-")]
    label = rule(short_flags(args), args, words, repo_dir)
    return (subcommand, label) if label else None


def check_nested(segment, profile, cwd, depth):
    """bash -c "git push", powershell -Command "git push", eval "...": look inside the strings."""
    if not segment or program_name(segment[0]) not in INTERPRETERS:
        return None
    for token in segment[1:]:
        if " " in token:
            hit = find_violation(token, profile, cwd, depth + 1)
            if hit:
                return hit
    return None


def decision(command, profile, cwd=None):
    """None when allowed, else the deny reason. cwd is where a bare git command runs."""
    hit = find_violation(command, profile, cwd)
    if hit is None:
        return None
    subcommand, label = hit
    return "git %s is blocked by the git-guardrail hook (%s profile, rule: %s). %s" % (
        subcommand, profile, label, ADVICE[profile])


# ----------------------------------------------------------------------------------------------
# Hook entry point
# ----------------------------------------------------------------------------------------------

def command_from_payload(payload):
    if payload.get("tool_name") not in {"Bash", "PowerShell"}:
        return None
    command = payload.get("tool_input", {}).get("command")
    return command if isinstance(command, str) and command.strip() else None


def cwd_from_payload(payload):
    cwd = payload.get("cwd")
    return cwd if isinstance(cwd, str) and cwd.strip() else None


def deny_json(reason):
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": HOOK_EVENT,
        "permissionDecision": "deny",
        "permissionDecisionReason": reason,
    }})


def log_deny(path, payload, reason):
    if not path:
        return
    row = {
        "ts": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "cwd": payload.get("cwd"),
        "tool": payload.get("tool_name"),
        "command": payload.get("tool_input", {}).get("command"),
        "reason": reason,
    }
    try:
        with open(path, "a", encoding="utf-8") as handle:
            handle.write(json.dumps(row) + "\n")
    except OSError:
        pass


def run_hook(profile, log_path, stdin):
    try:
        payload = json.load(stdin)
    except (ValueError, OSError):
        return 0  # fail open: an unreadable payload never blocks real work
    if not isinstance(payload, dict):
        return 0
    command = command_from_payload(payload)
    if command is None:
        return 0
    reason = decision(command, profile, cwd_from_payload(payload))
    if reason is None:
        return 0
    log_deny(log_path, payload, reason)
    print(deny_json(reason))
    return 0


def explain(profile, command, cwd):
    reason = decision(command, profile, cwd)
    print("ALLOW" if reason is None else "DENY  " + reason)
    return 0 if reason is None else 1


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--profile", choices=sorted(PROFILES), default="handback")
    parser.add_argument("--log", default=None, help="append one JSON line per deny to this file")
    parser.add_argument("--explain", metavar="COMMAND", help="print the decision for COMMAND and exit")
    parser.add_argument("--cwd", default=None, help="with --explain: the directory a bare git command would run in")
    parser.add_argument("--version", action="version", version="git-guardrail " + VERSION)
    args = parser.parse_args(argv)
    if args.explain is not None:
        return explain(args.profile, args.explain, args.cwd)
    return run_hook(args.profile, args.log, sys.stdin)


if __name__ == "__main__":
    sys.exit(main())
