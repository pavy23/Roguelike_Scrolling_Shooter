// rss-verify — WebGL 빌드 headless 검증 하네스 (REQ-124 후속, 2026-08-03)
//
// 왜 있나: build25~32 내내 검증 스크립트를 매 세션 새로 썼고, 세션이 끊기면
// 사라졌다. 판독도 사람(모델)이 PNG를 한 장씩 눈으로 보는 방식이라 느리고 비쌌다.
// 이 파일은 (1) 재사용 가능한 구동부와 (2) **픽셀 수치 어서션**을 함께 둔다 —
// "HP바가 30초간 단조 감소" 같은 판정이 스크린샷 판독 없이 PASS/FAIL로 떨어진다.
//
// 준비:  npm i puppeteer-core pngjs      (이 폴더에서)
// 서버:  cd Builds/Web && python -m http.server 8099 --bind 127.0.0.1
// 실행:  node rss-verify.js --stage 3 --warp boss --seconds 45 --out ./out/st3
//
// 공유 PC 제약(AGENTS.md §9): 항상 headless — 창을 띄우지 않는다.
const puppeteer = require('puppeteer-core');
const { PNG } = require('pngjs');
const fs = require('fs');
const path = require('path');

const CHROME = process.env.RSS_CHROME
  || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const BASE = process.env.RSS_URL || 'http://127.0.0.1:8099/index.html';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function parseArgs(argv) {
  const a = { stage: null, warp: null, seed: null, seconds: 30, out: './out/run', god: true, uncharted: false };
  for (let i = 2; i < argv.length; i += 2) {
    const k = argv[i].replace(/^--/, '');
    const v = argv[i + 1];
    if (k === 'seconds') a.seconds = parseInt(v, 10);
    else if (k === 'god') a.god = v !== '0';
    else if (k === 'uncharted') a.uncharted = v !== '0';
    else a[k] = v;
  }
  return a;
}

// ── 픽셀 판독 ────────────────────────────────────────────────────────────────
// 게임 캔버스는 레터박스 안에 있고 배율이 고정이라, 색 기반 측정이 해상도 변화에
// 강하다. 좌표를 박아 두면 캔버스 크기가 바뀔 때 조용히 틀리므로 색으로 찾는다.

/** 보스 HP바(상단 주황/적색 가로 막대)의 픽셀 폭. 없으면 0. */
// 보스 HP 바는 2026-08-04에 화면 위(y 9.7u)에서 아래(y -10.4u)로 옮겼다
// (PAUSE 버튼과 자리를 다퉜다). 이 함수는 화면 전체를 훑으므로 위치가 바뀌어도
// 찾지만, 어디를 보는지 모르면 "바를 못 찾음 = FAIL"을 회귀로 오해하게 된다.
function measureBossHpBar(png) {
  const { width, height, data } = png;
  let best = 0;
  // 화면 전체를 훑는다. 예전에는 상단 1/3만 봤는데(하단 게이지 HUD의 주황 글자를
  // 피하려고), 바가 아래로 옮겨간 순간 "바 없음 = FAIL"이 되어 멀쩡한 빌드가
  // 회귀로 보였다. 대신 **아주 긴 가로 연속**만 바로 인정한다 — 글자는 그렇게
  // 길게 이어지지 않는다.
  for (let y = 0; y < height; y++) {
    let run = 0;
    let longest = 0;
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) << 2;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      // 주황~적색 계열: R이 높고 B가 낮고, G는 R보다 확실히 낮다
      const bar = r > 150 && b < 110 && g < r - 40;
      run = bar ? run + 1 : 0;
      if (run > longest) longest = run;
    }
    // 게이지 라벨 글자는 길어야 수십 px다. 바는 16월드유닛(≈380px)이라
    // 하한을 두면 둘이 섞이지 않는다.
    if (longest >= 120 && longest > best) best = longest;
  }
  return best;
}

/** 특정 색과 가까운 픽셀 수 — 무적 테두리·경고색 같은 단발 신호 확인용. */
function countNear(png, [r, g, b], tol = 40) {
  const { data } = png;
  let n = 0;
  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] < 128) continue;
    if (Math.abs(data[i] - r) <= tol
      && Math.abs(data[i + 1] - g) <= tol
      && Math.abs(data[i + 2] - b) <= tol) n++;
  }
  return n;
}

async function shotPng(page) {
  return PNG.sync.read(await page.screenshot({ encoding: 'binary' }));
}

// ── 구동 ─────────────────────────────────────────────────────────────────────
async function main() {
  const args = parseArgs(process.argv);
  const outDir = path.resolve(args.out);
  fs.mkdirSync(outDir, { recursive: true });

  const browser = await puppeteer.launch({
    executablePath: CHROME,
    headless: true,
    args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
           '--window-size=1300,760', '--mute-audio'],
    defaultViewport: { width: 1300, height: 760 },
  });
  const page = await browser.newPage();
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push('[error] ' + m.text().slice(0, 300)); });
  page.on('pageerror', (e) => errors.push('[pageerror] ' + String(e).slice(0, 300)));

  const q = ['dev=1'];
  if (args.god) q.push('god=1');
  if (args.stage) q.push(`stage=${args.stage}`);
  // 미지의 구역 직행 (REQ-123). warp=boss와 같이 쓰면 거대 보스 앞에서 시작한다.
  if (args.uncharted) q.push('uncharted=1');
  // 테마·거대보스는 시드가 정한다. 고정하지 않으면 --stage 3이 전함을 보장하지
  // 않는다 — 실제로 nebula 보스를 찍어 놓고 FAIL을 뱉은 적이 있다.
  if (args.theme) q.push(`theme=${args.theme}`);
  if (args.colossal) q.push(`colossal=${args.colossal}`);
  if (args.power) q.push(`power=${args.power}`);
  if (args.warp) q.push(`warp=${args.warp}`);
  const url = `${BASE}?${q.join('&')}`;
  console.log('url:', url);
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });

  // Unity 로딩 — 캔버스가 실제 픽셀을 그릴 때까지
  for (let i = 0; i < 90; i++) {
    await sleep(2000);
    const buf = await page.screenshot({ encoding: 'binary' });
    if (buf.length > 30000 && i > 4) break;
  }
  await page.mouse.click(650, 300);
  await sleep(400);

  // 시드 고정은 타이틀 입력으로만 된다 (URL ?seed는 Core가 안 읽는다 — DevArgs 참고).
  // 키는 프레임당 1회만 인식되므로 간격이 필수다.
  if (args.seed) {
    for (let i = 0; i < 14; i++) { await page.keyboard.press('Backspace'); await sleep(80); }
    for (const ch of String(args.seed)) { await page.keyboard.press(`Digit${ch}`); await sleep(90); }
  }
  await page.keyboard.press('Space');
  await sleep(3500);   // 워프는 Awake에서 끝나므로 이 시점이면 이미 목표 구간이다
  await page.screenshot({ path: path.join(outDir, '000_arrived.png') });

  // 관찰 — 1초 간격으로 HP바 폭을 수치로 기록.
  //
  // 기체를 **위아래로 훑는다.** 예전에는 y=0에 고정해 두었는데, 그 높이에 판정이
  // 없는 보스는 데미지가 0으로 나와 멀쩡한 빌드가 FAIL로 보였다. 실제로 전함
  // 갑판 포탑(y>0)과 하이브 상단 파츠를 이 한계 때문에 확인할 수 없었고,
  // "무데미지" 오판이 다섯 번 났다.
  //
  // 위/아래를 번갈아 길게 눌러 화면 높이를 왕복시킨다 — 어느 높이에 판정이
  // 있든 몇 초 안에 한 번은 지나간다.
  const samples = [];
  let sweepDown = true;
  for (let t = 0; t < args.seconds; t++) {
    const key = sweepDown ? 'ArrowDown' : 'ArrowUp';
    await page.keyboard.down(key);
    await sleep(1000);
    await page.keyboard.up(key);
    // 4초마다 방향을 바꾼다. 화면 높이(22.5유닛)를 한 번 지나기에 넉넉하다.
    if (t % 4 === 3) sweepDown = !sweepDown;
    const png = await shotPng(page);
    samples.push({ t, hp: measureBossHpBar(png) });
    if (t % 10 === 9) {
      await page.screenshot({ path: path.join(outDir, `${String(t + 1).padStart(3, '0')}_t.png`) });
    }
  }

  // 어서션: 보스 HP가 실제로 깎였는가 (데미지 경로 살아 있음)
  const first = samples.find((s) => s.hp > 50);
  const last = [...samples].reverse().find((s) => s.hp > 0);
  const damaged = first && last && last.hp < first.hp;
  const report = {
    url,
    consoleErrors: errors.length,
    hpFirst: first ? first.hp : null,
    hpLast: last ? last.hp : null,
    hpDropped: !!damaged,
    samples,
    errors,
  };
  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 2));

  const pass = errors.length === 0 && damaged;
  console.log(`console errors: ${errors.length}`);
  console.log(`boss hp bar: ${report.hpFirst} → ${report.hpLast} (dropped=${report.hpDropped})`);
  console.log(pass ? 'PASS' : 'FAIL');
  await browser.close();
  process.exit(pass ? 0 : 1);
}

main().catch((e) => { console.error('FAIL', e); process.exit(1); });
