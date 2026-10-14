$ErrorActionPreference="Stop"
Set-Location (Split-Path $PSScriptRoot)
python scripts/verify-runtime.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python -m unittest discover -s scripts/tests
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python -m unittest discover -s services/AgentService/tests
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test Collaby.slnx -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Push-Location frontend
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm run build
    exit $LASTEXITCODE
} finally { Pop-Location }
