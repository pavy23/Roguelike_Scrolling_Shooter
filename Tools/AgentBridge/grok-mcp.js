#!/usr/bin/env node
/**
 * grok-mcp — 로컬 Grok CLI를 MCP(stdio) 도구로 노출하는 무의존성 어댑터.
 *
 * 왜 필요한가: Grok CLI(0.2.x)는 MCP 서버 모드가 없고 ACP(stdio)만 있어서
 * Claude Code에 직접 등록할 수 없다. 이 어댑터는 이미 로그인된 로컬 grok
 * 바이너리를 인자 배열로 스폰하므로 (1) 구독 인증이 그대로 유지되고
 * (2) PowerShell 인용부호/이스케이프 문제가 원천 제거된다.
 *
 * 등록: claude mcp add grok --scope user -- node <이 파일 절대경로>
 * 프로토콜: MCP stdio = 한 줄에 JSON 하나 (newline-delimited JSON-RPC 2.0).
 */
'use strict';

const { spawn } = require('child_process');
const os = require('os');
const path = require('path');

const GROK_BIN = process.env.GROK_BIN
  || path.join(os.homedir(), '.grok', 'bin', 'grok.exe');
const DEFAULT_TIMEOUT_SEC = 1800; // grok 태스크는 길다 — 30분 상한, 도구 인자로 조절

const TOOLS = [
  {
    name: 'grok_run',
    description:
      '로컬 Grok CLI(구독 인증 유지)로 프롬프트를 실행한다. 파일 수정 권한이 '
      + '있는 에이전트 실행이므로 cwd를 반드시 의도한 작업 디렉토리로 지정할 것. '
      + 'session_id(임의 UUID)를 주면 같은 id의 후속 resume 호출로 대화를 이어갈 수 있다.',
    inputSchema: {
      type: 'object',
      properties: {
        prompt: { type: 'string', description: '그록에게 줄 지시문 전체' },
        cwd: { type: 'string', description: '작업 디렉토리 (--cwd). 생략 시 grok 기본값' },
        session_id: { type: 'string', description: '새 명명 세션 UUID (-s). resume_id와 배타' },
        resume_id: { type: 'string', description: '이어갈 세션 id/제목 (-r). session_id와 배타' },
        output_format: {
          type: 'string', enum: ['plain', 'json'],
          description: '출력 형식 (기본 plain)',
        },
        timeout_sec: { type: 'number', description: `초 단위 상한 (기본 ${DEFAULT_TIMEOUT_SEC})` },
      },
      required: ['prompt'],
    },
  },
];

function runGrok(args, timeoutSec) {
  return new Promise(resolve => {
    const child = spawn(GROK_BIN, args, {
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let out = '', err = '', done = false;
    const finish = (text, isError) => {
      if (done) return;
      done = true;
      resolve({ text, isError });
    };
    const timer = setTimeout(() => {
      child.kill();
      finish(`[grok-mcp] ${timeoutSec}초 타임아웃으로 중단\n--- 지금까지의 출력 ---\n${out}${err}`, true);
    }, timeoutSec * 1000);
    child.stdout.on('data', d => { out += d; });
    child.stderr.on('data', d => { err += d; });
    child.on('error', e => { clearTimeout(timer); finish(`[grok-mcp] 스폰 실패: ${e.message} (GROK_BIN=${GROK_BIN})`, true); });
    child.on('close', code => {
      clearTimeout(timer);
      const body = out.trim() || err.trim() || '(출력 없음)';
      finish(code === 0 ? body : `[grok-mcp] exit=${code}\n${body}`, code !== 0);
    });
  });
}

async function callTool(name, input) {
  if (name !== 'grok_run')
    return { text: `[grok-mcp] 알 수 없는 도구: ${name}`, isError: true };
  if (!input || typeof input.prompt !== 'string' || !input.prompt.trim())
    return { text: '[grok-mcp] prompt는 비어 있지 않은 문자열이어야 한다', isError: true };

  const args = ['-p', input.prompt, '--always-approve',
    '--output-format', input.output_format === 'json' ? 'json' : 'plain'];
  if (input.cwd) args.push('--cwd', input.cwd);
  if (input.session_id && input.resume_id)
    return { text: '[grok-mcp] session_id와 resume_id는 동시에 줄 수 없다', isError: true };
  if (input.session_id) args.push('-s', input.session_id);
  if (input.resume_id) args.push('-r', input.resume_id);

  const timeoutSec = Number(input.timeout_sec) > 0
    ? Number(input.timeout_sec) : DEFAULT_TIMEOUT_SEC;
  return runGrok(args, timeoutSec);
}

// ---- MCP stdio 루프 (newline-delimited JSON-RPC 2.0) ----

function send(msg) {
  process.stdout.write(JSON.stringify(msg) + '\n');
}

let buffer = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => {
  buffer += chunk;
  let idx;
  while ((idx = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, idx).trim();
    buffer = buffer.slice(idx + 1);
    if (line) handleLine(line);
  }
});
let pending = 0;
let stdinEnded = false;
process.stdin.on('end', () => {
  stdinEnded = true;
  if (pending === 0) process.exit(0); // 진행 중인 grok 호출은 끝까지 기다린다
});

async function handleLine(line) {
  let msg;
  try { msg = JSON.parse(line); }
  catch { return; } // 프레이밍 깨진 줄은 무시
  const { id, method, params } = msg;
  if (id === undefined || id === null) return; // 알림(notifications/*)은 응답 금지

  try {
    if (method === 'initialize') {
      send({ jsonrpc: '2.0', id, result: {
        protocolVersion: (params && params.protocolVersion) || '2025-06-18',
        capabilities: { tools: {} },
        serverInfo: { name: 'grok-mcp', version: '1.0.0' },
      } });
    } else if (method === 'ping') {
      send({ jsonrpc: '2.0', id, result: {} });
    } else if (method === 'tools/list') {
      send({ jsonrpc: '2.0', id, result: { tools: TOOLS } });
    } else if (method === 'tools/call') {
      pending++;
      try {
        const { text, isError } = await callTool(params.name, params.arguments);
        send({ jsonrpc: '2.0', id, result: {
          content: [{ type: 'text', text }], isError: !!isError,
        } });
      } finally {
        pending--;
        if (stdinEnded && pending === 0) process.exit(0);
      }
    } else {
      send({ jsonrpc: '2.0', id, error: { code: -32601, message: `method not found: ${method}` } });
    }
  } catch (e) {
    send({ jsonrpc: '2.0', id, error: { code: -32603, message: String(e && e.message || e) } });
  }
}
