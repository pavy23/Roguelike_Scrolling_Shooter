# M1 파일럿 생성 스크립트 — ROADMAP M1, ART-DIRECTION.md v2
# 프롬프트·시드를 여기 고정해 재현 가능하게 유지한다 (AGENTS.md 아트 규칙).
# 2026-07-28 실행 결과 반영: 기체·잡졸·폭발 전부 PixelLab 네이티브 생성이 채택안.
#   (gpt-image-2는 투명 배경 미지원, gpt-image-1.5는 지원하나 20배 다운스케일에서 뭉개짐 —
#    대형 보스/배경 키프레임 용도로만 유지)
# 사용: powershell -ExecutionPolicy Bypass -File .\pilot.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $env:OPENAI_API_KEY)   { $env:OPENAI_API_KEY   = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User') }
if (-not $env:PIXELLAB_API_KEY) { $env:PIXELLAB_API_KEY = [Environment]::GetEnvironmentVariable('PIXELLAB_API_KEY', 'User') }

# 1) 플레이어 기체 — PixelLab pixen 네이티브 (pixen은 4의 배수 크기만 허용 → 48x32 생성 후 48x30 정리)
python artgen.py pixen --prompt 'sleek sci-fi space fighter ship, side view facing right, dark blue and steel hull, orange engine glow exhaust, Gradius style, hi-bit pixel art' --width 48 --height 32 --seed 1234 --out out/raw/ship_native.png
python artgen.py post --src out/raw/ship_native.png --target 48x30 --colors 48 --out ../../../art-input/player_ship.png

# (비교 후보 — gpt-image-1.5 원본 및 image-to-pixelart 변환)
# python artgen.py openai --model gpt-image-1.5 --prompt '...' --n 2 --out out/raw/ship.png
# python artgen.py pixelart --src out/raw/ship_0.png --target 48x30 --seed 9001 --out out/raw/ship_px.png

# 2) 잡졸 2종 — PixelLab pixen 네이티브 24x24
python artgen.py pixen --prompt 'biomechanical alien drone enemy, R-Type inspired organic-mechanical fusion, menacing round silhouette, side view facing left, dark chitin shell with glowing cyan core, hi-bit pixel art' --width 24 --height 24 --seed 9001 --out out/raw/zako_bio.png
python artgen.py pixen --prompt 'armored insectoid space fighter enemy, mechanical scarab, side view facing left, dark red carapace with orange visor glow, menacing silhouette, hi-bit pixel art' --width 24 --height 24 --seed 4242 --out out/raw/zako_scarab.png
python artgen.py post --src out/raw/zako_bio.png --target 24x24 --colors 48 --no-trim --out ../../../art-input/enemy_zako.png

# 3) 폭발 — 첫 프레임 48x48 → animate-with-text-v3 8프레임 (비동기 job 자동 폴링)
python artgen.py pixen --prompt 'orange fireball explosion burst, bright yellow-white core, hi-bit pixel art effect sprite, centered' --width 48 --height 48 --seed 777 --out out/raw/boom_first.png
python artgen.py animate --first out/raw/boom_first.png --action 'violent explosion expanding into fireball then dissipating into smoke and sparks' --frames 8 --seed 777 --out-dir out/raw/expl
python artgen.py sheet --src-dir out/raw/expl --colors 48 --out out/final/fx_explosion_m.png

# 4) 인게임 반영: 에디터에서 Tools → Shmup → Rebuild Battle Scene
#    (또는 unity command eval 'UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Shmup/Rebuild Battle Scene");')
