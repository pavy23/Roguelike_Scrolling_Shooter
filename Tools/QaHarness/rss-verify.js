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
  const a = { stage: null, warp: null, seed: null, seconds: 30, out: './out/run', god: true };
  for (let i = 2; i < argv.length; i += 2) {
    const k = argv[i].replace(/^--/, '');
    const v = argv[i + 1];
    if (k === 'seconds') a.seconds = parseInt(v, 10);
    else if (k === 'god') a.god = v !== '0';
    else a[k] = v;
  }
  return a;
}

// ── 픽셀 판독 ────────────────────────────────────────────────────────────────
// 게임 캔버스는 레터박스 안에 있고 배율이 고정이라, 색 기반 측정이 해상도 변화에
// 강하다. 좌표를 박아 두면 캔버스 크기가 바뀔 때 조용히 틀리므로 색으로 찾는다.

/** 보스 HP바(상단 주황/적색 가로 막대)의 픽셀 폭. 없으면 0. */
function measureBossHpBar(png) {
  const { width, height, data } = png;
  let best = 0;
  // 상단 1/3만 훑는다 — 하단 게이지 HUD의 주황 글자에 걸리지 않게.
  for (let y = 0; y < Math.floor(height / 3); y++) {
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
    if (longest > best) best = longest;
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

  // 관찰 — 1초 간격으로 HP바 폭을 수치로 기록
  const samples = [];
  for (let t = 0; t < args.seconds; t++) {
    await sleep(1000);
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
