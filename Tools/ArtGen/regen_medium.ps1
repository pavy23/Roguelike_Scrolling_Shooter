# 중형·중간보스 스프라이트 재생성 — 히트박스 네이티브 크기 (REQ-050 후속)
#
# 기존 아트는 24x24~64x48로 생성돼 히트박스(40x32~80x60)까지 최대 1.8배 확대됐고,
# 확대된 픽셀이 거칠어 보였다 ("중간보스급 스프라이트 픽셀이 거칠다").
# ApplyEnemyScale이 스프라이트를 히트박스 폭에 맞추므로, 처음부터 그 크기로
# 생성하면 스케일이 1.0이 되어 픽셀 밀도가 균일해진다.
#
# pixen은 4의 배수 크기만 허용 → elite만 44x36 생성 후 42x34로 정리.
# 시드 고정 (AGENTS.md 아트 규칙: 재현 가능).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $env:OPENAI_API_KEY)   { $env:OPENAI_API_KEY   = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User') }
if (-not $env:PIXELLAB_API_KEY) { $env:PIXELLAB_API_KEY = [Environment]::GetEnvironmentVariable('PIXELLAB_API_KEY', 'User') }

# ── 중간보스 4종 (테마 성격 = REQ-061 행동 패턴과 일치시킨다) ──

# scrapyard: 돌진형 — 고철 조립 구축함, 앞으로 쏠린 실루엣
python artgen.py pixen --prompt 'salvaged scrap destroyer gunship mid-boss, patchwork welded armor plates, forward-lunging aggressive silhouette, side view facing left, rust orange and gunmetal grey, glowing engine vents, hi-bit pixel art' --width 76 --height 56 --seed 6101 --out out/raw/mini_destroyer_L.png
python artgen.py post --src out/raw/mini_destroyer_L.png --target 76x56 --colors 48 --no-trim --out ../../../art-input/enemy_mini_destroyer.png

# hive: 산탄 살포형 — 육질 공포체, 열리는 아가리
python artgen.py pixen --prompt 'fleshy biomechanical horror mid-boss, pulsating organic mass with opening maw and tentacle stubs, single glowing eye, side view facing left, purple magenta chitin with pink membrane, hi-bit pixel art' --width 72 --height 56 --seed 6102 --out out/raw/mini_horror_L.png
python artgen.py post --src out/raw/mini_horror_L.png --target 72x56 --colors 48 --no-trim --out ../../../art-input/enemy_mini_horror.png

# fortress: 정지 집중포형 — 중장갑 보행 포대
python artgen.py pixen --prompt 'heavy armored assault walker mech mid-boss, fortress artillery platform with thick leg struts and twin cannon barrels, side view facing left, dark steel with orange hazard stripes, hi-bit pixel art' --width 80 --height 60 --seed 6103 --out out/raw/mini_walker_L.png
python artgen.py post --src out/raw/mini_walker_L.png --target 80x60 --colors 48 --no-trim --out ../../../art-input/enemy_mini_walker.png

# nebula: 위치 전환형 — 부유 결정체
python artgen.py pixen --prompt 'floating crystalline entity mid-boss, sharp prism shards orbiting a bright glowing core, ethereal, translucent cyan and violet crystal facets, side view, hi-bit pixel art' --width 76 --height 56 --seed 6104 --out out/raw/mini_crystal_L.png
python artgen.py post --src out/raw/mini_crystal_L.png --target 76x56 --colors 48 --no-trim --out ../../../art-input/enemy_mini_crystal.png

# ── 중형 4종 ──

python artgen.py pixen --prompt 'squat armored tank drone enemy, heavy frontal plating and rivets, treads underneath, side view facing left, olive drab and gunmetal, small red sensor eye, hi-bit pixel art' --width 40 --height 32 --seed 6105 --out out/raw/tank_L.png
python artgen.py post --src out/raw/tank_L.png --target 40x32 --colors 48 --no-trim --out ../../../art-input/enemy_tank.png

python artgen.py pixen --prompt 'elite interceptor space fighter enemy, sleek aggressive swept-wing silhouette, side view facing left, crimson and dark grey hull with glowing yellow canopy, hi-bit pixel art' --width 44 --height 36 --seed 6106 --out out/raw/elite_L.png
python artgen.py post --src out/raw/elite_L.png --target 42x34 --colors 48 --out ../../../art-input/enemy_elite.png

python artgen.py pixen --prompt 'armored guardian sphere enemy, concentric rotating ring shells around a glowing energy core, ancient sentinel, side view, bronze and teal metal, hi-bit pixel art' --width 44 --height 40 --seed 6107 --out out/raw/guardian_L.png
python artgen.py post --src out/raw/guardian_L.png --target 44x40 --colors 48 --no-trim --out ../../../art-input/enemy_guardian.png

python artgen.py pixen --prompt 'faceted crystal prism enemy, sharp angular gemstone with glowing refraction edges, floating menacing shard cluster, violet and ice blue, side view, hi-bit pixel art' --width 44 --height 40 --seed 6108 --out out/raw/shard_L.png
python artgen.py post --src out/raw/shard_L.png --target 44x40 --colors 48 --no-trim --out ../../../art-input/enemy_shard_prism.png

Write-Host "=== 8종 재생성 완료 ==="
