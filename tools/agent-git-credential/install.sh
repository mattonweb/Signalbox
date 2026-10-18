#!/bin/sh
# Install the agent git identity tooling on a Windows machine (Git Bash). Run once per computer from the
# Signalbox repo root, and again after pulling a new version. See COMPUTER-SETUP.md.
#   Git Bash:    sh tools/agent-git-credential/install.sh
#   PowerShell:  & "$(Split-Path (Split-Path (Get-Command git).Source))\bin\sh.exe" tools/agent-git-credential/install.sh
#
# What it does:
#   1. Publishes git-credential-github-app (needs the .NET 10 SDK) to ~/bin/agent-git-credential/.
#   2. Places the gh shims at ~/bin/gh and ~/bin/gh.ps1.
#   3. Creates the registration database, keyed to this Windows user account, if there is none yet.
# It changes no git configuration: a repository opts in through the env block in its .claude/settings.json
# (REPO-SETUP.md), so sessions outside an agent's keep the user's own identity.
set -e

HERE="$(cd "$(dirname "$0")" && pwd)"
TARGET="$HOME/bin/agent-git-credential"
HELPER="$TARGET/git-credential-github-app.exe"
REAL_GH="/c/Program Files/GitHub CLI/gh.exe"

# 1. The helper
command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found: install the .NET 10 SDK first." >&2; exit 1; }
mkdir -p "$TARGET"
dotnet publish "$HERE/src/AgentGitCredential/AgentGitCredential.csproj" --configuration Release --runtime win-x64 --self-contained false --output "$TARGET" --nologo --verbosity quiet
echo "helper: $HELPER"

# 2. The gh shims
mkdir -p "$HOME/bin"
cp "$HERE/gh" "$HOME/bin/gh"
cp "$HERE/gh.ps1" "$HOME/bin/gh.ps1"
chmod +x "$HOME/bin/gh"
echo "shims: $HOME/bin/gh and $HOME/bin/gh.ps1"
if [ ! -x "$REAL_GH" ]; then
  echo "GitHub CLI not found at $REAL_GH: install it with  winget install --id GitHub.cli -e" >&2
fi

# 3. The registration database: create it once; afterwards prove it still opens
DATABASE="$LOCALAPPDATA/Signalbox/agent-git-credential/github-apps.kdbx"
if [ -f "$DATABASE" ]; then
  "$HELPER" list >/dev/null && echo "database: present and opens"
else
  "$HELPER" init
fi

echo "done. Register a repository's GitHub App with:"
echo "  $HELPER set --repository owner/name --app-id N --installation-id N --private-key-file path.pem"
