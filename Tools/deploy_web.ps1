# WebGL 배포 (GitHub Pages: pavy23/rss-play)
#
# 왜 스크립트인가: 손으로 배포하다 두 번 사고가 났다.
#   1) Builds/Web를 통째로 복사해 배포 저장소의 **커스텀 index.html을 덮어썼다**
#      → 전체화면 레이아웃과 로딩 화면이 Unity 기본 템플릿으로 되돌아갔다.
#   2) 캐시 무효화가 없어 배포해도 플레이어는 **옛 빌드를 계속 돌렸다**
#      → Unity WebGL 로더가 data/wasm을 IndexedDB에 캐시하는데 키가
#        (URL + productVersion)이라 둘 다 고정이면 새 파일을 받지 않는다.
#        여러 번 배포하고도 "여전히 그대로"라는 보고가 나온 원인이 이것이다.
#
# 그래서 이 스크립트는 **Build/만 갱신하고 index.html의 스탬프만 바꾼다.**
#
# 사용법:
#   powershell -ExecutionPolicy Bypass -File Tools\deploy_web.ps1 -Message "build52: ..."
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [string]$DeployRepo = "$env:TEMP\rss-play-deploy",
    [string]$RemoteUrl = "https://github.com/pavy23/rss-play.git"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$buildDir = Join-Path $root "Builds\Web\Build"
if (-not (Test-Path $buildDir)) { throw "빌드 결과가 없다: $buildDir (unity build 먼저)" }

if (-not (Test-Path $DeployRepo)) {
    git clone --depth 1 $RemoteUrl $DeployRepo
} else {
    git -C $DeployRepo fetch --depth 1 origin main
    git -C $DeployRepo reset --hard origin/main
}

# Build/ 페이로드만 교체한다. index.html·TemplateData는 배포 저장소 것이 원본이다.
Copy-Item (Join-Path $buildDir '*') (Join-Path $DeployRepo 'Build') -Recurse -Force

# 캐시 스탬프 갱신 — 이게 없으면 배포가 플레이어에게 도달하지 않는다.
$indexPath = Join-Path $DeployRepo 'index.html'
$stamp = (Get-Date -Format 'yyyyMMdd-HHmm')
$index = Get-Content $indexPath -Raw
if ($index -notmatch 'var BUILD_STAMP = "([^"]*)"') {
    throw "index.html에 BUILD_STAMP가 없다 — 캐시 무효화가 빠진 채로 배포하면 안 된다."
}
$index = [regex]::Replace($index, 'var BUILD_STAMP = "[^"]*"', "var BUILD_STAMP = `"$stamp`"")
Set-Content $indexPath $index -NoNewline -Encoding utf8

git -C $DeployRepo add Build index.html
git -C $DeployRepo commit -m $Message
git -C $DeployRepo push origin main
Write-Host "배포 완료 — 스탬프 $stamp" -ForegroundColor Green
Write-Host "확인: https://pavy23.github.io/rss-play (반영까지 1~2분)"
