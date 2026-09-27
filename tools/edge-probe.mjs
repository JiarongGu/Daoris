/**
 * The measurement behind the in-app browser's next shape (2026-09-28): is a real Edge, started and
 * kept by Daoris on a profile of its own, a better in-app browser than Daoris's own WebView2 window?
 *
 * BRW4–BRW6 rebuilt a browser's basics by hand on WebView2 and found two things no embedding answers:
 * a tab an agent opens over CDP has no window, and a session cookie ends with the process. A real
 * Edge has tabs, history, favorites, devtools and downloads of its own. What is measured here is
 * what would make or break it as Daoris's browser:
 *
 *   1. It accepts a debug port on a profile that is not the default one, and Playwright MCP drives it.
 *   2. A tab an agent opens (`browser_tabs new`) is a tab the person can see.
 *   3. A sign-in (a cookie with no expiry) survives Edge restarting: by default, and with its own
 *      "continue where you left off" setting.
 *
 *   node tools/edge-probe.mjs
 *
 * 🔴 It starts its OWN Edge: a fresh `--user-data-dir` under `_fixtures/edge-probe`, which is a
 * separate browser process from the person's, and it closes only that one, over its own CDP port.
 * Nothing is killed by name. A stand-in site on loopback plays the identity server.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { randomUUID } from 'node:crypto';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { Cdp, freePort, targetsAt } from './cdp.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'edge-probe');
const profile = join(scratch, 'profile');
const EDGE = ['C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'].find((path) => existsSync(path));
if (!EDGE) {
  console.error('no Edge at its standard install path');
  process.exit(2);
}

// The stand-in identity server: /login sets a cookie with no expiry, / says who is signed in.
const site = createServer((req, res) => {
  const sid = /(?:^|;\s*)sid=([^;]+)/.exec(req.headers.cookie ?? '')?.[1];
  if (req.url === '/login') {
    res.writeHead(302, { 'Set-Cookie': `sid=${randomUUID()}; HttpOnly; Path=/; SameSite=Lax`, Location: '/' });
    return res.end();
  }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  return res.end(`<title>probe</title><p id="who">${sid ? `signed in: ${sid}` : 'signed out'}</p>`);
});
const sitePort = await freePort(5300);
await new Promise((resolve) => site.listen(sitePort, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${sitePort}`;

rmSync(scratch, { recursive: true, force: true });
mkdirSync(profile, { recursive: true });
const cdpPort = await freePort(9500);

/** Start this probe's Edge, and wait for its debug port to answer. */
async function start() {
  const edge = spawn(EDGE, [
    `--user-data-dir=${profile}`, `--remote-debugging-port=${cdpPort}`,
    '--no-first-run', '--no-default-browser-check',
    // 🔴 Without it a fresh profile signs in to the person's Microsoft account and syncs their
    // extensions and open tabs within seconds (measured, 2026-09-28). It stops the sync; the account
    // stays signed in, which no flag here changes.
    '--disable-sync', 'about:blank',
  ], { stdio: 'ignore', detached: false });
  for (let i = 0; i < 60; i++) {
    const version = await fetch(`http://127.0.0.1:${cdpPort}/json/version`).then((r) => r.json()).catch(() => null);
    if (version) return { edge, version };
    await sleep(500);
  }
  throw new Error('Edge never answered on its debug port');
}

/** Open a page in the first page target and read who the site says is signed in. */
async function visit(path) {
  const page = (await targetsAt(cdpPort)).find((t) => t.type === 'page');
  const cdp = await new Cdp(page.webSocketDebuggerUrl).open();
  await cdp.send('Page.enable');
  await cdp.send('Page.navigate', { url: `${origin}${path}` });
  await sleep(1500);
  const who = await cdp.evaluate("document.getElementById('who')?.textContent ?? document.title");
  cdp.ws.close();
  return who;
}

/** Close this probe's Edge the way a person closing the window would, over its own port. */
async function stop(edge) {
  const version = await fetch(`http://127.0.0.1:${cdpPort}/json/version`).then((r) => r.json());
  const browser = await new Cdp(version.webSocketDebuggerUrl).open();
  await browser.send('Browser.close').catch(() => {});
  for (let i = 0; i < 40 && edge.exitCode === null; i++) await sleep(250);
  await sleep(1500); // the profile's files are written as the browser winds down
}

/** Edge's own tabs, as its window shows them — read through UI Automation, by this Edge's pid. */
function visibleTabs(pid) {
  const script = `
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $mine = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, ${pid})
    $tab = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)
    $root.FindAll([System.Windows.Automation.TreeScope]::Children, $mine) | ForEach-Object {
      $_.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tab) | ForEach-Object { $_.Current.Name }
    }`;
  return spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).stdout.trim().split(/\r?\n/).filter(Boolean);
}

/** Playwright MCP over this Edge's endpoint: the calls given, each answer's first lines. */
function playwright(calls) {
  const input = [
    { jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'edge-probe', version: '0' } } },
    { jsonrpc: '2.0', method: 'notifications/initialized' },
    ...calls.map((call, n) => ({ jsonrpc: '2.0', id: n + 2, method: 'tools/call', params: { name: call.tool, arguments: call.arguments ?? {} } })),
  ];
  return new Promise((resolve) => {
    const mcp = spawn('npx', ['-y', '@playwright/mcp@0.0.82', '--cdp-endpoint', `http://127.0.0.1:${cdpPort}`,
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
          if (frame.id === calls.length + 1) { mcp.kill(); resolve(answers); }
        } catch { /* not a frame */ }
      }
    });
    // One at a time: a call is sent when the one before it has answered.
    let next = 0;
    const send = () => { if (next < input.length) mcp.stdin.write(`${JSON.stringify(input[next++])}\n`); };
    const timer = setInterval(() => {
      const waiting = input[next - 1]?.id;
      if (waiting === undefined || answers.has(waiting)) send();
    }, 200);
    setTimeout(() => { clearInterval(timer); mcp.kill(); resolve(answers); }, 120_000).unref();
    mcp.on('exit', () => clearInterval(timer));
  });
}

const text = (answer) => (answer?.result?.content ?? []).map((c) => c.text).join('\n') || JSON.stringify(answer?.error ?? 'no answer');

/** Close this Edge's window as a person does — UI Automation's Close on its own window — and wait for it to go. */
async function closeWindow(edge) {
  const script = `
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $mine = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, ${edge.pid})
    $root.FindAll([System.Windows.Automation.TreeScope]::Children, $mine) | ForEach-Object {
      $_.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    }`;
  spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' });
  for (let i = 0; i < 60 && edge.exitCode === null; i++) await sleep(250);
  await sleep(1500);
}

/** A photograph of this Edge's window, by its pid — never the person's Edge beside it. */
function capture(edge, name) {
  const out = join(scratch, `${name}.png`);
  spawnSync('powershell', ['-NoProfile', '-File', join(repoRoot, 'tools', 'shot-window.ps1'),
    '-OutFile', out, '-ProcessName', 'msedge', '-ProcessId', String(edge.pid)], { encoding: 'utf8' });
  return out;
}

/** The browser's cookies, over its own port. */
async function browserCall(method, params = {}) {
  const version = await fetch(`http://127.0.0.1:${cdpPort}/json/version`).then((r) => r.json());
  const browser = await new Cdp(version.webSocketDebuggerUrl).open();
  const answer = await browser.send(method, params);
  browser.ws.close();
  return answer;
}

console.log(`Edge ${EDGE}`);
let { edge, version } = await start();
console.log(`1. ${version.Browser} answered on a debug port, profile under _fixtures (pid ${edge.pid})`);

console.log(`   signed out at first: ${await visit('/')}`);
console.log(`   after the sign-in:   ${await visit('/login')}`);

const answers = await playwright([
  { tool: 'browser_tabs', arguments: { action: 'list' } },
  { tool: 'browser_tabs', arguments: { action: 'new' } },
  { tool: 'browser_navigate', arguments: { url: `${origin}/` } },
  { tool: 'browser_tabs', arguments: { action: 'list' } },
]);
console.log(`2. Playwright MCP's tabs before:\n${text(answers.get(2)).split('\n').map((l) => `     ${l}`).join('\n')}`);
console.log(`   after browser_tabs new and a navigate:\n${text(answers.get(5)).split('\n').map((l) => `     ${l}`).join('\n')}`);
console.log(`   CDP page targets: ${(await targetsAt(cdpPort)).filter((t) => t.type === 'page').map((t) => t.title || t.url).join(' | ')}`);
console.log(`   tabs Edge's window shows: ${visibleTabs(edge.pid).join(' | ') || '(none read)'}`);
console.log(`   photographed: ${capture(edge, 'after-agent-tab')}`);

// 3. The sign-in across a restart, closed as a person closes a window, after the cookie store has
// had time to write (it flushes on a timer, about thirty seconds).
const FLUSH = 35_000;
console.log(`3a. Edge's defaults: ${await visit('/login')}; waiting ${FLUSH / 1000}s, then closing the window`);
await sleep(FLUSH);
await closeWindow(edge);
({ edge } = await start());
console.log(`    restarted: ${await visit('/')}`);

// Edge's own "continue where you left off": restore_on_startup = 1, in the profile's preferences.
await stop(edge);
const preferences = join(profile, 'Default', 'Preferences');
const prefs = JSON.parse(readFileSync(preferences, 'utf8'));
prefs.session = { ...(prefs.session ?? {}), restore_on_startup: 1 };
writeFileSync(preferences, JSON.stringify(prefs));
({ edge } = await start());
console.log(`3b. with "continue where you left off": ${await visit('/login')}; waiting, then closing the window`);
await sleep(FLUSH);
await closeWindow(edge);
({ edge } = await start());
console.log(`    restarted: ${await visit('/')}`);
console.log(`    tabs restored: ${visibleTabs(edge.pid).join(' | ') || '(none read)'}`);

// 3c. Daoris keeping them, as BRW10 does for its own window: the session cookies read over CDP before
// the close, and put back over CDP before the first page after it.
console.log(`3c. Daoris keeps them: ${await visit('/login')}`);
const { cookies } = await browserCall('Storage.getCookies');
const kept = cookies.filter((cookie) => cookie.session);
await stop(edge);
({ edge } = await start());
await browserCall('Storage.setCookies', {
  cookies: kept.map(({ name, value, domain, path, secure, httpOnly, sameSite }) => ({ name, value, domain, path, secure, httpOnly, sameSite })),
});
console.log(`    kept ${kept.length} session cookie(s); restarted: ${await visit('/')}`);

await stop(edge);
site.close();
console.log('done; the probe\'s Edge is closed, and its profile is under _fixtures/edge-probe.');
process.exit(0);
