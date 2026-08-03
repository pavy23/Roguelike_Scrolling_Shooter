# 전함 파츠 전용 아트 (사람 지적 2026-08-03: "전함·코어·로봇 세 보스가 하나로 보인다")
#
# 원인: 함미·함수 하드포인트가 boss_fortress / boss_core를 빌려 쓰고 있었다. 둘 다
# 다른 보스의 조형이라 배의 일부로 안 읽혔다. 게다가 1차 함체 아트(gen_warship_hull.ps1)는
# 세로 42px(±1.3유닛)짜리 얇은 바늘이라, 갑판 포탑(±2.0·±3.5유닛)이 전부 허공에 떴다.
#
# 이 스크립트가 고치는 것:
#   1) 함체 재생성 — 캔버스 세로를 꽉 채우는 두꺼운 실루엣(결과 ±3.09유닛)
#   2) 함미 = 엔진 블록 80x64px (판정 5x4유닛)
#   3) 함수 = 함교 코어 모듈 64x64px (판정 4x4유닛)
# 파츠는 ART-DIRECTION 파이프라인대로 소형은 PixelLab pixen 네이티브 생성이다
# (대형 원본 다운스케일은 뭉개진다 - 파일럿 확인).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not $env:OPENAI_API_KEY) { $env:OPENAI_API_KEY = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User') }
if (-not $env:PIXELLAB_API_KEY) { $env:PIXELLAB_API_KEY = [Environment]::GetEnvironmentVariable('PIXELLAB_API_KEY', 'User') }

$hull = 'Pixel art side view of a colossal sci-fi space battleship hull, hi-bit 16-bit SNES style like Gradius V and R-Type with modern shading, 3-4 tone ramps per material, dark gunmetal and deep navy armor plating with orange running lights. TALL BULKY PROFILE: the ship body fills the entire canvas height from top to bottom, thick armored superstructure on the upper deck and a deep keel below, not a thin sliver. Flat empty rectangular mounting pads along the top deck edge and the bottom keel edge for turrets to be attached later, no turrets drawn. Bow at the left, engine section at the right. Hard pixel edges, no anti-aliasing, transparent background, limited 48 color palette'

python artgen.py openai --model gpt-image-1.5 --size 1536x1024 --n 2 --prompt $hull --out out/raw/warship_hull2.png
if (-not (Test-Path out/raw/warship_hull2_0.png)) { throw "함체 재생성 실패" }
python artgen.py post --src out/raw/warship_hull2_0.png --target 320x160 --colors 48 --out out/final/warship_hull2_A.png
python artgen.py post --src out/raw/warship_hull2_1.png --target 320x160 --colors 48 --out out/final/warship_hull2_B.png
Copy-Item out/final/warship_hull2_A.png ../../../art-input/warship_hull.png -Force   # A안 채택(44색, ±3.09유닛)

python artgen.py pixen --width 80 --height 64 --seed 3103 --out out/raw/warship_stern.png --prompt 'battleship stern engine block, side view, massive armored thruster housing with three exhaust nozzles glowing orange on the right side, dark gunmetal and deep navy armor plating, part of a larger warship hull not a standalone creature, hi-bit 16-bit pixel art, hard pixel edges'
python artgen.py post --src out/raw/warship_stern.png --target 80x64 --colors 48 --no-trim --out out/final/warship_stern.png
Copy-Item out/final/warship_stern.png ../../../art-input/warship_stern.png -Force

python artgen.py pixen --width 64 --height 64 --seed 3104 --out out/raw/warship_core.png --prompt 'battleship bow command core module, side view, armored bridge tower with a single large glowing cyan reactor eye at center, dark gunmetal and deep navy armor plating with orange trim, part of a larger warship hull not a standalone creature, hi-bit 16-bit pixel art, hard pixel edges'
python artgen.py post --src out/raw/warship_core.png --target 64x64 --colors 48 --no-trim --out out/final/warship_core.png
Copy-Item out/final/warship_core.png ../../../art-input/warship_core.png -Force

Write-Host "=== 전함 파츠 완료 — 씬 재생성(BattleSceneBuilder.Build) 후 반영된다 ==="

# ── St1/St3 파괴 가능 장애물 (사람 지적 2026-08-03: "scrab 디자인이 허접하다") ──
# 기존에는 BattleSceneBuilder.BuildScrapDebrisPixels()가 만드는 절차 생성 덩어리였다.
# 사람 요구: "그럴싸한 판때기". 모서리 볼트 4개 + 용접선 + 녹이 있는 장갑 패널로 뽑는다.
python artgen.py pixen --width 32 --height 32 --seed 4207 --out out/raw/scrap_plate2.png --prompt 'square armored bulkhead panel, flat rectangular steel plate filling the whole square frame, four corner bolts, riveted seams and a diagonal weld line, weathered gunmetal gray with rust streaks, straight edges and sharp square corners, industrial salvage, no glow, hi-bit 16-bit pixel art, hard pixel edges'
python artgen.py post --src out/raw/scrap_plate2.png --target 32x32 --colors 24 --no-trim --out out/final/scrap_plate2.png
Copy-Item out/final/scrap_plate2.png ../../../art-input/obstacle_scrap_debris.png -Force
