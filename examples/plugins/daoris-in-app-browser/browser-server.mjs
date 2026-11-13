// The plugin's MCP server: Playwright MCP over the CDP endpoint of Daoris's browser, given the context a session is to drive
// (shared with the person through its own pages, or isolated where the plugin's setting says), never one of its own choosing.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { holdOthers } from './hold-others.mjs';
import { chooseContext } from './isolation.mjs';
import { startRelay } from './relay.mjs';

const PLAYWRIGHT_MCP = '@playwright/mcp@0.0.82';

/** The command line's `--name value` pairs. */
export function readArguments(argv) {
  const values = {};
  for (let i = 0; i < argv.length; i += 2) values[argv[i].replace(/^--/, '')] = argv[i + 1];
  return values;
}

/** The pinned Playwright MCP, installed once under the plugin's data folder and resolved from there. */
export function loadPackages(dataFolder) {
  const runtime = join(dataFolder, 'runtime');
  const marker = join(runtime, 'node_modules', '@playwright', 'mcp', 'package.json');
  if (!existsSync(marker)) {
    // Sessions start together: each installs beside the folder and moves it into place, and the first to arrive keeps it.
    const staging = `${runtime}.${process.pid}`;
    rmSync(staging, { recursive: true, force: true });
    mkdirSync(staging, { recursive: true });
    writeFileSync(join(staging, 'package.json'), '{"private":true}\n');
    // stdout is the MCP stream: the install speaks on stderr alone.
    const npm = spawnSync(`npm install --no-audit --no-fund --prefix "${staging}" ${PLAYWRIGHT_MCP}`, { stdio: ['ignore', 2, 2], shell: true });
    try {
      if (npm.status !== 0) throw new Error(`could not install ${PLAYWRIGHT_MCP} (npm exited ${npm.status})`);
      if (!existsSync(marker)) renameSync(staging, runtime);
    } catch (error) {
      if (!existsSync(marker)) throw error;
    } finally {
      rmSync(staging, { recursive: true, force: true });
    }
  }
  const require = createRequire(join(runtime, 'noop.js'));
  return {
    createConnection: require('@playwright/mcp').createConnection,
    chromium: require('playwright-core').chromium,
    StdioServerTransport: require('playwright-core/lib/utilsBundle').StdioServerTransport,
    ws: { WebSocket: require('playwright-core/lib/utilsBundle').ws, WebSocketServer: require('playwright-core/lib/utilsBundle').wsServer },
  };
}

/** The browser's DevTools socket for an endpoint given as that socket or as the browser's HTTP address. */
async function socketOf(endpoint) {
  if (/^wss?:/i.test(endpoint)) return endpoint;
  const version = await (await fetch(new URL('/json/version', endpoint))).json();
  return version.webSocketDebuggerUrl;
}

async function main() {
  const options = readArguments(process.argv.slice(2));
  const endpoint = options['cdp-endpoint'];
  const dataFolder = options.data;
  if (!endpoint || !dataFolder) throw new Error('usage: browser-server.mjs --cdp-endpoint <url> --data <folder> [--output-dir <folder>]');
  const { createConnection, chromium, StdioServerTransport, ws } = loadPackages(dataFolder);

  let browser;
  let relay;
  let chosen;
  let server;
  const contextOfSession = async () => {
    if (!browser) {
      relay = await startRelay({ upstream: await socketOf(endpoint), ws, filter: send => holdOthers(send) });
      browser = await chromium.connectOverCDP(relay.url);
    }
    const client = server?.getClientVersion?.();
    chosen = await chooseContext(browser, dataFolder, { client: client?.name, role: process.env.DAORIS_SESSION_ROLE });
    return chosen.context;
  };
  server = await createConnection(options['output-dir'] ? { outputDir: options['output-dir'] } : {}, contextOfSession);

  let ending;
  const end = () => ending ??= (async () => {
    await server.close().catch(() => {});
    await chosen?.end().catch(() => {});
    // Disconnecting never closes the person's browser: only what this session made goes with it.
    await browser?.close().catch(() => {});
    await relay?.close().catch(() => {});
  })().finally(() => process.exit(0));
  process.stdin.on('end', end);
  process.on('SIGTERM', end);
  process.on('SIGINT', end);
  await server.connect(new StdioServerTransport());
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    process.stderr.write(`${error?.stack ?? error}\n`);
    process.exit(1);
  });
}
