// 타이틀 화면만 캡처한다 (스페이스를 누르지 않는다).
const puppeteer = require('puppeteer-core');
const CHROME = process.env.RSS_CHROME || String.raw`C:\Program Files\Google\Chrome\Application\chrome.exe`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: true,
    args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
           '--window-size=1300,760', '--mute-audio'],
    defaultViewport: { width: 1300, height: 760 },
  });
  const page = await browser.newPage();
  await page.goto('http://127.0.0.1:8099/index.html?dev=1', { waitUntil: 'domcontentloaded', timeout: 60000 });
  for (let i = 0; i < 60; i++) {
    await sleep(2000);
    const buf = await page.screenshot({ encoding: 'binary' });
    if (buf.length > 30000 && i > 4) break;
  }
  await page.mouse.click(650, 300);
  await sleep(1500);
  await page.screenshot({ path: 'title.png' });
  // MODE 버튼을 눌러 데일리로 전환한 화면도 남긴다
  await page.mouse.click(245, 265);
  await sleep(900);
  await page.screenshot({ path: 'title_daily.png' });
  await browser.close();
  console.log('saved title.png');
})();
