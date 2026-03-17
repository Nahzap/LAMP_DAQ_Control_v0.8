# ========================================================
# Signal Manager Test Suite Runner
# Uses MSBuild + vstest.console for WPF/.NET Framework projects
# ========================================================

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Signal Manager Test Suite Runner (MSBuild + VSTest)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# Tool paths
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
$vstestPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"

# Check if tools exist
if (-not (Test-Path $msbuildPath)) {
    Write-Host "[ERROR] MSBuild not found at: $msbuildPath" -ForegroundColor Red
    Write-Host "Please install Visual Studio 2022 Professional" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $vstestPath)) {
    Write-Host "[ERROR] vstest.console.exe not found at: $vstestPath" -ForegroundColor Red
    Write-Host "Please install Visual Studio Test Platform" -ForegroundColor Yellow
    exit 1
}

Write-Host "[INFO] Using MSBuild: $msbuildPath" -ForegroundColor Gray
Write-Host "[INFO] Using VSTest:  $vstestPath" -ForegroundColor Gray
Write-Host ""

# ========================================================
# STEP 1: Clean previous builds
# ========================================================
Write-Host "[1/3] Cleaning previous builds..." -ForegroundColor Yellow

& $msbuildPath "LAMP_DAQ_Control_v0.8.SignalManager.Tests.csproj" /t:Clean /p:Configuration=Release /v:minimal /nologo > $null

Write-Host "[SUCCESS] Clean completed" -ForegroundColor Green
Write-Host ""

# ========================================================
# STEP 2: Restore test project NuGet packages
# ========================================================
Write-Host "[2/3] Restoring NuGet packages..." -ForegroundColor Yellow

& $msbuildPath "LAMP_DAQ_Control_v0.8.SignalManager.Tests.csproj" /t:Restore /p:Configuration=Release /v:quiet /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] NuGet restore failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] NuGet packages restored" -ForegroundColor Green
Write-Host ""

# ========================================================
# STEP 3: Build test project (and dependencies)
# ========================================================
Write-Host "[3/3] Building test project and dependencies..." -ForegroundColor Yellow
Write-Host ""

# Build test project (will build main project if needed)
& $msbuildPath "LAMP_DAQ_Control_v0.8.SignalManager.Tests.csproj" /t:Build /p:Configuration=Release /p:ResolveNuGetPackages=false /v:minimal /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "[HINT] Make sure the main project is already built:" -ForegroundColor Yellow
    Write-Host "  1. Open solution in Visual Studio 2022" -ForegroundColor Yellow
    Write-Host "  2. Build -> Build Solution" -ForegroundColor Yellow
    Write-Host "  3. Re-run this test script" -ForegroundColor Yellow
    exit 1
}

Write-Host "[SUCCESS] Test project built successfully" -ForegroundColor Green
Write-Host ""

# ========================================================
# STEP 4: Run tests with vstest.console.exe
# ========================================================
Write-Host "[4/4] Running tests with VSTest Console Runner..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan

$testDllPath = "bin\Release\net472\LAMP_DAQ_Control_v0.8.SignalManager.Tests.dll"

if (-not (Test-Path $testDllPath)) {
    Write-Host ""
    Write-Host "[ERROR] Test DLL not found: $testDllPath" -ForegroundColor Red
    exit 1
}

# Run tests with detailed output
& $vstestPath $testDllPath /Logger:console /Platform:x64

$testExitCode = $LASTEXITCODE

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan

# ========================================================
# SUMMARY
# ========================================================
Write-Host ""
if ($testExitCode -eq 0) {
    Write-Host "[SUCCESS] All tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test DLL: $testDllPath" -ForegroundColor Gray
} else {
    Write-Host "[FAILURE] Some tests failed or encountered errors!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Exit Code: $testExitCode" -ForegroundColor Yellow
    Write-Host "Review output above for failure details" -ForegroundColor Yellow
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

exit $testExitCode
