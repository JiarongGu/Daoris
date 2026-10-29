import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { listenFetchable } from '../../../tools/listen-port.mjs';
import {
  doorCommand, EVERY_DOOR, KINDS, programOf, providersFolder, readProviders, readStandIns, SCENARIO_CONTRACT, scenarioPlan,
  type PlanRow, type ProviderRead, type StandInDoor,
} from '../../../tools/stand-ins.mjs';
import { makeFixture, PATIENCE, unroled } from '../test/_fixture.ts';

/**
 * AGENTPROVIDER1 row b, part 3 (D213's row b part 3 note): the stand-ins found from their files, as the family rehearsal's stand-in phase
 * finds them. The reader and the phase's plan over a scratch folder of providers, the repository's own stand-ins read with no break of
 * the layout, and each of their programs answering a start and a turn on each door against a stand-in for the service's quest door, so
 * a stand-in added later is held by these rows with no edit to them. Run by the `tool-tests` gate; only the parent runs the rehearsal.
 */

type Door = StandInDoor;
type StandIn = ProviderRead;
type Row = PlanRow;

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

/** A provider's folder under a scratch providers' folder: its file, its pending cases where given, and its program's entry. */
function provider(fx: ReturnType<typeof makeFixture>, folder: string, file: object | string, pending?: object[]): void {
  fx.write(`Providers/${folder}/provider.json`, typeof file === 'string' ? file : `${JSON.stringify(file, null, 2)}\n`);
  if (pending) fx.write(`Providers/${folder}/pending.json`, `${JSON.stringify(pending, null, 2)}\n`);
  fx.write(`Providers/${folder}/Program/agent.mjs`, '// a scratch program\n');
}

function twoProviders(name: string): string {
  const fx = makeFixture(name);
  provider(fx, 'First', {
    id: 'first-stand-in', standIn: true, program: { entry: 'agent.mjs' },
    doors: [
      { id: 'first-native', kind: 'native', arguments: ['--door', 'native'] },
      { id: 'first-protocol', kind: 'protocol', arguments: ['--door', 'protocol'] },
    ],
  }, [
    { case: 'live.limit-cools', door: EVERY_DOOR, row: 'l', why: 'Held on every door.' },
    { case: 'live.missing-conversation', door: 'first-protocol', row: 'i', why: 'Held on the protocol door.' },
  ]);
  provider(fx, 'Second', {
    id: 'second-stand-in', standIn: true, program: { entry: 'agent.mjs' },
    doors: [{ id: 'second-native', kind: 'native', arguments: ['native'] }],
  }, []);
  provider(fx, 'Real', { id: 'real-agent', standIn: false, doors: [{ id: 'real-agent', kind: 'native' }] });
  fx.write('Providers/Nobody/notes.txt', 'a folder with no provider.json is no provider\'s\n');
  return join(fx.root, 'Providers');
}

test('the files are read by one layout: every provider in its folders\' order, a stand-in with its pending lines, a folder with no file none', () => {
  const folder = twoProviders('stand-ins-read');
  const read = readProviders(folder);

  assert.deepEqual(read.problems, []);
  assert.deepEqual(read.providers.map((each) => [each.id, each.folder, each.standIn]), [
    ['first-stand-in', 'First', true], ['real-agent', 'Real', false], ['second-stand-in', 'Second', true],
  ]);
  const { standIns } = readStandIns(folder);
  assert.deepEqual(standIns.map((each) => each.id), ['first-stand-in', 'second-stand-in']);
  assert.deepEqual(standIns[0]!.pending.map((line) => [line.case, line.door, line.row]), [
    ['live.limit-cools', EVERY_DOOR, 'l'], ['live.missing-conversation', 'first-protocol', 'i'],
  ]);

  // A door's command: Node, the program where a build carries it beside the driver, the door's own arguments, then the case's.
  const output = join('built', 'output');
  assert.deepEqual(doorCommand(standIns[0]!, standIns[0]!.doors[1]!, output, ['--scenario', 'missing']), [
    'node', join(output, 'Agents', 'Providers', 'First', 'Program', 'agent.mjs'), '--door', 'protocol', '--scenario', 'missing',
  ]);
});

test('the phase plans the scenario contract over every stand-in\'s doors: held by its own file, read elsewhere, or driven', () => {
  const { standIns } = readStandIns(twoProviders('stand-ins-plan'));
  const rows = scenarioPlan(standIns);
  const state = (row: Row) => (row.held ? `held ${row.held.row}` : row.elsewhere ? 'elsewhere' : 'driven');
  const of = (provider: string) => rows.filter((row) => row.provider === provider).map((row) => `${row.case} ${row.door} ${state(row)}`);

  assert.deepEqual(of('first-stand-in'), [
    'live.start first-native driven', 'live.start first-protocol driven',
    'live.turn first-native driven', 'live.turn first-protocol driven',
    'live.resume-own first-native driven', 'live.resume-own first-protocol driven',
    // A missing conversation is the protocol door's alone, and this provider's file holds it there.
    'live.missing-conversation first-protocol held i',
    'live.child-seen-ended first-native driven', 'live.child-seen-ended first-protocol driven',
    'live.sign-in first-native elsewhere', 'live.sign-in first-protocol elsewhere',
    'live.access-and-fatal first-native driven', 'live.access-and-fatal first-protocol driven',
    'live.limit-cools first-native held l', 'live.limit-cools first-protocol held l',
  ]);
  // The second stand-in holds nothing and has no protocol door, so it is planned by the same contract with no line of its own.
  assert.deepEqual(of('second-stand-in'), SCENARIO_CONTRACT
    .filter((each) => each.kinds.includes('native'))
    .map((each) => `${each.case} second-native ${each.elsewhere ? 'elsewhere' : 'driven'}`));
  assert.equal(rows.some((row) => row.provider === 'real-agent'), false, 'a provider that runs a model is no stand-in');
});

// The layout's breaks, row for row the driver's `ProviderFilesTests.The_reader_refuses_a_file_out_of_the_layout` (its twin, the
// driver's tests' `ProviderFiles.cs`): a change to one table is a change to the other.
test('a file out of the layout is refused, naming the field, and its provider is never planned', () => {
  const fx = makeFixture('stand-ins-refused');
  const program = { entry: 'agent.mjs' };
  provider(fx, 'Extra', { id: 'a', standIn: true, program, doors: [{ id: 'a', kind: 'native', arguments: ['n'] }], extra: 1 });
  provider(fx, 'NoId', { standIn: false, doors: [{ id: 'b', kind: 'native' }] });
  provider(fx, 'Pipe', { id: 'c', standIn: false, doors: [{ id: 'c', kind: 'pipe' }] });
  provider(fx, 'NoProgram', { id: 'd', standIn: true, doors: [{ id: 'd', kind: 'native', arguments: ['n'] }] });
  provider(fx, 'Unchosen', { id: 'e', standIn: true, program, doors: [{ id: 'e', kind: 'native' }] });
  provider(fx, 'Same', {
    id: 'f', standIn: true, program,
    doors: [{ id: 'f', kind: 'native', arguments: ['n'] }, { id: 'g', kind: 'protocol', arguments: ['n'] }],
  });
  provider(fx, 'Outside', { id: 'h', standIn: true, program: { entry: '../a.mjs' }, doors: [{ id: 'h', kind: 'native', arguments: ['n'] }] });
  provider(fx, 'NoDoors', { id: 'i', standIn: false, doors: [] });
  provider(fx, 'Broken', '{ not json');
  const read = readProviders(join(fx.root, 'Providers'));

  assert.deepEqual(read.providers, []);
  for (const says of [
    "Extra/provider.json: 'extra' is no field of the layout",
    "NoId/provider.json: 'id' is missing",
    "Pipe/provider.json, door 1: 'kind' is one of native, protocol",
    "NoProgram/provider.json: a stand-in states its 'program'",
    "Unchosen/provider.json, door 1: a stand-in's door states the 'arguments' that choose it",
    'Same/provider.json: two doors state the same arguments',
    "Outside/provider.json: 'program.entry' is a path under the folder's Program/, never outside it",
    "NoDoors/provider.json: 'doors' is an array",
    'Broken/provider.json: not JSON',
  ]) {
    assert.ok(read.problems.some((problem) => problem.startsWith(says)), `${says}\n${read.problems.join('\n')}`);
  }
});

test('the repository\'s own stand-ins keep the layout, each door\'s kind one of the two, each program in its folder', () => {
  const { standIns, problems } = readStandIns(providersFolder(root));

  assert.deepEqual(problems, []);
  assert.ok(standIns.length > 0, 'the files declare no stand-in');
  for (const standIn of standIns) {
    assert.ok(existsSync(programOf(standIn, join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver'))), `${standIn.id}: no program`);
    for (const door of standIn.doors) assert.ok(KINDS.includes(door.kind), `${standIn.id}: ${door.id} is ${door.kind}`);
  }
});

type Move = { quest: string | undefined; action: string };

/** The service's quest door as a stand-in: every respond it is sent, answered as the service answers a move it made. */
async function questDoor(): Promise<{ url: string; moves: Move[]; close: () => Promise<void> }> {
  const moves: Move[] = [];
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => { text += chunk; });
    request.on('end', () => {
      const match = /^\/api\/quests\/([^/]+)\/respond$/.exec(request.url ?? '');
      if (request.method !== 'POST' || !match) {
        response.writeHead(404).end('{}');
        return;
      }
      moves.push({ quest: match[1], action: (JSON.parse(text) as { action: string }).action });
      response.writeHead(200, { 'content-type': 'application/json' }).end('{}');
    });
  });
  return { url: await listenFetchable(server), moves, close: () => new Promise<void>((resolve) => server.close(() => resolve())) };
}

/** A door's program run from the driver's source with the environment a start or a conversation hands it, bounded by the shared patience. */
function runDoor(standIn: StandIn, door: Door, env: Record<string, string>, more: string[] = []) {
  const [, ...rest] = doorCommand(standIn, door, join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver'), more);
  const fx = makeFixture(`stand-ins-run-${door.id}`);
  const child = spawn(process.execPath, rest, { cwd: fx.root, env: { ...unroled(), ...env }, stdio: ['pipe', 'pipe', 'pipe'] });
  let out = '';
  let err = '';
  child.stdout.on('data', (chunk) => { out += chunk; });
  child.stderr.on('data', (chunk) => { err += chunk; });
  const closed = new Promise<number | null>((resolve, reject) => {
    const timer = setTimeout(() => {
      child.kill();
      reject(new Error(`${door.id} did not end within ${PATIENCE} ms\n${out}\n${err}`));
    }, PATIENCE);
    child.on('close', (code) => { clearTimeout(timer); resolve(code); });
  });
  return { child, closed, out: () => out, err: () => err };
}

test('each stand-in\'s program answers a start on each door, taking and closing its quest through the service\'s door', async () => {
  const { standIns } = readStandIns(providersFolder(root));
  for (const standIn of standIns) {
    for (const door of standIn.doors) {
      const service = await questDoor();
      try {
        const env = { DAORIS_QUEST_ID: 'q-1', DAORIS_SERVICE_URL: service.url, DAORIS_REPOSITORY: 'engine', DAORIS_TARGET: 'Answer the quest.' };
        const ran = runDoor(standIn, door, env);
        if (door.kind === 'protocol') {
          const answers = new Map<number, { result?: { stopReason?: string; sessionId?: string } }>();
          const said: string[] = [];
          let answered = () => {};
          createInterface({ input: ran.child.stdout }).on('line', (line) => {
            const frame = JSON.parse(line) as { id?: number; method?: string; result?: { stopReason?: string }; params?: { update?: { content?: { text?: string } } } };
            if (frame.method === 'session/update') said.push(frame.params?.update?.content?.text ?? '');
            else if (frame.id !== undefined) answers.set(frame.id, frame);
            if (answers.has(3)) answered();
          });
          const prompted = new Promise<void>((resolve) => { answered = resolve; });
          const send = (frame: object) => ran.child.stdin.write(`${JSON.stringify(frame)}\n`);
          send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1 } });
          send({ jsonrpc: '2.0', id: 2, method: 'session/new', params: { cwd: '.', mcpServers: [] } });
          send({ jsonrpc: '2.0', id: 3, method: 'session/prompt', params: { sessionId: 's-1', prompt: [{ type: 'text', text: 'Answer the quest.' }] } });
          await Promise.race([prompted, ran.closed]);
          ran.child.stdin.end();
          assert.equal(await ran.closed, 0, ran.err());
          assert.equal(answers.get(3)?.result?.stopReason, 'end_turn', ran.err());
          assert.ok(said.includes('heard: Answer the quest.'), said.join('\n'));
        } else {
          assert.equal(await ran.closed, 0, ran.err());
          assert.match(ran.out(), /started on engine with a target of words/);
        }
        assert.deepEqual(service.moves, [{ quest: 'q-1', action: 'take' }, { quest: 'q-1', action: 'done' }], `${door.id}\n${ran.out()}\n${ran.err()}`);
      } finally {
        await service.close();
      }
    }
  }
});

test('each stand-in\'s native door says back each line a conversation hands it, and ends when its input closes', async () => {
  const { standIns } = readStandIns(providersFolder(root));
  for (const door of standIns.flatMap((standIn) => standIn.doors.filter((each) => each.kind === 'native').map((each) => ({ standIn, door: each })))) {
    const ran = runDoor(door.standIn, door.door, { DAORIS_REPOSITORY: 'engine' });
    ran.child.stdin.end('first words\nsecond words\n');
    assert.equal(await ran.closed, 0, ran.err());
    assert.match(ran.out(), /heard: first words[\s\S]*heard: second words/);
  }
});
