# Режим разработки: Kubernetes backend и frontend через Vite.
# Использование: .\dev.ps1   или   pwsh -File dev.ps1

Set-Location $PSScriptRoot

& (Join-Path $PSScriptRoot 'scripts/local.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nStarting frontend (npm run dev) at http://localhost:3000 ..." -ForegroundColor Green
Set-Location frontend
npm run dev
