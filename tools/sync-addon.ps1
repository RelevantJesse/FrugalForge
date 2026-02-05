param(
    [string]$AddonName = "FrugalForge",
    [string]$WowPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_"
)

$src = Join-Path (Get-Location) $AddonName
$dst = Join-Path $WowPath "Interface\AddOns\$AddonName"

if (-not (Test-Path $src)) {
    Write-Error "Source addon folder not found: $src"
    exit 1
}

New-Item -ItemType Directory -Force -Path $dst | Out-Null
robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }

Write-Output "Synced $src -> $dst"
