# mini_core 스프라이트 + 아이들 애니메이션 (REQ-063)
# 코어 테마 중간보스 — 앞선 4종(돌진·산탄·집중포·위치전환)의 성격을 조합한
# "종합 시험" 컨셉이라, 기계+결정이 섞인 코어 수호 구축물로 그린다.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not $env:PIXELLAB_API_KEY) { $env:PIXELLAB_API_KEY = [Environment]::GetEnvironmentVariable('PIXELLAB_API_KEY', 'User') }

python artgen.py pixen --prompt 'core guardian construct mid-boss, fusion of machine armor and glowing crystal, central burning energy core with rotating armored plates and cannon mounts, side view facing left, dark steel with red-orange core glow and violet crystal accents, hi-bit pixel art' --width 76 --height 56 --seed 6109 --out out/raw/mini_core_L.png
if (-not (Test-Path out/raw/mini_core_L.png)) { throw "mini_core 생성 실패" }
python artgen.py post --src out/raw/mini_core_L.png --target 76x56 --colors 48 --no-trim --out ../../../art-input/enemy_mini_core.png

python artgen.py animate --first "../../../art-input/enemy_mini_core.png" --action 'idle menace, armored plates slowly rotate around the burning core, core glow pulses, crystal accents glint' --frames 6 --seed 7109 --out-dir out/raw/anim_mini_core
if (-not (Test-Path out/raw/anim_mini_core/frame_00.png)) { throw "mini_core 애니메이션 실패" }
$i = 0
Get-ChildItem out/raw/anim_mini_core/frame_*.png | Sort-Object Name | Select-Object -First 5 | ForEach-Object {
    $n = "{0:D2}" -f $i
    python artgen.py post --src $_.FullName --target 76x56 --colors 48 --no-trim --out "../../../art-input/anim_mini_core_$n.png"
    $i++
}
Write-Host "=== mini_core 완료 ==="
