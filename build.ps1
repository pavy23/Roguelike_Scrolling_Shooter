# Windows 플레이어 빌드 체크포인트 스크립트 (사람 전용 — AGENTS.md §2)
# 사용법:  powershell -ExecutionPolicy Bypass -File .\build.ps1
# 결과물:  Builds\Dev\RSS.exe  (Builds/는 gitignore 대상)
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe",
    [string]$OutDir = "Builds\Dev"
)

$root = $PSScriptRoot
$lock = Join-Path $root "Temp\UnityLockfile"
if (Test-Path $lock) {
    Write-Host "중단: Unity 에디터(또는 다른 배치 작업)가 이 프로젝트를 열고 있습니다. 닫고 다시 실행하세요." -ForegroundColor Red
    exit 1
}

$out = Join-Path $root $OutDir
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out "RSS.exe"
$log = Join-Path $root "Builds\build.log"

Write-Host "빌드 시작 → $exe"
$p = Start-Process -FilePath $UnityExe -ArgumentList @(
    '-batchmode', '-quit',
    '-projectPath', $root,
    '-buildWindows64Player', $exe,
    '-logFile', $log
) -PassThru -Wait

if ($p.ExitCode -eq 0 -and (Test-Path $exe)) {
    Write-Host "빌드 성공: $exe" -ForegroundColor Green
    Write-Host "실행: & '$exe'"
} else {
    Write-Host "빌드 실패 (ExitCode $($p.ExitCode)). 로그 확인: $log" -ForegroundColor Red
    Select-String -Path $log -Pattern "error CS|Build Finished|Exiting batchmode" | Select-Object -Last 10 | ForEach-Object { $_.Line }
    exit 1
}
