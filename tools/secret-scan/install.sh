#!/bin/sh
# Install the machine-wide secret-scan commit hook on a Windows machine (Git Bash).
# Run once per computer from the Signalbox repo root. See COMPUTER-SETUP.md.
#   Git Bash:    sh tools/secret-scan/install.sh
#   PowerShell:  & "$(Split-Path (Split-Path (Get-Command git).Source))\bin\sh.exe" tools/secret-scan/install.sh
#   (PowerShell has no `sh`; that line runs the sh.exe that ships with Git for Windows.)
#
# Pinned: Betterleaks 1.8.1, windows_x64, from the official GitHub release.
# The expected SHA-256 is written HERE, not fetched, so a swapped release asset fails.
# Safe to re-run: it never overwrites an existing ~/.githooks/betterleaks.toml, so a private
# overlay placed there survives a reinstall.
set -e

VERSION="1.8.1"
ASSET="betterleaks_${VERSION}_windows_x64.zip"
SHA256="94310d028285a1bcce7f160bc19eb62f87de6460c95bfd4319151ef5b501ed3f"
URL="https://github.com/betterleaks/betterleaks/releases/download/v${VERSION}/${ASSET}"
HERE="$(cd "$(dirname "$0")" && pwd)"
HOOKS="$HOME/.githooks"
PASSTHROUGH="pre-push post-checkout post-commit post-merge commit-msg prepare-commit-msg"

# 1. Binary (skip download when the pinned version is already installed)
mkdir -p "$HOME/bin"
if [ "$("$HOME/bin/betterleaks.exe" version 2>/dev/null)" != "$VERSION" ]; then
  TMP="$(mktemp -d)"
  curl -sSfL -o "$TMP/$ASSET" "$URL"
  echo "$SHA256 *$TMP/$ASSET" | sha256sum -c - >/dev/null || { echo "checksum MISMATCH - aborting" >&2; exit 1; }
  unzip -o -q "$TMP/$ASSET" betterleaks.exe -d "$TMP"
  cp "$TMP/betterleaks.exe" "$HOME/bin/betterleaks.exe"
  rm -rf "$TMP"
fi
echo "betterleaks $("$HOME/bin/betterleaks.exe" version) in $HOME/bin"

# 2. Hooks (always refreshed) and the base config (only if none is there yet)
mkdir -p "$HOOKS"
cp "$HERE/hooks/pre-commit" "$HOOKS/pre-commit"
for h in $PASSTHROUGH; do cp "$HERE/hooks/passthrough" "$HOOKS/$h"; done
if [ -f "$HOOKS/betterleaks.toml" ]; then
  echo "config: kept existing $HOOKS/betterleaks.toml (not overwritten)"
else
  cp "$HERE/betterleaks.toml" "$HOOKS/betterleaks.toml"
  echo "config: installed the generic base at $HOOKS/betterleaks.toml"
fi
chmod +x "$HOOKS"/*
echo "hooks in $HOOKS"

# 3. Machine-wide wiring
git config --global core.hooksPath "$HOOKS"
echo "core.hooksPath = $(git config --global --get core.hooksPath)"

# 4. Report repos whose LOCAL hooksPath would override the machine-wide one.
#    Edit the roots below to match where your repos live.
echo "Repos with a local core.hooksPath (these are NOT covered until it is removed):"
for d in /c/Source/*/ ; do
  [ -d "$d/.git" ] || continue
  lp="$(git -C "$d" config --local --get core.hooksPath || true)"
  [ -n "$lp" ] && echo "  $d -> $lp"
done
echo "done."
