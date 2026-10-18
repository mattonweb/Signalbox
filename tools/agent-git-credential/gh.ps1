# gh shim for agent sessions, PowerShell form. Installed to ~\bin\gh.ps1 by tools/agent-git-credential/install.sh.
# PowerShell resolves gh.ps1 before gh.exe only when ~\bin precedes the GitHub CLI folder on PATH; the Windows
# user PATH comes after the system PATH, so this shim is reached only where that order holds. Agent sessions
# use the Bash tool for gh, where the sh shim is always first. See COMPUTER-SETUP.md in the Signalbox repo.

$realGh = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'
$helper = Join-Path $env:USERPROFILE 'bin\agent-git-credential\git-credential-github-app.exe'

if ($env:AGENT_GIT_IDENTITY -ne 'github-app') {
    & $realGh @args
    exit $LASTEXITCODE
}

$remote = git remote get-url origin 2>$null
if (-not $remote) {
    Write-Error 'gh: this session acts as a GitHub App, which needs a repository with an origin remote; run gh from inside the repository.'
    exit 1
}
$repository = $remote -replace '^(https://github\.com/|git@github\.com:)', '' -replace '\.git$', ''
$token = & $helper token --repository $repository
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
$env:GH_TOKEN = $token
& $realGh @args
exit $LASTEXITCODE
