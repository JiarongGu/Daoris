import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { parse, runCase, catalogueText, writeReceipt } from './real-case.mjs';

const RUN = (states) => ['engine · `hosted` v1 workflow · quest #q1 · ask #a1', ...[['●', 'The work', states[0]], ['○', 'Your look', states[1]], ['○', 'The landing', states[2]]].map(([m, n, s]) => `  ${m} ${n.padEnd(14)}  ${s}`), ''].join('\n');

/** A stand-in installed command that replays one scripted stage per quest-list read, recording every call it gets. */
function standIn(script, { accept = true, create = { code: 0, stdout: 'repository create: `rcase` in `games` - git, for: x\nrepository create: made.\n  commit abc123 in folder\n  quest  #su1 for `rcase`\n' }, setUp = 'Done' } = {}) {
  const calls = [];
  let tick = 0;
  let asked = false;
  const fn = (args) => {
    calls.push(args);
    const stage = script[Math.min(tick, script.length - 1)];
    if (args[0] === 'repository') return { stderr: '', ...create };
    if (args[0] === 'ask') { asked = true; return { code: 0, stdout: 'Asked.\n  ask    #a1\n  quest  #q1\n', stderr: '' }; }
    if (args[0] === 'quest' && args[1] === 'list' && !asked) return { code: 0, stdout: JSON.stringify([{ id: 'su1', status: setUp }]), stderr: '' };
    if (args[0] === 'quest' && args[1] === 'list') return { code: 0, stdout: JSON.stringify([stage.quest]), stderr: '' };
    if (args[0] === 'session') return { code: 0, stdout: JSON.stringify(stage.sessions), stderr: '' };
    if (args[0] === 'workflow' && args[1] === 'run') { tick += 1; return { code: 0, stdout: stage.run + (stage.waits && accept ? '  accept with `daoris workflow accept r1 look`\n' : ''), stderr: '' }; }
    if (args[0] === 'workflow' && args[1] === 'accept') return { code: 0, stdout: 'accepted', stderr: '' };
    return { code: 0, stdout: '', stderr: '' };
  };
  fn.calls = calls;
  return fn;
}

const q = (extra = {}) => ({ id: 'q1', from: 'ask #a1', to: 'rcase', status: 'Open', ...extra });
const s = (state) => [{ id: 's1', quest: 'q1', state }];
const clockFrom = () => { let t = Date.parse('2026-11-14T10:00:00Z'); return () => (t += 1000); };
const base = { workspaceDir: 'unused', home: 'unused', notes: [] };
const harness = (installed) => ({ installed, sleep: async () => {}, clock: clockFrom() });

const toLanded = [
  { quest: q(), sessions: [], run: RUN(['not reached', 'not reached', 'not reached']) },
  { quest: q({ status: 'Taken' }), sessions: s('Working'), run: RUN(['working', 'not reached', 'not reached']) },
  { quest: q({ status: 'Done', submission: { at: 'x' } }), sessions: s('Done'), run: RUN(['reached', 'waiting on you', 'not reached']), waits: true },
  { quest: q({ status: 'Done' }), sessions: s('Done'), run: RUN(['reached', 'reached', 'reached']) },
];

test('case 1 stops at the person\'s Accept and presses nothing', async () => {
  const installed = standIn(toLanded);
  const { receipt, text } = await runCase({ ...base, case: '1' }, harness(installed));
  assert.equal(receipt.outcome, 'waiting-on-person');
  assert.equal(receipt.result, 'waiting-on-person');
  const verdicts = Object.fromEntries(receipt.expectations.map(e => [e.name, e.verdict]));
  assert.deepEqual(verdicts, { created: 'pass', 'set-up': 'pass', filed: 'pass', taken: 'pass', started: 'pass', done: 'pass', 'review-reached': 'pass', accepted: 'person', landed: 'person' });
  assert.ok(receipt.stages.every(st => st.afterFiledMs >= 0));
  assert.equal(installed.calls.some(c => c[0] === 'workflow' && c[1] === 'accept'), false);
  assert.match(text, /interventions: none/);
});

test('case 1 with the person\'s word presses the run\'s accept and lands with no intervention', async () => {
  const installed = standIn(toLanded);
  const { receipt } = await runCase({ ...base, case: '1', withWord: true }, harness(installed));
  assert.equal(receipt.result, 'passed');
  assert.deepEqual(receipt.words.map(w => w.act), ['daoris workflow accept r1 look']);
  assert.equal(receipt.interventions.length, 0);
});

test('the repository is made by the installed verb, its set-up awaited, then the ask is filed at once; no verb reaches a remote', async () => {
  const installed = standIn(toLanded);
  const { receipt } = await runCase({ ...base, case: '1', withWord: true, workspace: 'games', workspaceDir: 'folder' }, harness(installed));
  const [create] = installed.calls;
  assert.deepEqual([create[0], create[1], create[2]], ['repository', 'create', receipt.repository]);
  assert.deepEqual(create.slice(3), ['--purpose', 'One-repository quest through to landed', '--under', 'folder', '--line', 'main', '--workspace', 'games']);
  const ask = installed.calls.find(c => c[0] === 'ask');
  assert.deepEqual(ask.slice(0, 6), ['ask', '--workspace', 'games', '--to', receipt.repository, '--workflow']);
  assert.ok(installed.calls.indexOf(create) < installed.calls.indexOf(ask));
  assert.deepEqual(receipt.stages.slice(0, 3).map(st => st.stage), ['created', 'set-up', 'filed']);
  const verbs = new Set(installed.calls.map(c => c.slice(0, 2).join(' ')));
  for (const verb of verbs) assert.ok(['repository create', 'ask --workspace', 'quest list', 'session list', 'workflow run', 'workflow accept'].includes(verb), verb);
});

test('a refused creation, a declined set-up and a set-up that never ends fail the case with the command\'s sentence, filing nothing', async () => {
  const refused = standIn(toLanded, { create: { code: 1, stdout: 'repository create: `x` in `games`\nrefused:\n  - the folder is not empty\nrepository create: refused: not done.\n', stderr: '' } });
  const a = await runCase({ ...base, case: '1' }, harness(refused));
  assert.equal(a.receipt.outcome, 'could-not-create');
  assert.equal(a.receipt.result, 'failed');
  assert.match(a.receipt.expectations[0].detail, /repository create: refused: not done/);
  assert.equal(refused.calls.some(c => c[0] === 'ask'), false);
  const declined = await runCase({ ...base, case: '1' }, harness(standIn(toLanded, { setUp: 'Declined' })));
  assert.equal(declined.receipt.outcome, 'set-up-declined');
  assert.equal(declined.receipt.stages.map(st => st.stage).join(), 'created');
  const idle = await runCase({ ...base, case: '1', timeoutMin: '0.01' }, harness(standIn(toLanded, { setUp: 'Taken' })));
  assert.equal(idle.receipt.outcome, 'set-up-timeout');
});

test('case 2 files its requirement and judges the answer against it', async () => {
  const requirement = { quote: 'the notes file ends with the word done' };
  const script = [
    { quest: q({ requirements: [requirement] }), sessions: [], run: RUN(['not reached', 'not reached', 'not reached']) },
    { quest: q({ status: 'Taken', requirements: [requirement] }), sessions: s('Working'), run: RUN(['working', 'not reached', 'not reached']) },
    { quest: q({ status: 'Done', requirements: [requirement], answers: [{ requirement: 1, met: 'ends with done' }] }), sessions: s('Done'), run: RUN(['reached', 'waiting on you', 'not reached']), waits: true },
    { quest: q({ status: 'Done', requirements: [requirement], answers: [{ requirement: 1, met: 'ends with done' }] }), sessions: s('Done'), run: RUN(['reached', 'reached', 'reached']) },
  ];
  const installed = standIn(script);
  const { receipt } = await runCase({ ...base, case: '2', withWord: true }, harness(installed));
  const filing = installed.calls.find(c => c[0] === 'ask');
  assert.deepEqual(filing.slice(filing.indexOf('--require'), filing.indexOf('--require') + 3), ['--require', 'the notes file ends with the word done', 'read notes.txt: its last word is done']);
  assert.equal(receipt.result, 'passed');
  assert.equal(receipt.expectations.find(e => e.name === 'requirement-answered').verdict, 'pass');
});

test('a done that departed from its requirement fails the case', async () => {
  const requirement = { quote: 'x' };
  const stage = { quest: q({ status: 'Done', requirements: [requirement], answers: [{ requirement: 1, departed: 'no file written' }] }), sessions: s('Done'), run: RUN(['reached', 'reached', 'reached']) };
  const { receipt } = await runCase({ ...base, case: '2' }, harness(standIn([stage])));
  assert.equal(receipt.result, 'failed');
  assert.match(receipt.expectations.find(e => e.name === 'requirement-answered').detail, /no file written/);
});

test('a failed session is retried once and recorded as an intervention, a defect', async () => {
  const installed = standIn([
    { quest: q({ status: 'Taken' }), sessions: s('Failed'), run: RUN(['working', 'not reached', 'not reached']) },
    ...toLanded.slice(1),
  ]);
  const { receipt, text } = await runCase({ ...base, case: '1', withWord: true }, harness(installed));
  assert.equal(receipt.interventions.length, 1);
  assert.equal(receipt.interventions[0].act, 'daoris quest retry q1');
  assert.equal(receipt.result, 'passed-with-interventions');
  assert.match(text, /quest retry q1/);
});

test('work that never moves times out as failed, and a refused ask is not followed', async () => {
  const idle = { quest: q(), sessions: [], run: RUN(['not reached', 'not reached', 'not reached']) };
  const timed = await runCase({ ...base, case: '1', timeoutMin: '0.01' }, harness(standIn([idle])));
  assert.equal(timed.receipt.outcome, 'timeout');
  assert.equal(timed.receipt.result, 'failed');
  const live = standIn(toLanded);
  const refusing = (args) => args[0] === 'ask' ? { code: 1, stdout: '', stderr: 'no such repository' } : live(args);
  const refused = await runCase({ ...base, case: '1' }, harness(refusing));
  assert.equal(refused.receipt.outcome, 'could-not-file');
  assert.equal(refused.receipt.result, 'failed');
});

test('a listed case that is not built says what it needs', async () => {
  await assert.rejects(runCase({ ...base, case: '5' }, harness(standIn(toLanded))), /listed, not built.*send-back/s);
  assert.match(catalogueText(), /12 {2}needs/);
});

test('arguments: the case, flags with values, repeated interventions notes, and nothing unknown', () => {
  const o = parse(['2', '--home', 'h', '--with-word', '--note-intervention', 'a', '--note-intervention', 'b', '--poll-sec', '1']);
  assert.deepEqual([o.case, o.home, o.withWord, o.notes, o.pollSec], ['2', 'h', true, ['a', 'b'], '1']);
  assert.throws(() => parse(['1', '--push']), /unknown argument/);
  assert.throws(() => parse(['1', '--home']), /needs a value/);
});

test('the receipt is written in two forms', async () => {
  const root = join('_fixtures', 'real-case-fixture');
  rmSync(root, { recursive: true, force: true });
  const done = await runCase({ ...base, case: '1' }, harness(standIn(toLanded)));
  const written = writeReceipt(done, join(root, 'receipts'));
  assert.equal(JSON.parse(readFileSync(`${written}.json`, 'utf8')).case, 1);
  assert.match(readFileSync(`${written}.txt`, 'utf8'), /Real case 1/);
  rmSync(root, { recursive: true, force: true });
});
