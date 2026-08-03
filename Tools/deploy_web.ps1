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

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & git @Args
    # 네이티브 git 실패는 $ErrorActionPreference로 안 잡힌다 - 직접 본다.
    # 이걸 안 봐서 "배포 완료"라고 찍어 놓고 실제로는 아무것도 안 올라간 적이 있다
    # (새 클론에 git 신원이 없어 commit이 조용히 실패했다).
    if ($LASTEXITCODE -ne 0) { throw "git $($Args -join ' ') 실패 (exit $LASTEXITCODE)" }
}

if (-not (Test-Path $DeployRepo)) {
    Invoke-Git clone --depth 1 $RemoteUrl $DeployRepo
} else {
    Invoke-Git -C $DeployRepo fetch --depth 1 origin main
    Invoke-Git -C $DeployRepo reset --hard origin/main
}

# 클론에는 신원이 없다. 본 저장소 설정을 그대로 물려준다.
$userName = (& git -C $root config user.name)
$userEmail = (& git -C $root config user.email)
if (-not $userName -or -not $userEmail) { throw "본 저장소에 git user.name/email이 없다." }
Invoke-Git -C $DeployRepo config user.name $userName
Invoke-Git -C $DeployRepo config user.email $userEmail

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

Invoke-Git -C $DeployRepo add Build index.html
Invoke-Git -C $DeployRepo commit -m $Message
Invoke-Git -C $DeployRepo push origin main

# 배포가 실제로 도달했는지 확인한다 - 올렸다고 끝이 아니다.
$deployed = (& git -C $DeployRepo rev-parse HEAD).Trim()
Write-Host "배포 커밋 $deployed"
Write-Host "배포 완료 — 스탬프 $stamp" -ForegroundColor Green
Write-Host "확인: https://pavy23.github.io/rss-play (반영까지 1~2분)"
