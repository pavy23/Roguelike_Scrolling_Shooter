/**
 * RSS 글로벌 스코어보드 P1 (Cloudflare Workers + KV)
 *
 * 보드: daily:{yyyymmdd} (데일리 런 — 공통 시드 경쟁), all (프리 런 전체)
 * 항목: { n:이름, s:점수, sd:시드, sh:기체, d:난이도, g:등급, h:상태해시, t:제출시각, tk:토큰지문 }
 * 익명 디바이스 토큰당 보드별 최고 기록 1개 유지. 상위 100 저장.
 *
 * P1 규모(개인 게임)에서는 KV read-modify-write 경합을 허용한다 — 드물게 지는
 * 갱신은 다음 제출에서 복원된다. 트래픽이 늘면 Durable Object로 교체.
 * P2 예정: replay 본문 업로드(R2) + GitHub Action 재실행 검증 → verified 마크.
 */

const ALLOWED_ORIGINS = [
  'https://pavy23.github.io',
  'http://localhost:8099',
];
const MAX_ENTRIES = 100;
const NAME_RE = /^[0-9A-Za-z가-힣ㄱ-ㅎㅏ-ㅣ _\-\.]{2,10}$/;
const RATE_LIMIT_PER_10MIN = 12;

function cors(origin) {
  const allow = ALLOWED_ORIGINS.includes(origin) ? origin : ALLOWED_ORIGINS[0];
  return {
    'Access-Control-Allow-Origin': allow,
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Content-Type': 'application/json; charset=utf-8',
  };
}

const json = (obj, status, headers) =>
  new Response(JSON.stringify(obj), { status, headers });

async function sha256hex(text) {
  const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(buf)].map(b => b.toString(16).padStart(2, '0')).join('');
}

function utcDateKey(now) {
  const d = new Date(now);
  return `${d.getUTCFullYear()}${String(d.getUTCMonth() + 1).padStart(2, '0')}${String(d.getUTCDate()).padStart(2, '0')}`;
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const headers = cors(request.headers.get('Origin') || '');

    if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers });

    // GET /v1/board/daily/{yyyymmdd} | /v1/board/daily (오늘) | /v1/board/all
    if (request.method === 'GET' && url.pathname.startsWith('/v1/board/')) {
      const part = url.pathname.slice('/v1/board/'.length);
      let key;
      if (part === 'all') key = 'all';
      else if (part === 'daily') key = `daily:${utcDateKey(Date.now())}`;
      else if (/^daily\/\d{8}$/.test(part)) key = `daily:${part.slice(6)}`;
      else return json({ error: 'unknown board' }, 404, headers);
      const list = (await env.SCORES.get(key, 'json')) || [];
      return json({ board: key, entries: list }, 200, headers);
    }

    // POST /v1/scores
    if (request.method === 'POST' && url.pathname === '/v1/scores') {
      let body;
      try { body = await request.json(); } catch { return json({ error: 'bad json' }, 400, headers); }

      const name = String(body.name || '').trim();
      const token = String(body.token || '');
      const score = Number(body.score);
      const seed = String(body.seed || '');
      const ship = String(body.ship || '').slice(0, 16);
      const difficulty = String(body.difficulty || '').slice(0, 8);
      const grade = String(body.grade || '').slice(0, 16);
      const hash = String(body.replayHash || '').slice(0, 32);
      const daily = body.daily === true;

      if (!NAME_RE.test(name)) return json({ error: 'invalid name (2~10자)' }, 400, headers);
      if (!Number.isFinite(score) || score < 0 || score > 9_999_999_999)
        return json({ error: 'invalid score' }, 400, headers);
      if (token.length < 8 || token.length > 64) return json({ error: 'invalid token' }, 400, headers);
      if (!seed) return json({ error: 'seed required' }, 400, headers);

      // 레이트리밋: IP+토큰 지문, 10분 창
      const ip = request.headers.get('CF-Connecting-IP') || '0';
      const tkHash = (await sha256hex(token)).slice(0, 8);
      const rlKey = `rl:${await sha256hex(ip + token)}`;
      const count = Number((await env.SCORES.get(rlKey)) || 0);
      if (count >= RATE_LIMIT_PER_10MIN) return json({ error: 'rate limited' }, 429, headers);
      await env.SCORES.put(rlKey, String(count + 1), { expirationTtl: 600 });

      const boardKey = daily ? `daily:${utcDateKey(Date.now())}` : 'all';
      const list = (await env.SCORES.get(boardKey, 'json')) || [];

      const entry = {
        n: name, s: Math.floor(score), sd: seed, sh: ship, d: difficulty,
        g: grade, h: hash, t: Date.now(), tk: tkHash,
      };

      // 같은 토큰의 기존 기록은 최고 점수 하나만 남긴다
      const existing = list.findIndex(e => e.tk === tkHash);
      if (existing >= 0) {
        if (list[existing].s >= entry.s)
          return json({ ok: true, kept: 'existing', rank: existing + 1 }, 200, headers);
        list.splice(existing, 1);
      }
      list.push(entry);
      list.sort((a, b) => b.s - a.s);
      if (list.length > MAX_ENTRIES) list.length = MAX_ENTRIES;
      await env.SCORES.put(boardKey, JSON.stringify(list));

      const rank = list.findIndex(e => e.tk === tkHash && e.s === entry.s) + 1;
      return json({ ok: true, rank: rank || null, board: boardKey }, 200, headers);
    }

    return json({ error: 'not found' }, 404, headers);
  },
};
