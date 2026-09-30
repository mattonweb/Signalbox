<#
.SYNOPSIS
    Creates one Windows junction from an agent home to a shared skill in the skill's home repo.

.DESCRIPTION
    A shared skill lives once, at <SourceRoot>\<Source>\skills\<name>, in the repo whose owner maintains
    it. It is never copied. This script makes <AgentHome>\.claude\skills\<name> a junction to that folder,
    so every agent that reads it reads the same bytes, always the latest the owner has committed and this
    machine has pulled. Claude Code discovers skills only under .claude\skills\, so the repo that stores a
    skill at its root skills\ folder needs a junction of its own too. See REPO-SETUP.md.

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
    The skill folder name, e.g. code-roadbed-csharp.

.PARAMETER Source
    The name of the skill's home repo under -SourceRoot, e.g. Roadbed.

.PARAMETER SourceRoot
    Where repo clones live. Defaults to C:\Source.

.EXAMPLE
    .\New-SkillJunction.ps1 -AgentHome C:\Source\MyApp -SkillName code-roadbed-csharp -Source Roadbed
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AgentHome,
    [Parameter(Mandatory = $true)] [string] $SkillName,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Source,
    [string] $SourceRoot = 'C:\Source'
)

$ErrorActionPreference = 'Stop'

# A skill has ONE home: the repo whose owner maintains it. The junction points at that home, never at a copy.
$sourcePath = Join-Path (Join-Path (Join-Path $SourceRoot $Source) 'skills') $SkillName
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    throw "Skill not found: $sourcePath. Clone or pull $Source under $SourceRoot first, or check the repo and skill names."
}
$sourceItem = Get-Item -LiteralPath $sourcePath -Force
if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "$sourcePath is itself a junction. Point at the skill's home repo, not at another junction."
}
if (-not (Test-Path -LiteralPath $AgentHome -PathType Container)) {
    throw "Agent home not found: $AgentHome"
}

$skillsDir = Join-Path (Join-Path $AgentHome '.claude') 'skills'
if (-not (Test-Path -LiteralPath $skillsDir)) {
    New-Item -ItemType Directory -Path $skillsDir | Out-Null
}
$target = Join-Path $skillsDir $SkillName

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
    $tracked = @(git -C $AgentHome ls-files -- ".claude/skills/$SkillName" 2>$null)
    if ($tracked.Count -gt 0) {
        throw "$AgentHome still tracks $($tracked.Count) file(s) under .claude/skills/$SkillName on the current branch. Commit the copy's removal, merge it, and pull it on this machine BEFORE creating the junction; otherwise git will delete the skill's home through the junction."
    }
}

New-Item -ItemType Junction -Path $target -Target $sourcePath | Out-Null
Write-Output "Created junction: $target -> $sourcePath"

# Keep the junction out of the agent home's git history, once.
$gitDir = Join-Path $AgentHome '.git'
if (Test-Path -LiteralPath $gitDir) {
    $gitignore = Join-Path $AgentHome '.gitignore'
    $line = ".claude/skills/$SkillName/"
    $present = $false
    if (Test-Path -LiteralPath $gitignore) {
        $present = (Get-Content -LiteralPath $gitignore) -contains $line
    }
    if (-not $present) {
        Add-Content -LiteralPath $gitignore -Encoding utf8 -Value @(
            '',
            "# Shared skill reached by junction point from $sourcePath (see REPO-SETUP.md in the Signalbox repo); never commit the junction",
            $line
        )
        Write-Output "Added to ${gitignore}: $line"
    }
}
