import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import type { AddressInfo } from 'node:net';
import { commandBrainstorm } from '../../src/service/brainstorm.ts';
import { DaorisError } from '../../src/platform/errors.ts';

/**
 * 🔴 BRAINSTORM1a3 (D176 points 1 and 8): `daoris brainstorm` is the driver verb's twin from any terminal. It writes nothing
 * itself: it reaches this machine's Daoris through `service.ts`, by the local host's machine-only door, which writes the request
 * the driver's look takes and answers show, send, clear and list. Tried here against a host that stands in: a real HTTP server on
 * the loopback, holding brainstorms in memory as the service's door holds them on disk, so what is asserted is the request the
 * verb makes and what it says of the answer. A host off this machine is never asked, and a host that keeps no brainstorm (a
 * shared one) is said to keep none.
 */

type Asked = { method: string; path: string; body: unknown };

const PACKET = {
  packet: 1,
  digest: 'sha256:5f1c',
  question: 'Why does the daily report drop its last day?',
  repository: 'reports',
  tier: 'words',
  sources: [{
    id: 'S1', kind: 'decision', path: 'docs/decisions/D12.md', lines: [3, 9], revision: '0123456789abcdef',
    why: { words: 'daily report', tier: 'words' }, kept: 18, of: 40, text: 'The window is open.\nIt ends a day early.',
  }],
  leftOut: [{ path: 'docs/long.md', why: 'bound' }],
};

/** One brainstorm as the door answers it, in the state the test sets. */
function standing(id: string, state: string, extra: Record<string, unknown> = {}) {
  return {
    id, question: 'Why does the daily report drop its last day?', repository: 'reports', agents: ['stub'], at: '2026-10-19T10:00:00Z',
    state, code: null, packet: state === 'asked' ? null : PACKET.digest, sent: null, ...extra,
  };
}

/**
 * The host that stands in, as the service's door answers: `shared` keeps no brainstorm, so every door is a 404 with no sentence,
 * as a shared host that maps none answers.
 */
async function standIn({ shared = false } = {}) {
  const asked: Asked[] = [];
  const kept = new Map<string, { state: string; document: string | null; sent: string | null }>([
    ['a0000001', { state: 'asked', document: null, sent: null }],
    ['f0000002', { state: 'frozen', document: null, sent: null }],
    ['e0000003', { state: 'ended', document: '# Brainstorm e0000003\n\n1. Inclusive window\n', sent: PACKET.digest }],
    ['r0000004', { state: 'running', document: null, sent: PACKET.digest }],
  ]);
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => { text += chunk; });
    request.on('end', () => {
      const path = request.url ?? '';
      asked.push({ method: request.method ?? '', path, body: text ? JSON.parse(text) : undefined });
      const answer = (status: number, body?: unknown) => {
        response.writeHead(status, body === undefined ? {} : { 'content-type': 'application/json' });
        response.end(body === undefined ? '' : JSON.stringify(body));
      };
      if (shared) return answer(404);

      const [, , , id, verb] = path.split('/');
      if (request.method === 'GET' && path === '/api/brainstorms') {
        return answer(200, [...kept].map(([each, { state }]) => standing(each, state)));
      }
      if (request.method === 'POST' && path === '/api/brainstorms') {
        const body = JSON.parse(text) as { question?: string };
        if (!body.question?.trim()) return answer(400, { error: 'A brainstorm needs its question. Nothing was asked.' });
        kept.set('n0000005', { state: 'asked', document: null, sent: null });
        return answer(200, { brainstorm: standing('n0000005', 'asked'), message: 'Brainstorm `n0000005` asked: Daoris freezes its packet at its next look.' });
      }
      const one = id ? kept.get(id) : undefined;
      if (!one) return answer(404, { error: `There is no brainstorm \`${id}\` on this machine.` });
      if (request.method === 'GET' && !verb) {
        return answer(200, {
          brainstorm: standing(id!, one.state, { sent: one.sent }),
          frozen: one.state === 'asked' ? null : PACKET,
          document: one.document,
        });
      }
      if (request.method === 'POST' && verb === 'send') {
        one.sent = PACKET.digest;
        return answer(200, { brainstorm: standing(id!, one.state, { sent: one.sent }), message: `Brainstorm \`${id}\` sent, packet \`${PACKET.digest}\`.` });
      }
      if (request.method === 'DELETE' && !verb) {
        if (one.state === 'running') return answer(409, { error: `Brainstorm \`${id}\` is running; it is cleared once it ends.` });
        kept.delete(id!);
        return answer(200, { brainstorm: standing(id!, one.state), message: `Brainstorm \`${id}\` cleared: its records and its packet are gone from this machine.` });
      }
      return answer(405);
    });
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  const url = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  process.env.DAORIS_SERVICE_URL = url;
  return { url, asked, kept, close: () => new Promise<void>((resolve) => server.close(() => resolve())) };
}

afterEach(() => {
  delete process.env.DAORIS_SERVICE_URL;
});

async function run(argv: string[]): Promise<{ code: number; out: string[] }> {
  const out: string[] = [];
  const code = await commandBrainstorm({ root: '.', argv, write: (line: string) => out.push(line), packageRoot: '' });
  return { code, out };
}

test('a question is posted to the brainstorm door with its repository and agents, and the service\'s sentence is said', async () => {
  const host = await standIn();
  try {
    const { code, out } = await run(['Why does the daily report drop its last day?', '--repository', 'reports', '--agents', 'stub, acp-stub']);

    assert.equal(code, 0);
    assert.deepEqual(host.asked, [{
      method: 'POST', path: '/api/brainstorms',
      body: { question: 'Why does the daily report drop its last day?', repository: 'reports', agents: ['stub', 'acp-stub'] },
    }]);
    assert.match(out.join('\n'), /Brainstorm `n0000005` asked/);
    assert.match(out.join('\n'), /daoris brainstorm show n0000005/);
  } finally {
    await host.close();
  }
});

test('a question with no repository names none, and no agents names none', async () => {
  const host = await standIn();
  try {
    assert.equal((await run(['What should the next release hold?'])).code, 0);
    assert.deepEqual(host.asked[0]!.body, { question: 'What should the next release hold?', repository: null, agents: [] });
  } finally {
    await host.close();
  }
});

test('show prints the packet whole before it is sent, and says nothing has gone to any agent', async () => {
  const host = await standIn();
  try {
    const { code, out } = await run(['show', 'f0000002']);

    assert.equal(code, 0);
    assert.deepEqual(host.asked.map((each) => `${each.method} ${each.path}`), ['GET /api/brainstorms/f0000002']);
    const shown = out.join('\n');
    assert.match(shown, /Brainstorm `f0000002`, packet `sha256:5f1c`/);
    assert.match(shown, /> Why does the daily report drop its last day\?/);
    assert.match(shown, /1 sources of `reports`, found by words\./);
    assert.match(shown, /S1 {2}decision {2}docs\/decisions\/D12\.md:3-9 at 0123456789ab {2}\(18 of 40 characters; found by "daily report", words\)/);
    assert.match(shown, / {4}It ends a day early\./);
    assert.match(shown, /Left out: docs\/long\.md \(bound\)/);
    assert.match(shown, /Nothing has gone to any agent\. Send it with `daoris brainstorm send f0000002`, or clear it\./);
  } finally {
    await host.close();
  }
});

test('show says a brainstorm with no packet yet waits for the look, a running one runs, and an ended one is its document', async () => {
  const host = await standIn();
  try {
    assert.match((await run(['show', 'a0000001'])).out.join('\n'), /Brainstorm `a0000001` waits for Daoris's next look, which freezes its packet\./);
    assert.match((await run(['show', 'r0000004'])).out.join('\n'), /Sent, and running\./);
    const ended = await run(['show', 'e0000003']);
    assert.equal(ended.code, 0);
    assert.deepEqual(ended.out, ['# Brainstorm e0000003\n\n1. Inclusive window\n']);
  } finally {
    await host.close();
  }
});

test('send is the person\'s press, posted to the brainstorm\'s send door, and clear its delete', async () => {
  const host = await standIn();
  try {
    const sent = await run(['send', 'f0000002']);
    assert.equal(sent.code, 0);
    assert.match(sent.out.join('\n'), /Brainstorm `f0000002` sent, packet `sha256:5f1c`/);

    const cleared = await run(['clear', 'e0000003']);
    assert.equal(cleared.code, 0);
    assert.match(cleared.out.join('\n'), /cleared: its records and its packet are gone/);
    assert.deepEqual(host.asked.map((each) => `${each.method} ${each.path} ${JSON.stringify(each.body ?? null)}`),
      ['POST /api/brainstorms/f0000002/send null', 'DELETE /api/brainstorms/e0000003 null']);
    assert.equal(host.kept.has('e0000003'), false);
  } finally {
    await host.close();
  }
});

test('list names each brainstorm with where it stands, oldest first', async () => {
  const host = await standIn();
  try {
    const { code, out } = await run(['list']);
    assert.equal(code, 0);
    assert.deepEqual(out, [
      'a0000001  asked  Why does the daily report drop its last day?',
      'f0000002  frozen  Why does the daily report drop its last day?',
      'e0000003  ended  Why does the daily report drop its last day?',
      'r0000004  running  Why does the daily report drop its last day?',
    ]);
  } finally {
    await host.close();
  }
});

test('a refusal is the service\'s sentence, said as a policy failure: no brainstorm by that id, or a running one cleared', async () => {
  const host = await standIn();
  try {
    await assert.rejects(run(['show', 'nope0000']), (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 1 && error.message === 'There is no brainstorm `nope0000` on this machine.');
    await assert.rejects(run(['clear', 'r0000004']), (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 1 && /is running; it is cleared once it ends/.test(error.message));
    await assert.rejects(run(['  ']), (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 1 && /needs its question/.test(error.message));
  } finally {
    await host.close();
  }
});

test('a host off this machine is never asked: a brainstorm is kept on the machine that asked it', async () => {
  const host = await standIn();
  try {
    process.env.DAORIS_SERVICE_URL = 'https://team.example.com';
    for (const argv of [['Why?'], ['list'], ['show', 'f0000002'], ['send', 'f0000002'], ['clear', 'f0000002']]) {
      await assert.rejects(run(argv), (error: unknown) =>
        error instanceof DaorisError && error.exitCode === 1 && /team\.example\.com is not this machine's service/.test(error.message)
        && /nothing was sent/.test(error.message));
    }
    assert.equal(host.asked.length, 0);
  } finally {
    await host.close();
  }
});

test('a host on this machine that keeps no brainstorm, as a shared host keeps none, is said to keep none', async () => {
  const host = await standIn({ shared: true });
  try {
    await assert.rejects(run(['list']), (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 1
      && error.message.includes(`the service at ${host.url} keeps no brainstorm`) && /a shared host keeps none/.test(error.message));
  } finally {
    await host.close();
  }
});

test('the verb with nothing, a verb with no id, or a flag with no value is its usage, and nothing is sent', async () => {
  const host = await standIn();
  try {
    for (const argv of [[], ['show'], ['send', 'a', 'b'], ['Why?', '--repository'], ['Why?', '--colour', 'red'], ['--agents', 'stub']]) {
      await assert.rejects(run(argv), (error: unknown) =>
        error instanceof DaorisError && error.exitCode === 2 && /daoris brainstorm "<question>"/.test(error.message), argv.join(' '));
    }
    assert.equal(host.asked.length, 0);
  } finally {
    await host.close();
  }
});

test('--dry-run says what a write would do and sends nothing; a read is a read', async () => {
  const host = await standIn();
  try {
    assert.match((await run(['Why?', '--dry-run'])).out.join('\n'), /would ask a brainstorm: Why\?/);
    assert.match((await run(['send', 'f0000002', '--dry-run'])).out.join('\n'), /would send brainstorm `f0000002`/);
    assert.match((await run(['clear', 'f0000002', '--dry-run'])).out.join('\n'), /would clear brainstorm `f0000002`/);
    assert.equal(host.asked.length, 0);
  } finally {
    await host.close();
  }
});
