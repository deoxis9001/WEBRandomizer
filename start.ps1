# Lance le serveur WebRandomizer en mode developpement
param(
    [string]$Url = "http://localhost:5000",
    [ValidateSet("Development", "Production")]
    [string]$Env = "Development"
)

$Project = "$PSScriptRoot\WebRandomizer\WebRandomizer.csproj"

$env:ASPNETCORE_ENVIRONMENT = $Env
$env:ASPNETCORE_URLS        = $Url

Write-Host "=== Demarrage WebRandomizer ===" -ForegroundColor Yellow
Write-Host "URL  : $Url" -ForegroundColor Cyan
Write-Host "Mode : $Env" -ForegroundColor Cyan
Write-Host "Ouvrez votre navigateur sur $Url`n" -ForegroundColor Green

dotnet run --project $Project --no-build
