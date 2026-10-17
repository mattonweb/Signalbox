<#
.SYNOPSIS
    Creates one Windows junction from an agent home to a shared skill, or a shared hook, in its home repo.

.DESCRIPTION
    A shared skill lives once, at <SourceRoot>\<Source>\skills\<name>, in the repo whose owner maintains
    it. It is never copied. This script makes <AgentHome>\.claude\skills\<name> a junction to that folder,
    so every agent that reads it reads the same bytes, always the latest the owner has committed and this
    machine has pulled. Claude Code discovers skills only under .claude\skills\, so the repo that stores a
    skill at its root skills\ folder needs a junction of its own too. See REPO-SETUP.md.

    With -Kind Hook the same is done for a shared hook: the source is <SourceRoot>\<Source>\tools\<name>
    and the junction is <AgentHome>\.claude\hooks\<name>. The repo's .claude\settings.json then names the
    hook by that path. The git-guardrail hook in this repo is the first one (tools\git-guardrail\README.md).

    Rules it enforces:
      - It never overwrites a real folder. If the target exists and is not a junction to the same source,
        it stops and says so. Remove or rename the copy by hand first.
      - It refuses a source that is itself a junction: point at the home, never at another junction.
      - It is idempotent: an existing junction to the same source is reported and left alone.
      - If the agent home is a git repo, it adds the junction path to that repo's .gitignore (once), so
        the junction is never committed as if it were content.

    Runs on Windows PowerShell 5.1; junctions need no administrator rights.

.PARAMETER AgentHome
    The folder that holds the agent's .claude\ directory: a repo clone, or any Claude Code project folder.

.PARAMETER SkillName
    The skill folder name, e.g. code-roadbed-csharp; with -Kind Hook, the hook folder name, e.g. git-guardrail.

.PARAMETER Source
    The name of the skill's or hook's home repo under -SourceRoot, e.g. Roadbed or Signalbox.

.PARAMETER SourceRoot
    Where repo clones live. Defaults to C:\Source.

.PARAMETER Kind
    Skill (default): skills\<name> in the home, .claude\skills\<name> in the agent home.
    Hook: tools\<name> in the home, .claude\hooks\<name> in the agent home.

.EXAMPLE
    .\New-SkillJunction.ps1 -AgentHome C:\Source\MyApp -SkillName code-roadbed-csharp -Source Roadbed

.EXAMPLE
    .\New-SkillJunction.ps1 -AgentHome C:\Source\MyApp -SkillName git-guardrail -Source Signalbox -Kind Hook
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AgentHome,
    [Parameter(Mandatory = $true)] [string] $SkillName,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Source,
    [string] $SourceRoot = 'C:\Source',
    [ValidateSet('Skill', 'Hook')] [string] $Kind = 'Skill'
)

$ErrorActionPreference = 'Stop'

# One kind, two folder names: where the home stores it, and where Claude Code discovers it.
if ($Kind -eq 'Hook') {
    $storeFolder = 'tools'
    $discoverFolder = 'hooks'
} else {
    $storeFolder = 'skills'
    $discoverFolder = 'skills'
}
$kindWord = $Kind.ToLower()

# A skill or hook has ONE home: the repo whose owner maintains it. The junction points at that home, never at a copy.
$sourcePath = Join-Path (Join-Path (Join-Path $SourceRoot $Source) $storeFolder) $SkillName
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    throw "$Kind not found: $sourcePath. Clone or pull $Source under $SourceRoot first, or check the repo and $kindWord names."
}
$sourceItem = Get-Item -LiteralPath $sourcePath -Force
if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "$sourcePath is itself a junction. Point at the $kindWord's home repo, not at another junction."
}
if (-not (Test-Path -LiteralPath $AgentHome -PathType Container)) {
    throw "Agent home not found: $AgentHome"
}

$discoverDir = Join-Path (Join-Path $AgentHome '.claude') $discoverFolder
if (-not (Test-Path -LiteralPath $discoverDir)) {
    New-Item -ItemType Directory -Path $discoverDir | Out-Null
}
$target = Join-Path $discoverDir $SkillName
$relativePath = ".claude/$discoverFolder/$SkillName"

if (Test-Path -LiteralPath $target) {
    $item = Get-Item -LiteralPath $target -Force
    $isJunction = ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    if ($isJunction -and $item.Target -and ($item.Target -ieq $sourcePath)) {
        Write-Output "Already a junction to $sourcePath : $target"
        return
    }
    if ($isJunction) {
        throw "$target is a junction to '$($item.Target)', not to $sourcePath. Remove it by hand if it should change: [System.IO.Directory]::Delete('$target', `$false)"
    }
    throw "$target is a real folder (a copy). Remove or rename it by hand before creating the junction; this script never deletes content."
}

# Refuse while the agent home's git index still tracks files under the target path. Git deletes and
# restores tracked files THROUGH a junction, so a junction created before the copy's removal is merged
# lets the next pull, merge or branch switch empty the skill's home. Seen 2026-09-29: a pull that merged
# the copy's deletion removed all 17 files from the home repo's folder.
$gitDir = Join-Path $AgentHome '.git'
if (Test-Path -LiteralPath $gitDir) {
    $tracked = @(git -C $AgentHome ls-files -- $relativePath 2>$null)
    if ($tracked.Count -gt 0) {
        throw "$AgentHome still tracks $($tracked.Count) file(s) under $relativePath on the current branch. Commit the copy's removal, merge it, and pull it on this machine BEFORE creating the junction; otherwise git will delete the $kindWord's home through the junction."
    }
}

New-Item -ItemType Junction -Path $target -Target $sourcePath | Out-Null
Write-Output "Created junction: $target -> $sourcePath"

# Keep the junction out of the agent home's git history, once.
if (Test-Path -LiteralPath $gitDir) {
    $gitignore = Join-Path $AgentHome '.gitignore'
    $line = "$relativePath/"
    $present = $false
    if (Test-Path -LiteralPath $gitignore) {
        $present = (Get-Content -LiteralPath $gitignore) -contains $line
    }
    if (-not $present) {
        Add-Content -LiteralPath $gitignore -Encoding utf8 -Value @(
            '',
            "# Shared $kindWord reached by junction point from $sourcePath (see REPO-SETUP.md in the Signalbox repo); never commit the junction",
            $line
        )
        Write-Output "Added to ${gitignore}: $line"
    }
}
