import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer, type Server, type ServerResponse } from 'node:http';
import { join } from 'node:path';
import { listenFetchable } from '../../../../tools/listen-port.mjs';
import { endpoint, request, RETRY_AFTER_MS, RUNNING_FILE, runningService } from '../../src/service/service.ts';
import { HOME_ID_FILE } from '../../src/platform/home.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * 🔴 SERVICESTALL1: on 2026-10-28 a terminal's ask exited 2 with "no DAORIS_SERVICE_URL … nothing answered as its service" a minute
 * after a landing, on a machine at full load, and the service answered 200 in 62 ms ten seconds later. A busy service is said as busy:
 * the sentence names the address, the bound and that it asked again, and does not blame the variable; a read is asked once more
 * after a short wait, a press that changes state is never asked again blindly. Stand-ins on the loopback answer late or never.
 */

const HOME_ID = '0123456789abcdef0123456789abcdef';

/** No wait between a try and its second: the tests' own bound is what a real wait would be spent on. */
const quickly = async () => {};

/** A loopback service that answers each request as told, in the order asked, and counts what it was asked. */
async function stand(answer: (nth: number, res: ServerResponse) => void): Promise<{ url: string; asked: string[]; server: Server }> {
  const asked: string[] = [];
  const server = createServer((req, res) => {
    asked.push(`${req.method} ${req.url}`);
    // A client that gave up leaves a socket the late answer is written to: nothing to say about it.
    res.on('error', () => {});
    answer(asked.length, res);
  });
  return { url: await listenFetchable(server), asked, server };
}

const json = (res: ServerResponse, body: unknown) => {
  res.writeHead(200, { 'content-type': 'application/json' });
  res.end(JSON.stringify(body));
};

/** Answers after `ms`, which a bound shorter than that has given up on. */
const late = (ms: number, res: ServerResponse, body: unknown) => { setTimeout(() => json(res, body), ms).unref(); };

async function withService<T>(url: string, run: () => Promise<T>): Promise<T> {
  const before = process.env.DAORIS_SERVICE_URL;
  process.env.DAORIS_SERVICE_URL = url;
  try {
    return await run();
  } finally {
    if (before === undefined) delete process.env.DAORIS_SERVICE_URL; else process.env.DAORIS_SERVICE_URL = before;
  }
}

test('a read the service answers late is asked once more and succeeds', async () => {
  const service = await stand((nth, res) => (nth === 1 ? late(600, res, { slow: true }) : json(res, { ok: true })));
  const waits: number[] = [];
  try {
    const answered = await withService(service.url, () => request('GET', '/api/things', undefined, {
      within: 150, pause: async (ms) => { waits.push(ms); },
    }));
    assert.deepEqual(answered.json, { ok: true });
    assert.deepEqual(service.asked, ['GET /api/things', 'GET /api/things']);
    assert.deepEqual(waits, [RETRY_AFTER_MS]);
  } finally {
    service.server.closeAllConnections();
    service.server.close();
  }
});

test('a press the service answers late is not asked again: the sentence names the address, the bound and what to check', async () => {
  const service = await stand((_, res) => late(600, res, { done: true }));
  try {
    await assert.rejects(
      withService(service.url, () => request('POST', '/api/registry', { repository: 'r' }, { within: 150, pause: quickly })),
      (error: Error) => {
        assert.ok(error.message.includes(`the service at ${service.url} did not answer within 0.15 seconds to /api/registry`), error.message);
        assert.match(error.message, /asked for a change, so it was not asked again/);
        assert.match(error.message, /look at what it changes/);
        assert.match(error.message, /DAORIS_SERVICE_URL is not what is missing/);
        return true;
      });
    assert.deepEqual(service.asked, ['POST /api/registry'], 'a press is sent once');
  } finally {
    service.server.closeAllConnections();
    service.server.close();
  }
});

test('a service that never answers a read is asked twice and then said honestly', async () => {
  const service = await stand(() => {});
  try {
    await assert.rejects(
      withService(service.url, () => request('GET', '/api/things', undefined, { within: 120, pause: quickly })),
      (error: Error) => {
        assert.ok(error.message.includes(`the service at ${service.url} did not answer within 0.12 seconds to /api/things`), error.message);
        assert.match(error.message, /not again after a second try 1 second later/);
        assert.doesNotMatch(error.message, /^no DAORIS_SERVICE_URL/);
        return true;
      });
    assert.deepEqual(service.asked, ['GET /api/things', 'GET /api/things']);
  } finally {
    service.server.closeAllConnections();
    service.server.close();
  }
});

test('an address the home left that nothing answers at is not blamed on the variable, and its status is asked twice', async () => {
  const fx = makeFixture('stall-record');
  try {
    fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
    fx.write(join('data', RUNNING_FILE), JSON.stringify({ url: 'http://localhost:5181', pid: process.pid, started: 1, since: '2026-10-28T03:54:00+00:00' }));
    fx.write(join('data', HOME_ID_FILE), JSON.stringify({ id: HOME_ID, minted: '2026-10-16T08:00:00+00:00' }));

    const asked: string[] = [];
    const waits: number[] = [];
    const never = async (url: string) => { asked.push(url); throw new DOMException('timed out', 'TimeoutError'); };
    await assert.rejects(endpoint({}, never, fx.root), (error: Error) => {
      assert.doesNotMatch(error.message, /no DAORIS_SERVICE_URL/);
      assert.ok(error.message.startsWith(`Daoris is running on ${join(fx.root, 'data')} (pid ${process.pid}), and nothing answered as its service at http://localhost:5181 within 2 seconds, asked twice, 1 second apart.`), error.message);
      assert.match(error.message, /DAORIS_SERVICE_URL is not the cause: the service may be busy/);
      return true;
    });
    assert.deepEqual(asked, ['http://localhost:5181/api/status', 'http://localhost:5181/api/status']);

    // Late: the first try is spent, the second is answered as this home's host.
    let nth = 0;
    const lateOnce = async () => {
      if (++nth === 1) throw new DOMException('timed out', 'TimeoutError');
      return Response.json({ tier: 'lexical only', home: HOME_ID });
    };
    assert.deepEqual(
      await runningService(join(fx.root, 'data'), fx.root, lateOnce, () => true, async (ms) => { waits.push(ms); }),
      { url: 'http://localhost:5181' });
    assert.equal(nth, 2);
    assert.deepEqual(waits, [RETRY_AFTER_MS]);
  } finally {
    fx.cleanup();
  }
});
