import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { DatabaseSync } from 'node:sqlite';
import { parse } from './dev-metrics/options.mjs';
import { MIN_SAMPLES, familyOf, rankFamilies, readOpinions, summarizeFamilies, tokensBySession } from './dev-metrics/families.mjs';

mkdirSync('_fixtures', { recursive: true });
const root = mkdtempSync(join('_fixtures', 'dev-families-'));
after(() => rmSync(root, { recursive: true, force: true }));
const jl = rows => rows.map(r => JSON.stringify(r)).join('\n') + '\n';
const write = (file, body) => { mkdirSync(join(file, '..'), { recursive: true }); writeFileSync(file, body); };

const minute = n => new Date(Date.UTC(2026, 10, 10, 9, n)).toISOString();
const finding = number => ({ number, weight: 'should', where: 'a', claim: 'c', consequence: 'q', reproduce: 'r', sure: 'sure' });

function opinionRow({ id, door, session, pass = 'first', n = 0, findings = 0, handed = true, said = [], given = true, product = null }) {
  const askedAt = new Date(Date.UTC(2026, 10, 10, 9, n)).toISOString();
  const at = new Date(Date.UTC(2026, 10, 10, 9, n + 10)).toISOString();
  return {
    id, session, pass, asked: askedAt,
    asking: JSON.stringify({ candidate: { repository: 'r', base: 'b', tip: 't', commits: [] }, reviewer: { door, label: 'another-maker', ...(product ? { product } : {}) }, families: [], minutes: 30, tier: 'agent' }),
    given: given ? JSON.stringify({ findings: Array.from({ length: findings }, (_, i) => finding(i + 1)), read: 'x', at }) : null,
    answers: said.length ? JSON.stringify(said.map((s, i) => ({ finding: i + 1, said: s, at: minute(n + 20 + i) }))) : null,
    handed: handed ? 'w1' : null,
  };
}

function homeWith(name, rows, turns = []) {
  const home = join(root, name);
  mkdirSync(join(home, 'data'), { recursive: true });
  write(join(home, 'INSTALLED.md'), 'a marker');
  const db = new DatabaseSync(join(home, 'data', 'knowledge.db'));
  db.exec('CREATE TABLE opinions (id TEXT PRIMARY KEY, repository TEXT, working TEXT, session TEXT, occasion TEXT, pass TEXT, rechecks TEXT, asked TEXT, asking TEXT, given TEXT, handed_to TEXT, handed_word TEXT, handed_at TEXT, answers TEXT)');
  const insert = db.prepare('INSERT INTO opinions (id, repository, working, session, occasion, pass, asked, asking, given, handed_to, answers) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)');
  for (const r of rows) insert.run(r.id, 'r', 'w', r.session, 'landing', r.pass, r.asked, r.asking, r.given, r.handed, r.answers);
  db.close();
  if (turns.length) write(join(home, 'data', 'usage-history', 'turns-2026-11.jsonl'), jl(turns));
  return join(home, 'data');
}

test('a door, model or product names its family, and an unknown one names none', () => {
  assert.equal(familyOf('claude-code-acp'), 'claude-code');
  assert.equal(familyOf('claude-opus-9'), 'claude-code');
  assert.equal(familyOf('codex'), 'codex');
  assert.equal(familyOf('gpt-9'), 'codex');
  assert.equal(familyOf('dsh'), 'dsh');
  assert.equal(familyOf('DeepSeek Harness'), 'dsh');
  assert.equal(familyOf('another'), null);
  assert.equal(familyOf(undefined), null);
});

test('the flag is read, and wants no value', () => {
  assert.equal(parse(['--families', '--since', '2026-11-01', '--json']).families, true);
  assert.equal(parse(['--families', '--home', 'x']).home, 'x');
});

test('a pass counts its findings, and the share is of the handed passes\' answers with a missing answer unresolved', () => {
  const home = homeWith('shares', [
    opinionRow({ id: 'o1', door: 'dsh', session: 's1', n: 1, findings: 4, said: ['fixed', 'fixed', 'rejected'] }),
    opinionRow({ id: 'o2', door: 'dsh', session: 's2', n: 2, findings: 2, said: ['fixed', 'fixed'] }),
    opinionRow({ id: 'o3', door: 'dsh', session: 's3', n: 3, findings: 5, handed: false }),
    opinionRow({ id: 'o4', door: 'dsh', session: 's4', n: 4, given: false, handed: false }),
  ]);
  return readOpinions(home).then(({ rows }) => {
    const s = summarizeFamilies({ opinions: rows, landings: [] }).secondOpinion.families.dsh;
    assert.equal(s.passes, 4);
    assert.equal(s.given, 3);
    assert.equal(s.findings, 11);
    assert.equal(s.findingsPerPass, 3.7);
    assert.equal(s.handedPasses, 2);
    assert.deepEqual([s.fixed, s.rejected, s.unresolved], [4, 1, 1]);
    assert.equal(s.fixedShare, 0.667);
    assert.equal(s.medianMinutes, 10);
  });
});

test('the latest answer to a finding stands over an earlier one', async () => {
  const row = opinionRow({ id: 'o1', door: 'codex', session: 's1', findings: 1, said: [] });
  row.answers = JSON.stringify([
    { finding: 1, said: 'unresolved', at: '2026-11-10T09:30:00.000Z' },
    { finding: 1, said: 'fixed', at: '2026-11-10T09:40:00.000Z' },
  ]);
  const { rows } = await readOpinions(homeWith('latest', [row]));
  assert.equal(summarizeFamilies({ opinions: rows, landings: [] }).secondOpinion.families.codex.fixed, 1);
});

test('a reviewer session\'s turns give its pass tokens, and a pass whose turns hold none says so', async () => {
  const turn = (id, session, tokens) => ({ v: 1, kind: 'turn', id, session, ended: '2026-11-10T10:00:00Z', ...tokens });
  const home = homeWith('tokens', [
    opinionRow({ id: 'o1', door: 'claude-code-acp', session: 'sa', findings: 1 }),
    opinionRow({ id: 'o2', door: 'dsh', session: 'sb', n: 1, findings: 1 }),
  ], [
    turn('t1', 'sa', { input: 1, output: 2, cacheRead: 3, cacheWrite: 4 }), turn('t1', 'sa', { input: 1, output: 2, cacheRead: 3, cacheWrite: 4 }),
    turn('t2', 'sa', { input: 10, output: 20, cacheRead: 30, cacheWrite: 40 }), turn('t3', 'sb', { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 }),
  ]);
  const { rows } = await readOpinions(home);
  const tokens = tokensBySession(home, new Set(rows.map(r => r.session)));
  const by = summarizeFamilies({ opinions: rows, tokens, landings: [] }).secondOpinion.families;
  assert.deepEqual(by['claude-code'].tokens, { input: 11, output: 22, cacheRead: 33, cacheWrite: 44 });
  assert.match(by.dsh.tokens, /^not recorded/);
});

test('a home with no database says where the opinions would be, and reads nothing', async () => {
  const read = await readOpinions(join(root, 'nowhere'));
  assert.deepEqual(read.rows, []);
  assert.match(read.missing, /^not recorded/);
});

const landing = (branch, model, firstPass, wall) => ({ branch, builder: model ? { model, wallMinutes: wall, tokens: { input: 1, output: 2, cacheRead: 3, cacheWrite: 4 } } : null, gates: firstPass === null ? null : { passed: true, firstPass, ms: 1 } });

test('work per family: landings, first-pass rate, mean wall time, tokens; a landing with no builder names no family', () => {
  const { work } = summarizeFamilies({
    opinions: [],
    landings: [landing('a', 'claude-x', true, 10), landing('b', 'claude-y', false, 20), landing('c', 'claude-x', true, 30), landing('d', 'gpt-x', true, 5), landing('e', null, true, 0), landing('f', 'claude-x', null, 30)],
  });
  const claude = work.families['claude-code'];
  assert.equal(claude.landings, 4);
  assert.equal(claude.withGates, 3);
  assert.equal(claude.firstPassRate, 0.667);
  assert.equal(claude.meanWallMinutes, 22.5);
  assert.deepEqual(claude.tokens, { input: 4, output: 8, cacheRead: 12, cacheWrite: 16 });
  assert.equal(work.families.codex.landings, 1);
  assert.equal(work.unattributed, 1);
  assert.equal(work.families.dsh.landings, 0);
});

test('families rank only with the samples to, and say plainly where they have too few', () => {
  const stat = (n, share, tie = 0) => ({ n, share, tie });
  const by = { a: stat(MIN_SAMPLES, 0.5), b: stat(MIN_SAMPLES + 3, 0.9), c: stat(MIN_SAMPLES - 1, 1) };
  const options = { samples: s => s.n, score: s => s.share, tie: s => s.tie };
  const ranked = rankFamilies(by, options);
  assert.deepEqual(ranked.ranked.map(r => r.family), ['b', 'a']);
  assert.deepEqual(ranked.unranked, [{ family: 'c', samples: MIN_SAMPLES - 1 }]);
  assert.match(ranked.note, /^c not ranked/);
  const tie = rankFamilies({ a: stat(MIN_SAMPLES, 0.5, 9), b: stat(MIN_SAMPLES, 0.5, 3) }, options);
  assert.deepEqual(tie.ranked.map(r => r.family), ['b', 'a']);
  const few = rankFamilies({ a: stat(MIN_SAMPLES, 0.5), b: stat(1, 0.9) }, options);
  assert.deepEqual(few.ranked, []);
  assert.match(few.note, /^too few samples to rank/);
});

test('the report runs from the command line over a home and a repository, as text and as JSON', async () => {
  const home = homeWith('cli', Array.from({ length: MIN_SAMPLES }, (_, i) => opinionRow({ id: `o${i}`, door: 'dsh', session: `s${i}`, n: i, findings: 2, said: ['fixed', 'rejected'] })));
  const repo = join(root, 'repo');
  write(join(repo, 'local', 'metrics', 'landings.jsonl'), jl([
    { schema: 2, commit: 'c', branch: 'steward/x', at: '2026-11-10T10:00:00Z', builder: null, gates: null },
  ]));
  const run = (...extra) => spawnSync(process.execPath, [resolve('tools/dev-metrics.mjs'), '--families', '--home', home, '--repo', repo, ...extra], { encoding: 'utf8', windowsHide: true });
  const text = run('--since', '2026-11-01');
  assert.equal(text.status, 0, text.stderr);
  assert.match(text.stdout, /dsh +5\/5 passes given/);
  assert.match(text.stdout, /not ranked: too few samples to rank/);
  assert.match(text.stdout, /1 landings name no family/);
  const json = JSON.parse(run('--json').stdout);
  assert.equal(json.secondOpinion.families.dsh.fixedShare, 0.5);
  assert.equal(json.secondOpinion.ranking.ranked.length, 0);
  const outside = JSON.parse(run('--since', '2026-12-01', '--json').stdout);
  assert.equal(outside.secondOpinion.families.dsh.passes, 0);
});
