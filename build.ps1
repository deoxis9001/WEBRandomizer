# Build et publie WebRandomizer pour Windows et Linux
param(
    [ValidateSet("win-x64", "linux-x64", "all")]
    [string]$Target = "all",
    [string]$OutDir = "$PSScriptRoot\publish"
)

$Project = "$PSScriptRoot\WebRandomizer\WebRandomizer.csproj"

function Publish($rid) {
    $dest = "$OutDir\$rid"
    Write-Host "Publication pour $rid -> $dest" -ForegroundColor Cyan
    dotnet publish $Project -c Release -r $rid --self-contained -o $dest
    if ($LASTEXITCODE -ne 0) { Write-Error "Echec publication $rid"; exit 1 }
    Write-Host "OK : $dest" -ForegroundColor Green
}

Write-Host "=== Build WebRandomizer ===" -ForegroundColor Yellow
dotnet build "$PSScriptRoot\WEBRandomizer.slnx" -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build echoue"; exit 1 }

if ($Target -eq "win-x64" -or $Target -eq "all") { Publish "win-x64" }
if ($Target -eq "linux-x64" -or $Target -eq "all") { Publish "linux-x64" }

Write-Host "`nPublications disponibles dans : $OutDir" -ForegroundColor Yellow
