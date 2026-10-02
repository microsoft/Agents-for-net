$ErrorActionPreference = "Stop"

$forbiddenPaths = @(
    "docs/superpowers/**"
    ".superpowers/**"
    ".worktrees/**"
)

$trackedArtifacts = @(& git ls-files -- $forbiddenPaths)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect tracked repository files."
}

if ($trackedArtifacts.Count -gt 0) {
    Write-Error ("Local agent artifacts must not be committed:`n{0}" -f ($trackedArtifacts -join "`n"))
    exit 1
}

Write-Host "No tracked local agent artifacts found."
