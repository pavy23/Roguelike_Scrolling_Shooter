# St3 거대 전함 함체 (ART-DIRECTION.md 320×160 슬롯, 2026-08-03 사람 지시)
#
# 이 슬롯이 비어 있는 동안 WarshipView는 기존 보스 스프라이트를 조립해 대신 그렸고,
# 화면에서 "일반 보스 세 마리가 겹친 것"으로 읽혔다. 그 임시 조립을 끝내는 아트다.
#
# 도안 제약 (ART-DIRECTION §비어 있는 아트 슬롯 → 채워짐):
#   - 측면도, 뱃머리 왼쪽(진행 방향) / 함미 오른쪽
#   - 상/하 갑판에 포탑 하드포인트 4곳(로컬 ±3.5·±2u)이 별도 스프라이트로 얹히므로
#     그 자리는 평평한 장착면으로 비워 두고, 포탑 자체는 그리지 않는다
#   - 대형 키프레임이라 gpt-image-1.5 (gpt-image-2는 투명 배경 미지원 — ART-DIRECTION)
#
# 2안 생성 후 A안(=_0, 41색) 채택. B안은 out/final/warship_hull_B.png에 남겨 뒀다 —
# 교체하려면 art-input/warship_hull.png를 덮고 씬만 재생성하면 된다.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not $env:OPENAI_API_KEY) { $env:OPENAI_API_KEY = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User') }

$prompt = 'Pixel art sprite of a massive sci-fi space battleship hull, side view facing left, very long horizontal silhouette spanning the full canvas width, hi-bit 16-bit style like SNES-era Gradius V and R-Type with modern shading, 3-4 tone ramps per material, dark gunmetal gray and deep navy armor plating, subtle orange engine exhaust glow at the right rear stern, angular menacing bow on the left, flat empty mounting platforms along the top deck line and bottom keel line where turrets will be attached separately, no turrets drawn, hard pixel edges, no anti-aliasing, transparent background, limited 48 color palette'

python artgen.py openai --model gpt-image-1.5 --size 1536x1024 --n 2 --prompt $prompt --out out/raw/warship_hull.png
if (-not (Test-Path out/raw/warship_hull_0.png)) { throw "warship_hull 생성 실패" }

python artgen.py post --src out/raw/warship_hull_0.png --target 320x160 --colors 48 --out out/final/warship_hull_A.png
python artgen.py post --src out/raw/warship_hull_1.png --target 320x160 --colors 48 --out out/final/warship_hull_B.png

Copy-Item out/final/warship_hull_A.png ../../../art-input/warship_hull.png -Force
Write-Host "=== warship_hull 완료 — 씬 재생성(BattleSceneBuilder.Build)해야 반영된다 ==="
