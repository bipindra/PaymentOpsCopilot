# Payment Ops Console - Local Run Script (PowerShell)

Write-Host "Starting Payment Ops Console..." -ForegroundColor Green

# Check if Docker is running
Write-Host "`nChecking Docker..." -ForegroundColor Yellow
try {
    docker ps | Out-Null
    Write-Host "Docker is running" -ForegroundColor Green
} catch {
    Write-Host "Docker is not running. Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

# Start Qdrant
Write-Host "`nStarting Qdrant..." -ForegroundColor Yellow
docker-compose up -d
Start-Sleep -Seconds 3

# Check if Qdrant is healthy
$qdrantHealthy = $false
for ($i = 0; $i -lt 10; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/health" -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $qdrantHealthy = $true
            Write-Host "Qdrant is healthy" -ForegroundColor Green
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $qdrantHealthy) {
    Write-Host "Qdrant failed to start. Check docker-compose logs." -ForegroundColor Red
    exit 1
}

# Check for OpenAI API key
if (-not $env:OPENAI_API_KEY) {
    Write-Host "`nWARNING: OPENAI_API_KEY environment variable is not set." -ForegroundColor Yellow
    Write-Host "Set it with: `$env:OPENAI_API_KEY = 'your-key-here'" -ForegroundColor Yellow
    Write-Host "Or (recommended) set a .NET User Secret:" -ForegroundColor Yellow
    Write-Host "  cd src/PaymentOps.Backend; dotnet user-secrets set `"OpenAI:ApiKey`" `"your-key-here`"" -ForegroundColor Yellow
    Write-Host "  cd src/PaymentOps.Backend; dotnet user-secrets set `"Qdrant:BaseUrl`" `"http://localhost:6333`"" -ForegroundColor Yellow
    Write-Host "Or set it permanently in System Properties > Environment Variables" -ForegroundColor Yellow
}

# Start backend
Write-Host "`nStarting Backend..." -ForegroundColor Yellow
$backendJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    Set-Location "src/PaymentOps.Backend"
    dotnet run
}

# Wait a bit for backend to start
Start-Sleep -Seconds 5

# Start frontend
Write-Host "Starting Frontend..." -ForegroundColor Yellow
$frontendJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    Set-Location "frontend/payment-ops-ui"
    npm install
    ng serve --open
}

Write-Host "`nServices starting..." -ForegroundColor Green
Write-Host "Backend: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:4200" -ForegroundColor Cyan
Write-Host "Qdrant: http://localhost:6333" -ForegroundColor Cyan
Write-Host "`nPress Ctrl+C to stop all services" -ForegroundColor Yellow

# Wait for user interrupt
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host "`nStopping services..." -ForegroundColor Yellow
    Stop-Job $backendJob, $frontendJob -ErrorAction SilentlyContinue
    Remove-Job $backendJob, $frontendJob -ErrorAction SilentlyContinue
    docker-compose down
    Write-Host "Services stopped" -ForegroundColor Green
}
