# Signalbox

Reusable GitHub Actions workflows, public skills, and the machine and repo setup that go with them, for a
trunk-based, agent-driven CI/CD pipeline. A product repo carries a thin caller pinned to a version tag of
this repo and gets the whole pipeline: pull-request checks, merge-lane checks, tagging on merge, and a
build-once release. Every private value a workflow needs arrives as an input from the caller; nothing
here knows a hostname, a secret or a name.

A signalbox is where a signalman works the interlocking, the system that lets a train move only when its
route is set, locked and clear. This repo is the public face of that system for the pipeline it serves.

## Start here

- [COMPUTER-SETUP.md](COMPUTER-SETUP.md), once per machine: a fail-closed secrets scan on every commit,
  with Betterleaks, and how to add your own private rules on top without editing the base.
- [REPO-SETUP.md](REPO-SETUP.md), once per repo: reach a shared skill by junction point instead of a copy,
  with the Roadbed skill as the worked example.

## What is here

- `.github/workflows/` holds one reusable workflow per pipeline step, named for the step number it
  triggers on. Step numbers are frozen and never reused.
- `skills/` holds public skills for coding agents, each with an owner and a version in its frontmatter.
  Every skill is owned by Matt and changes only in an attended session with him.
- `tools/secret-scan/` is the installer, hook and generic base config behind COMPUTER-SETUP.md.
- `tools/New-SkillJunction.ps1` is the script behind REPO-SETUP.md.
- `docs/` holds the public contract for each workflow: inputs, secrets and what it reports.
- `CLAUDE.md` is the working agreement for coding-agent sessions here, including the human-gated commit rule.
- `mcp.json.template` is the shape of the gitignored `.mcp.json`; the bearer key is placed by hand.

`main` is protected and takes pull requests only. Callers pin to a tag, and the tag moves only when a
human moves it. Opened 2026-09-27.
