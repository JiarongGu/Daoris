/**
 * CHR1 (2026-09-28): what an embedded Chromium does under a Daoris window, measured before the host
 * is built (`docs/2026-09-28-chromium-host-design.md` §5). The host is `tools/chromium-probe/`, a
 * WinForms window embedding Chromium through CefSharp, in the two roles a Daoris window gives it: the
 * APP's page (the one that would hold the bridge) and the in-app browser, each in its own request
 * context.
 *
 *   1. 🔴 The debug port's reach: with a port on, can a CDP client reach the app's page and its bridge?
 *      And with the page and the browser in two processes, can it still?
 *   2. A tab an agent opens (`browser_tabs new`, `Target.createTarget`): is the app told, and does the
 *      person see it? In Alloy style and in Chrome style. A page's own `window.open` is the control.
 *   3. A sign-in (a cookie with no expiry) across a restart, with the engine's own setting and without.
 *   4. Playwright MCP attached and driving.
 *   5. A page-to-host round trip over the engine's own message channel, and the codecs the build has.
 *   6. What it adds to an install.
 *
 *   node tools/chromium-probe.mjs
 *
 * Everything it starts is its own: the host exe under `_fixtures/chromium-probe/bin`, on caches under
 * `_fixtures/chromium-probe`. It closes its windows through UI Automation, by its own pids, as a person
 * closes a window, and kills nothing by name. A stand-in site on loopback plays the app and the identity
 * server.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { randomUUID } from 'node:crypto';
import { gzipSync } from 'node:zlib';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { Cdp, freePort, targetsAt } from './cdp.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'chromium-probe');
const bin = join(scratch, 'bin');
const exe = join(bin, 'chromium-probe.exe');

// 0. The host, built into scratch.
const build = spawnSync('dotnet', ['build', join(repoRoot, 'tools', 'chromium-probe'), '-c', 'Release', '-o', bin, '-v', 'q', '-nologo'],
  { encoding: 'utf8', shell: true });
if (build.status !== 0) {
  console.error(build.stdout, build.stderr);
  process.exit(2);
}

// The stand-in site: /app is the app's page, /login sets a cookie with no expiry, / says who is signed in.
const reports = [];
const APP = `<title>app</title><p id="state">the app's page</p><script>
const n = Number(new URLSearchParams(location.search).get('bench') || 0);
const pending = new Map();
window.__reply = (v) => { const done = pending.get(v); if (done) { pending.delete(v); done(); } };
function codecs() {
  const video = document.createElement('video');
  const types = { h264: 'video/mp4; codecs="avc1.42E01E"', aac: 'audio/mp4; codecs="mp4a.40.2"',
    hevc: 'video/mp4; codecs="hvc1.1.6.L93.B0"', vp9: 'video/webm; codecs="vp9"',
    av1: 'video/mp4; codecs="av01.0.05M.08"', opus: 'audio/webm; codecs="opus"' };
  return Object.fromEntries(Object.entries(types).map(([name, type]) =>
    [name, (video.canPlayType(type) || 'no') + (typeof MediaSource !== 'undefined' && MediaSource.isTypeSupported(type) ? ', mse' : '')]));
}
(async () => {
  const bridge = typeof CefSharp === 'object' && typeof CefSharp.PostMessage === 'function';
  const report = { bridge, ua: navigator.userAgent, codecs: codecs() };
  if (bridge && n > 0) {
    const times = [];
    for (let i = 1; i <= n; i++) {
      const t0 = performance.now();
      await new Promise((done) => { pending.set(i, done); CefSharp.PostMessage(i); });
      times.push(performance.now() - t0);
    }
    times.sort((a, b) => a - b);
    const ms = (v) => Math.round(v * 100) / 100;
    report.roundTrip = { n, median: ms(times[n >> 1]), p95: ms(times[Math.floor(n * 0.95)]), max: ms(times[n - 1]) };
  }
  await fetch('/report', { method: 'POST', body: JSON.stringify(report) });
})();
</script>`;
const site = createServer((req, res) => {
  if (req.url === '/report' && req.method === 'POST') {
    let body = '';
    req.on('data', (chunk) => (body += chunk)).on('end', () => { reports.push(JSON.parse(body)); res.end(); });
    return undefined;
  }
  const sid = /(?:^|;\s*)sid=([^;]+)/.exec(req.headers.cookie ?? '')?.[1];
  if (req.url === '/login') {
    res.writeHead(302, { 'Set-Cookie': `sid=${randomUUID()}; HttpOnly; Path=/; SameSite=Lax`, Location: '/' });
    return res.end();
  }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  if (req.url.startsWith('/app')) return res.end(APP);
  return res.end(`<title>probe</title><p id="who">${sid ? `signed in: ${sid}` : 'signed out'}</p>`);
});
const sitePort = await freePort(5300);
await new Promise((resolve) => site.listen(sitePort, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${sitePort}`;

for (const entry of existsSync(scratch) ? readdirSync(scratch) : []) {
  if (entry !== 'bin') rmSync(join(scratch, entry), { recursive: true, force: true });
}

/** Start one host. Resolves once it has initialized and, where it has a port, the port answers. */
async function start(name, { page, browser, port = 0, persist = false, style = 'alloy', global = false, fresh = true } = {}) {
  const root = join(scratch, name);
  if (fresh) rmSync(root, { recursive: true, force: true });
  mkdirSync(root, { recursive: true });
  const log = join(root, 'events.jsonl');
  // A restart keeps the caches and not the log: the last run's `loaded` would read as this one's.
  rmSync(log, { force: true });
  const args = ['--root', root, '--log', log, '--title', `chromium-probe ${name}`, '--style', style];
  if (page) args.push('--page', page);
  if (browser) args.push('--browser', browser);
  if (port) args.push('--port', String(port));
  if (persist) args.push('--persist');
  if (global) args.push('--browser-global');
  const child = spawn(exe, args, { stdio: 'ignore' });
  const host = { name, child, root, log, port, events: () => readEvents(log) };
  for (let i = 0; i < 120; i++) {
    const ready = host.events().some((e) => e.what === 'initialized');
    const answering = !port || (await fetch(`http://127.0.0.1:${port}/json/version`).then((r) => r.ok).catch(() => false));
    const loaded = host.events().filter((e) => e.what === 'loaded').length >= [page, browser].filter(Boolean).length;
    if (ready && answering && loaded) return host;
    if (child.exitCode !== null) throw new Error(`${name} exited ${child.exitCode}`);
    await sleep(250);
  }
  throw new Error(`${name} never came up`);
}

function readEvents(log) {
  if (!existsSync(log)) return [];
  return readFileSync(log, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line));
}

/** The host's top-level windows, as UI Automation sees them, by its own pid. */
function windowsOf(pid) {
  const script = `
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $mine = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, ${pid})
    $root.FindAll([System.Windows.Automation.TreeScope]::Children, $mine) | ForEach-Object { "[" + $_.Current.Name + "]" }`;
  return spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).stdout.trim().split(/\r?\n/).filter(Boolean);
}

/** Close every window of this host as a person does, and wait for the process to end. */
async function close(host) {
  const script = `
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $mine = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, ${host.child.pid})
    $root.FindAll([System.Windows.Automation.TreeScope]::Children, $mine) | ForEach-Object {
      try { $_.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch {}
    }`;
  for (let attempt = 0; attempt < 3 && host.child.exitCode === null; attempt++) {
    spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' });
    for (let i = 0; i < 40 && host.child.exitCode === null; i++) await sleep(250);
  }
  if (host.child.exitCode === null) {
    host.child.kill(); // its own pid only, and said so
    console.log(`   (${host.name} did not close through its windows; its own process was ended)`);
  }
  await sleep(500);
}

function capture(host, label) {
  const out = join(scratch, `${label}.png`);
  spawnSync('powershell', ['-NoProfile', '-File', join(repoRoot, 'tools', 'shot-window.ps1'),
    '-OutFile', out, '-ProcessName', 'chromium-probe', '-ProcessId', String(host.child.pid)], { encoding: 'utf8' });
  return out;
}

async function pageTargets(port) {
  return ((await targetsAt(port)) ?? []).filter((t) => t.type === 'page');
}

async function inTarget(target, expression) {
  const cdp = await new Cdp(target.webSocketDebuggerUrl).open();
  try {
    return await cdp.evaluate(expression);
  } finally {
    cdp.close();
  }
}

async function browserCall(port, method, params = {}) {
  const version = await fetch(`http://127.0.0.1:${port}/json/version`).then((r) => r.json());
  const browser = await new Cdp(version.webSocketDebuggerUrl).open();
  try {
    return await browser.send(method, params);
  } finally {
    browser.close();
  }
}

/** Open a page in the browser's first page target and read who the site says is signed in. */
async function visit(port, path) {
  const target = (await pageTargets(port)).find((t) => !t.url.includes('/app'));
  const cdp = await new Cdp(target.webSocketDebuggerUrl).open();
  try {
    await cdp.send('Page.enable');
    await cdp.send('Page.navigate', { url: `${origin}${path}` });
    await sleep(1500);
    return await cdp.evaluate("document.getElementById('who')?.textContent ?? document.title");
  } finally {
    cdp.close();
  }
}

/** Playwright MCP over a port: the calls given, one at a time, each answer. */
function playwright(port, calls) {
  const input = [
    { jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'chromium-probe', version: '0' } } },
    { jsonrpc: '2.0', method: 'notifications/initialized' },
    ...calls.map((call, n) => ({ jsonrpc: '2.0', id: n + 2, method: 'tools/call', params: { name: call.tool, arguments: call.arguments ?? {} } })),
  ];
  return new Promise((resolve) => {
    const mcp = spawn('npx', ['-y', '@playwright/mcp@0.0.82', '--cdp-endpoint', `http://127.0.0.1:${port}`,
      '--output-dir', join(scratch, 'playwright')], { shell: true, stdio: ['pipe', 'pipe', 'ignore'] });
    let buffered = '';
    const answers = new Map();
    mcp.stdout.on('data', (chunk) => {
      buffered += chunk;
      let cut;
      while ((cut = buffered.indexOf('\n')) >= 0) {
        const line = buffered.slice(0, cut);
        buffered = buffered.slice(cut + 1);
        try {
          const frame = JSON.parse(line);
          if (frame.id) answers.set(frame.id, frame);
          if (frame.id === calls.length + 1) { mcp.stdin.end(); resolve(answers); }
        } catch { /* not a frame */ }
      }
    });
    let next = 0;
    const send = () => { if (next < input.length) mcp.stdin.write(`${JSON.stringify(input[next++])}\n`); };
    const timer = setInterval(() => {
      const waiting = input[next - 1]?.id;
      if (waiting === undefined || answers.has(waiting)) send();
    }, 200);
    setTimeout(() => { clearInterval(timer); mcp.stdin.end(); resolve(answers); }, 120_000).unref();
    mcp.on('exit', () => clearInterval(timer));
  });
}

const text = (answer) => (answer?.result?.content ?? []).map((c) => c.text).join('\n') || JSON.stringify(answer?.error ?? 'no answer');
const indent = (value) => String(value).split('\n').map((line) => `     ${line}`).join('\n');
const since = (host, count) => host.events().slice(count).filter((e) => e.role !== 'host').map((e) => `${e.role}:${e.what} ${JSON.stringify(e.detail)}`);

// `--bench-only` runs 5 and 6 alone, for working on the round trip without the rest.
let port = await freePort(9600);
phases: {
if (process.argv.includes('--bench-only')) break phases;

// ── 1. The debug port's reach ──────────────────────────────────────────────────────────────────
let one = await start('one-process', { page: `${origin}/app`, browser: `${origin}/`, port });
const init = one.events().find((e) => e.what === 'initialized').detail;
console.log(`CefSharp ${init.cefSharp}, CEF ${init.cef}, Chromium ${init.chromium}`);
const targets = await pageTargets(port);
console.log(`1a. one process, a port on it: its page targets are\n${indent(targets.map((t) => t.url).join('\n'))}`);
const app = targets.find((t) => t.url.includes('/app'));
console.log(`    the app's page over that port: ${app ? `reached; its bridge is ${await inTarget(app, 'typeof CefSharp === "object" ? typeof CefSharp.PostMessage : "absent"')}` : 'not listed'}`);
await close(one);

const quietPort = await freePort(port + 1);
const pageOnly = await start('page-process', { page: `${origin}/app` });
const browserOnly = await start('browser-process', { browser: `${origin}/`, port: quietPort });
console.log(`1b. two processes, the port on the browser's alone: its page targets are\n${indent((await pageTargets(quietPort)).map((t) => t.url).join('\n'))}`);
console.log(`    the page process's own port: ${pageOnly.port ? pageOnly.port : 'none set'}; both running at once: ${pageOnly.child.exitCode === null && browserOnly.child.exitCode === null}`);
await close(pageOnly);

// ── 2 and 4. A tab an agent opens, and Playwright MCP driving ────────────────────────────────────
const variants = [
  { label: 'alloy style, its own request context', host: browserOnly, style: 'alloy' },
  { label: 'chrome style, its own request context', name: 'browser-chrome', style: 'chrome' },
  { label: 'alloy style, the global request context', name: 'browser-global', style: 'alloy', global: true },
];
for (const variant of variants) {
  const style = variant.name ?? variant.style;
  port = await freePort(port + 2);
  const host = variant.host ?? await start(variant.name, { browser: `${origin}/`, port, style: variant.style, global: variant.global });
  console.log(`2. ${variant.label}`);
  const hostPort = host.port;
  const before = host.events().length;
  const windowsBefore = windowsOf(host.child.pid);
  // The control's own page, by its target id, taken while it is the only one.
  const control = (await pageTargets(hostPort))[0];
  const answers = await playwright(hostPort, [
    { tool: 'browser_tabs', arguments: { action: 'list' } },
    { tool: 'browser_snapshot' },
    { tool: 'browser_tabs', arguments: { action: 'new' } },
    { tool: 'browser_navigate', arguments: { url: `${origin}/?agent` } },
    { tool: 'browser_tabs', arguments: { action: 'list' } },
  ]);
  await sleep(1000);
  console.log(`   Playwright MCP's tabs before:\n${indent(text(answers.get(2)))}`);
  console.log(`   its snapshot of the page:\n${indent(text(answers.get(3)).split('\n').slice(0, 8).join('\n'))}`);
  console.log(`   browser_tabs new answered:\n${indent(text(answers.get(4)).split('\n').slice(0, 6).join('\n'))}`);
  console.log(`   browser_navigate answered:\n${indent(text(answers.get(5)).split('\n').slice(0, 4).join('\n'))}`);
  console.log(`   the tabs after:\n${indent(text(answers.get(6)))}`);
  console.log(`   CDP page targets while Playwright is gone: ${(await pageTargets(hostPort)).map((t) => t.url).join(' | ')}`);
  console.log(`   what the app was told: ${since(host, before).join('; ') || 'nothing'}`);
  console.log(`   windows before: ${windowsBefore.join(' ')}; after: ${windowsOf(host.child.pid).join(' ')}`);

  const raw = await browserCall(hostPort, 'Target.createTarget', { url: `${origin}/?cdp` }).catch((error) => ({ error: error.message }));
  await sleep(1500);
  console.log(`   Target.createTarget: ${JSON.stringify(raw)}; windows now: ${windowsOf(host.child.pid).join(' ')}`);
  console.log(`   photographed: ${capture(host, `agent-tab-${style}`)}`);
  console.log(`   the control's own page is now: ${(await pageTargets(hostPort)).find((t) => t.id === control.id)?.url ?? 'gone'}`);

  const beforeOpen = host.events().length;
  const cdp = await new Cdp(control.webSocketDebuggerUrl).open();
  await cdp.send('Runtime.evaluate', { expression: `window.open('${origin}/?popup'); 0`, userGesture: true });
  cdp.close();
  await sleep(1500);
  console.log(`   control, the control's page calling window.open: the app was told ${since(host, beforeOpen).join('; ') || 'nothing'}`);
  console.log(`   windows now: ${windowsOf(host.child.pid).join(' ')}`);

  // Does an agent's tab carry the browser's sign-in? Sign in on the control, then open a target.
  const signIn = await new Cdp(control.webSocketDebuggerUrl).open();
  await signIn.send('Page.enable');
  await signIn.send('Page.navigate', { url: `${origin}/login` });
  await sleep(1500);
  const onControl = await signIn.evaluate("document.getElementById('who')?.textContent");
  signIn.close();
  const shared = await browserCall(hostPort, 'Target.createTarget', { url: `${origin}/?share` });
  await sleep(1500);
  const sharedTarget = (await pageTargets(hostPort)).find((t) => t.id === shared.targetId);
  console.log(`   the control, signed in: ${onControl}; a CDP target opened after it: ${sharedTarget ? await inTarget(sharedTarget, "document.getElementById('who')?.textContent") : 'not listed'}`);
  await close(host);
}

// ── 3. A sign-in across a restart ────────────────────────────────────────────────────────────────
for (const persist of [true, false]) {
  const name = `cookies-${persist ? 'kept' : 'default'}`;
  port = await freePort(port + 1);
  let host = await start(name, { browser: `${origin}/`, port, persist });
  const signed = await visit(port, '/login');
  await close(host);
  host = await start(name, { browser: `${origin}/`, port, persist, fresh: false });
  console.log(`3. ${persist ? 'persist-session-cookies on' : "the engine's default"}: ${signed}; after a restart: ${await visit(port, '/')}`);
  await close(host);
}
} // phases

// ── 5. A round trip over the engine's own channel, and the codecs ────────────────────────────────
// The earlier phases' app pages reported too, with no benchmark; only this one's counts.
reports.length = 0;
const bench = await start('bench', { page: `${origin}/app?bench=500` });
for (let i = 0; i < 120 && !reports.some((r) => r.roundTrip); i++) await sleep(250);
const report = reports.find((r) => r.roundTrip) ?? reports[0];
console.log(`5. the page's bridge: ${report?.bridge}; round trip over CefSharp.PostMessage and a script back: ${JSON.stringify(report?.roundTrip)}`);
console.log(`   user agent: ${report?.ua}`);
console.log(`   codecs (canPlayType, and whether MSE takes it): ${JSON.stringify(report?.codecs)}`);
await close(bench);

// ── 6. What it adds to an install ───────────────────────────────────────────────────────────────
const files = [];
(function walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) walk(path);
    else files.push({ path, size: statSync(path).size });
  }
})(bin);
const mb = (bytes) => `${(bytes / 1048576).toFixed(0)} MB`;
const total = files.reduce((sum, f) => sum + f.size, 0);
const locales = files.filter((f) => f.path.includes(`${join('bin', 'locales')}`));
const kept = locales.filter((f) => /(en-US|zh-CN)\.pak$/.test(f.path));
const trimmed = total - locales.reduce((s, f) => s + f.size, 0) + kept.reduce((s, f) => s + f.size, 0);
const compressed = files.filter((f) => !f.path.includes(`${join('bin', 'locales')}`) || kept.includes(f))
  .reduce((sum, f) => sum + gzipSync(readFileSync(f.path), { level: 6 }).length, 0);
console.log(`6. on disk: ${mb(total)} (libcef.dll ${mb(files.find((f) => f.path.endsWith('libcef.dll')).size)}, ${locales.length} locales ${mb(locales.reduce((s, f) => s + f.size, 0))});`
  + ` with en-US and zh-CN only: ${mb(trimmed)}; that, compressed: ${mb(compressed)}`);

// The build's own account of its third-party licences, which the runtime package points at.
port = await freePort(port + 1);
const credits = await start('credits', { browser: 'chrome://credits', port });
const creditsPage = (await pageTargets(port)).find((t) => t.url.startsWith('chrome://credits'));
// textContent, not innerText: each licence is collapsed, and innerText skips what is hidden. The
// characters counted are the sanity check that this read anything at all.
const licences = creditsPage ? await inTarget(creditsPage, `(() => {
  const text = document.documentElement.textContent;
  const count = (word) => (text.match(new RegExp(word, 'gi')) ?? []).length;
  return { characters: text.length, chromium: count('chromium'), ffmpeg: count('ffmpeg'),
    lgpl: count('LGPL|Lesser General Public'), gpl: count('GNU General Public License'), mpl: count('Mozilla Public License') };
})()`) : null;
console.log(`   chrome://credits names: ${JSON.stringify(licences)}`);
await close(credits);

site.close();
console.log("done; every window the probe opened is closed, and its caches are under _fixtures/chromium-probe.");
process.exit(0);
