# 중형·중간보스 아이들 애니메이션 재생성 — 새 네이티브 크기 아트 기준
#
# 왜: ApplyIdleAnimation이 매 프레임 renderer.sprite를 anim 프레임으로 바꾸는데,
# 스케일은 기본 스프라이트 기준으로 한 번만 계산된다. 기본만 76x56으로 키우고
# 프레임을 옛 64x48로 두면 적이 애니메이션되는 순간 작아지고 찌그러진다.
#
# 각 적의 새 정지 아트를 첫 프레임으로 animate-with-text-v3에 넣어 6프레임을 만들고
# (API가 짝수 프레임만 허용), 앞 5장을 기존 규약(anim_*_00~04)에 맞춰 배치한다.
# 같은 크기로 정리해 art-input/anim_<prefix>_XX.png 로 배치한다 (씬 빌더 규약).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $env:PIXELLAB_API_KEY) { $env:PIXELLAB_API_KEY = [Environment]::GetEnvironmentVariable('PIXELLAB_API_KEY', 'User') }

# prefix = 씬 빌더 AddAnim 접두, art = art-input의 정지 스프라이트, size = 후처리 목표,
# action = 정지 비행 중의 미세 움직임 (이동은 시뮬이 하므로 제자리 동작만)
$jobs = @(
    @{ prefix='mini_destroyer'; art='enemy_mini_destroyer.png'; size='76x56'; seed=7101
       action='idle hover, engine vents flare and dim, hull plates rattle slightly' }
    @{ prefix='mini_horror';    art='enemy_mini_horror.png';    size='72x56'; seed=7102
       action='idle pulsating, fleshy mass breathes, maw slowly opens and closes, eye glows' }
    @{ prefix='mini_walker';    art='enemy_mini_walker.png';    size='80x60'; seed=7103
       action='idle stance, cannon barrels recoil slightly, warning lights blink' }
    @{ prefix='mini_crystal';   art='enemy_mini_crystal.png';   size='76x56'; seed=7104
       action='idle float, prism shards slowly orbit the glowing core, core brightness pulses' }
    @{ prefix='zako_tank';      art='enemy_tank.png';           size='40x32'; seed=7105
       action='idle rumble, treads shift slightly, sensor eye blinks' }
    @{ prefix='elite';          art='enemy_elite.png';          size='42x34'; seed=7106
       action='idle flight, engine glow flickers, wings tilt subtly' }
    @{ prefix='guardian';       art='enemy_guardian.png';       size='44x40'; seed=7107
       action='idle rotation, ring shells slowly rotate around the glowing core' }
    @{ prefix='shard_prism';    art='enemy_shard_prism.png';    size='44x40'; seed=7108
       action='idle float, crystal facets glint and refraction edges shimmer' }
)

foreach ($j in $jobs) {
    $prefix = $j.prefix
    $outDir = "out/raw/anim_$prefix"
    Write-Host "=== $prefix ==="
    python artgen.py animate --first "../../../art-input/$($j.art)" --action $j.action --frames 6 --seed $j.seed --out-dir $outDir
    if (-not (Test-Path "$outDir/frame_00.png")) { throw "$prefix 애니메이션 생성 실패" }
    $i = 0
    Get-ChildItem "$outDir/frame_*.png" | Sort-Object Name | Select-Object -First 5 | ForEach-Object {
        $n = "{0:D2}" -f $i
        python artgen.py post --src $_.FullName --target $j.size --colors 48 --no-trim --out "../../../art-input/anim_${prefix}_$n.png"
        $i++
    }
}
Write-Host "=== 8종 애니메이션 재생성 완료 ==="
