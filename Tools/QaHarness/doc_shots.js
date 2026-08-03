// README용 스크린샷 일괄 캡처 (headless).
//
// 왜 스크립트인가: docs/screenshots/*.png는 손으로 찍어 붙여 온 것이라, 게임이
// 바뀌어도 아무도 다시 찍지 않았다. 실제로 README의 전함 사진은 함체를 34x17로
// 키우기 전 것이었고, 타이틀 사진에는 없어진 시드 버튼이 남아 있었다.
// 장면 목록을 코드로 두면 "빌드 → 캡처 → 합성"이 한 번에 돌아간다.
//
// 준비:  npm i puppeteer-core   (이 폴더에서)
// 서버:  cd Builds/Web && python -m http.server 8099 --bind 127.0.0.1
// 실행:  node doc_shots.js            (전부)
//        node doc_shots.js title s1_early   (일부만)
//
// 공유 PC 제약(AGENTS.md §9): 항상 headless — 창을 띄우지 않는다.
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');

const CHROME = process.env.RSS_CHROME
  || String.raw`C:\Program Files\Google\Chrome\Application\chrome.exe`;
const BASE = process.env.RSS_URL || 'http://127.0.0.1:8099/index.html';
const OUT = path.join(__dirname, 'out', 'docs');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// name: 파일명 / query: dev 인자 / launch: 스페이스로 출격할지 / at: 캡처 시각(초)
const SCENES = [
  // 시드를 박아 둔다 — 테마는 시드가 정하므로, 안 박으면 문서 사진이 매번 다른
  // 스테이지가 된다(실제로 "3스테이지 전함"을 찍으려다 하이브가 나왔다).
  { name: 'title', query: 'dev=1', launch: false, at: [2] },
  { name: 'battle_early', query: 'dev=1&god=1&stage=1', seed: 3713368528,
    launch: true, at: [22] },
  { name: 'boss_scrapyard', query: 'dev=1&god=1&stage=1&warp=boss', seed: 2590789099,
    launch: true, at: [14] },
  { name: 'boss_hive', query: 'dev=1&god=1&stage=2&warp=boss', seed: 649309097,
    launch: true, at: [16] },
  { name: 'boss_nebula', query: 'dev=1&god=1&stage=2&warp=boss', seed: 2402718557,
    launch: true, at: [16] },
  { name: 'boss_core', query: 'dev=1&god=1&stage=5&warp=boss', seed: 1630372657,
    launch: true, at: [16] },
  // 전함은 막마다 자리와 패턴이 바뀐다 — 한 런에서 세 시점을 찍는다.
  { name: 'warship', query: 'dev=1&god=1&stage=3&warp=boss', seed: 2264451436,
    launch: true, at: [10, 70, 150] },
  { name: 'leviathan', query: 'dev=1&god=1&uncharted=1&warp=boss', seed: 475252250,
    launch: true, at: [20] },
];

async function capture(browser, scene) {
  const page = await browser.newPage();
  await page.setViewport({ width: 1300, height: 760 });
  await page.goto(`${BASE}?${scene.query}`, {
    waitUntil: 'domcontentloaded', timeout: 60000,
  });

  // Unity 로딩 — 캔버스가 실제 픽셀을 그릴 때까지 (파일 크기로 판별)
  for (let i = 0; i < 60; i++) {
    await sleep(2000);
    const buf = await page.screenshot({ encoding: 'binary' });
    if (buf.length > 30000 && i > 4) break;
  }
  await page.mouse.click(650, 300);
  await sleep(600);
  // 시드 고정 — 테마는 시드가 정하므로, 문서용 장면은 시드를 박아야 재현된다.
  // (URL ?seed는 Core가 안 읽는다. 타이틀의 숫자 입력이 유일한 경로다.)
  if (scene.seed) {
    for (let i = 0; i < 14; i++) { await page.keyboard.press('Backspace'); await sleep(70); }
    for (const ch of String(scene.seed)) { await page.keyboard.press(`Digit${ch}`); await sleep(80); }
  }
  if (scene.launch) {
    await page.keyboard.press('Space');
    await sleep(3000);
    // F3 = 진단 오버레이 끄기. dev 인자로 들어왔으니 좌표·시드가 화면에 찍히는데,
    // 그대로 문서에 실으면 개발 흔적으로 읽힌다. 워프·무적은 그대로 쓴다.
    await page.keyboard.press('F3');
    await sleep(400);
  }

  let elapsed = 0;
  for (const at of scene.at) {
    const wait = Math.max(0, at - elapsed);
    if (wait > 0) await sleep(wait * 1000);
    elapsed = at;
    const suffix = scene.at.length > 1 ? `_${at}s` : '';
    const file = path.join(OUT, `${scene.name}${suffix}.png`);
    await page.screenshot({ path: file });
    console.log(`  ${path.basename(file)}`);
  }
  await page.close();
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const only = process.argv.slice(2);
  const scenes = only.length
    ? SCENES.filter((s) => only.includes(s.name))
    : SCENES;

  const browser = await puppeteer.launch({
    executablePath: CHROME,
    headless: true,
    args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
           '--window-size=1300,760', '--mute-audio'],
    defaultViewport: { width: 1300, height: 760 },
  });
  for (const scene of scenes) {
    console.log(scene.name);
    try {
      await capture(browser, scene);
    } catch (error) {
      console.log(`  실패: ${String(error).slice(0, 160)}`);
    }
  }
  await browser.close();
  console.log(`완료 → ${OUT}`);
})();
