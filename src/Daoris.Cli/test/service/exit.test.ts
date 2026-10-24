import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { listenFetchable } from '../../../../tools/listen-port.mjs';
import { makeFixture } from '../_fixture.ts';

/**
 * CLIEXIT1: the CLI ended with `process.exit` straight after a service request and, on Windows with Node 24, aborted on a libuv
 * assertion (`!(handle->flags & UV_HANDLE_CLOSING)`) while a keep-alive socket was closing, so a script saw a failure for work
 * that was done. The entry point now sets `process.exitCode` and lets the loop drain (STUB1's fix in the rehearsal stub).
 *
 * This test spawns `node bin/daoris.mjs import` as a child process (the helper `runEntry` below) against a loopback stub service,
 * once leaving the socket to keep-alive and once destroying it right after the answer. `test/dogfood.test.ts` restricts spawning
 * in `src` and `bin`, not in tests.
 */

const entry = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'bin', 'daoris.mjs');

/** Runs the real entry point in a child process and returns its exit code and what it wrote. */
function runEntry(args: string[], env: Record<string, string>): Promise<{ code: number | null; out: string; err: string }> {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [entry, ...args], { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let out = '';
    let err = '';
    child.stdout.on('data', (chunk) => { out += chunk; });
    child.stderr.on('data', (chunk) => { err += chunk; });
    child.on('error', reject);
    child.on('close', (code) => resolve({ code, out, err }));
  });
}

for (const how of ['keep-alive', 'socket destroyed after the answer'] as const) {
  test(`a management command exits 0 with no assertion text after its request (${how})`, async () => {
    const fx = makeFixture(`cliexit-${how === 'keep-alive' ? 'alive' : 'destroyed'}`);
    const server = createServer((req, res) => {
      const body = req.url?.endsWith('/api/status')
        ? JSON.stringify({ tier: 'lexical only', home: '0123456789abcdef0123456789abcdef' })
        : JSON.stringify({ message: 'imported 1 repository.' });
      res.writeHead(200, { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(body) });
      res.end(body, () => { if (how !== 'keep-alive') res.socket?.destroy(); });
    });
    const url = await listenFetchable(server);
    try {
      const folder = fx.write('repo/README.md', '# repo\n') && join(fx.root, 'repo');
      const ran = await runEntry(['import', folder, '--workspace', 'w'], { DAORIS_SERVICE_URL: url });
      assert.doesNotMatch(ran.err, /Assertion failed/);
      assert.match(ran.out, /imported 1 repository/);
      assert.equal(ran.code, 0, ran.err);
    } finally {
      server.closeAllConnections();
      server.close();
      fx.cleanup();
    }
  });
}
