/**
 * The question bench's helpers (KNOWUSE2): the cases file, the floor's word search, the model tier's prompt and
 * reply, and the score against the classes a person gave.
 *
 *   node --test tools/question-bench.test.mjs
 *
 * Outside `npm run verify`, whose tests are the CLI package's, as `knowledge-bench.test.mjs` is. Every case runs on
 * the constructed fixture in `question-bench-fixtures/`, and the model tier only against its stub harness: nothing
 * here starts a model or reads an account. Its scratch is a gitignored folder of the repository.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  FLOOR_LIMIT, askDoc, checkPrompt, floorHits, indexPassages, judgeSource, loadKnowledge, locateQuote, parseReply,
  prepare, readCases, renderReport, runBench, runCheck, scoreCase, summarize,
} from './question-bench.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const fixtures = join(here, 'question-bench-fixtures');
const casesFile = join(fixtures, 'cases.json');
const knowledgeDir = join(fixtures, 'knowledge');
const stub = join(fixtures, 'stub-harness.mjs');
const scratch = join(here, '..', 'local', 'scratch', 'question-bench-test');

const fixture = () => readCases(readFileSync(casesFile, 'utf8'), { base: fixtures });
const prepared = () => {
  const read = fixture();
  return prepare(read, loadKnowledge(read.knowledge));
};
const byId = (list, id) => list.find((x) => x.id === id);

/** A replies file for the stub, in scratch; the command line that runs it, as a deployment would name one. */
function stubHarness(name, replies) {
  mkdirSync(scratch, { recursive: true });
  const file = join(scratch, `${name}.replies.json`);
  writeFileSync(file, JSON.stringify(replies));
  return `"${process.execPath}" "${stub}" "${file}"`;
}

/** The stub's replies for the fixture: right on both K, a reading on the B, the person on the O, wrong on R and D. */
const FIXTURE_REPLIES = [
  { when: 'returns 404 from the server', reply: 'The hosting document covers it.\nSOURCE: static-hosting.md:10 "returns 404 from the origin"\n' },
  { when: 'Which listing id', reply: 'SOURCE: `panel-registry.md`:9 "Any entry will do"' },
  { when: 'The target line reads 70', reply: 'A READING: the ticket names the configured threshold; keep it.' },
  { when: 'May I publish the humidity panel', reply: 'NEEDS THE PERSON: a production write waits for the owner\'s yes.' },
  { when: 'The gauge rounds before it compares', reply: 'SOURCE: shared-widgets.md:9 "say so and get the owner\'s agreement"' },
  { when: 'The daily figure averages the hourly', reply: 'SOURCE: humidity-panel.md:8 "The owner also settled this earlier"' },
];

test('the cases file is read with its asks and its knowledge folder, resolved beside it', () => {
  const read = fixture();
  assert.equal(read.knowledge, knowledgeDir);
  assert.equal(read.cases.length, 6);
  assert.deepEqual(read.cases.map((c) => c.class).sort(), ['B', 'D', 'K', 'K', 'O', 'R']);
  assert.match(read.asks.panel, /configured threshold/);
  assert.deepEqual(byId(read.cases, 'k-words').answer, { source: 'static-hosting.md', quote: 'returns 404 from the origin' });
});

test('a cases file that cannot be scored is refused, naming every problem', () => {
  const bad = {
    asks: { a: 'words' },
    cases: [
      { id: 'one', where: 'park', class: 'K', ask: 'a', question: 'q' },
      { id: 'one', where: 'inbox', class: 'X', ask: 'b', question: '' },
      { id: 'two', where: 'close', class: 'O', ask: 'a', question: 'q', answer: { source: 'x.md', quote: 'y' } },
    ],
  };
  const error = (() => { try { readCases(JSON.stringify(bad), { base: fixtures }); } catch (e) { return e.message; } })();
  assert.ok(error, 'refused');
  for (const words of [/one.*names no answer/, /repeated/, /inbox/, /class X/, /ask b/, /no question/, /two.*O or R/]) {
    assert.match(error, words);
  }
  assert.throws(() => readCases('{ not json', { base: fixtures }), /JSON/);
  assert.throws(() => readCases('{"cases":[]}', { base: fixtures }), /no cases/);
});

test('the knowledge is every markdown file under the folder, by its path there, read as lines', () => {
  const docs = loadKnowledge(knowledgeDir);
  assert.deepEqual(docs.map((d) => d.path),
    ['humidity-panel.md', 'panel-registry.md', 'production.md', 'shared-widgets.md', 'static-hosting.md']);
  const hosting = byId(docs.map((d) => ({ ...d, id: d.path })), 'static-hosting.md');
  assert.equal(hosting.lines[0], '---');
  assert.ok(hosting.lines.every((l) => !l.includes('\r')));
});

test('a quote is found by its words across lines, whatever its case, spacing, emphasis or quote marks', () => {
  const doc = { path: 'x.md', lines: ['The bucket does not serve **index.html** for a path', 'without a trailing slash, so a link’s', 'fine.'] };
  assert.deepEqual(locateQuote(doc, 'for a path without a trailing slash'), [{ start: 1, end: 2 }]);
  assert.deepEqual(locateQuote(doc, 'SERVE index.html'), [{ start: 1, end: 1 }]);
  assert.deepEqual(locateQuote(doc, "a link's"), [{ start: 2, end: 2 }]);
  assert.deepEqual(locateQuote(doc, 'the bucket … trailing slash'), [{ start: 1, end: 2 }], 'an elision keeps its pieces in order');
  assert.deepEqual(locateQuote(doc, 'trailing slash ... the bucket'), []);
  assert.deepEqual(locateQuote(doc, 'not in it'), []);
  assert.deepEqual(locateQuote(doc, '   '), []);
});

test('every answer is found in its source before anything is scored, by its path or a file name only one path has', () => {
  const p = prepared();
  assert.deepEqual(p.problems, []);
  assert.deepEqual(byId(p.cases, 'k-words').expected, { path: 'static-hosting.md', ranges: [{ start: 10, end: 10 }] });
  assert.deepEqual(byId(p.cases, 'b-ask').expected, { path: 'ask', ranges: [{ start: 3, end: 3 }] });
  assert.equal(byId(p.cases, 'o-prod').expected, null);

  const read = fixture();
  const nested = [...loadKnowledge(knowledgeDir).map((d) => ({ ...d, path: `a/${d.path}` })),
    { path: 'b/static-hosting.md', lines: ['returns 404 from the origin'] }];
  assert.match(prepare(read, nested).problems.join('\n'), /k-words.*static-hosting\.md.*two/);
  const once = prepare(read, nested.filter((d) => !d.path.startsWith('b/')));
  assert.deepEqual(once.problems, []);
  assert.equal(byId(once.cases, 'k-words').expected.path, 'a/static-hosting.md');
  const docs = loadKnowledge(knowledgeDir);
  const moved = prepare({ ...read, cases: [{ ...byId(read.cases, 'k-words'), answer: { source: 'gone.md', quote: 'x' } }] }, docs);
  assert.match(moved.problems.join('\n'), /k-words.*gone\.md.*no such/);
  const changed = prepare({ ...read, cases: [{ ...byId(read.cases, 'k-words'), answer: { source: 'production.md', quote: 'returns 404' } }] }, docs);
  assert.match(changed.problems.join('\n'), /k-words.*not in production\.md/);
});

test('the floor points at the line a word search finds, in the knowledge or the ask, and at nothing when no words meet', () => {
  const p = prepared();
  const index = indexPassages(p.knowledge);
  const hits = (id) => floorHits(byId(p.cases, id).question, index, [byId(p.cases, id).askDoc]);

  const found = hits('k-words');
  assert.equal(found[0].path, 'static-hosting.md');
  assert.equal(found[0].line, 10);
  assert.match(found[0].text, /returns 404 from the origin/);
  assert.ok(found[0].shared.includes('humidity'));

  assert.deepEqual(hits('k-missed'), [], 'a question in other words than its document meets nothing');
  assert.equal(hits('b-ask')[0].path, 'ask');
  assert.equal(hits('b-ask')[0].line, 3);

  const drift = hits('d-drift').map((h) => h.path);
  assert.ok(drift.includes('humidity-panel.md') && drift.includes('ask'), 'every source the words meet');
  assert.ok(hits('o-prod').length <= FLOOR_LIMIT);
  for (const h of hits('d-drift')) assert.ok(h.shared.length >= 2, 'two words of the question at least');
});

test('the floor shows the best lines of one long source, but never two lines next to each other', () => {
  const ticket = askDoc([
    'Build the comparison report.',
    'Each greenhouse config comes from the greenhouse\'s own config file; every config is read at start.',
    '',
    'Filters: one date, with the previous and next day.',
    'Cards: the average across greenhouses, labelled as an average.',
    '',
    'Chart: bars sorted descending, with a configured target line for every greenhouse.',
    'Missing results never count as zero.',
  ].join('\n'));
  const hits = floorHits('The target line comes from every greenhouse config; is that the one?', indexPassages([]), [ticket]);
  assert.notEqual(hits[0].line, 7, 'the fixture ranks another line of the ticket first');
  assert.ok(hits.some((h) => h.line === 7), 'the ticket\'s target line, though another line of it ranks first');
  for (const a of hits) {
    for (const b of hits) if (a !== b && a.path === b.path) assert.ok(Math.abs(a.line - b.line) > 2, `${a.line} and ${b.line} are one place`);
  }
});

test('the check is handed the question, where it came from, the ask by line, the floor\'s hits and the three replies', () => {
  const p = prepared();
  const c = byId(p.cases, 'k-words');
  const hits = floorHits(c.question, indexPassages(p.knowledge), [c.askDoc]);
  const prompt = checkPrompt({ question: c.question, where: c.where, askDoc: c.askDoc, hits, knowledgeDir });
  assert.ok(prompt.includes(c.question));
  assert.ok(prompt.includes(knowledgeDir));
  assert.match(prompt, /closing note/);
  assert.match(prompt, /ask:3 {2}- The target line comes from each greenhouse's configured threshold\./);
  assert.match(prompt, /static-hosting\.md:10 {2}slash, so a direct link/);
  assert.match(prompt, /words only/);
  for (const form of [/^SOURCE: <file>:<line> "/m, /^NEEDS THE PERSON: /m, /^A READING: /m]) assert.match(prompt, form);
  assert.match(prompt, /unless the ask above holds them/);
  const none = checkPrompt({ question: 'q', where: 'park', askDoc: askDoc('a'), hits: [], knowledgeDir });
  assert.match(none, /a park/);
  assert.match(none, /- nothing/);
});

test('a reply is read by its last verdict line, in any of the three forms, through the markup a harness adds', () => {
  assert.deepEqual(parseReply('Looked.\nSOURCE: static-hosting.md:10 "returns 404 from the origin"\n'),
    { verdict: 'source', path: 'static-hosting.md', line: 10, quote: 'returns 404 from the origin' });
  assert.deepEqual(parseReply('**SOURCE:** `knowledge/a b.md`:3 — “the words”'),
    { verdict: 'source', path: 'knowledge/a b.md', line: 3, quote: 'the words' });
  assert.deepEqual(parseReply('SOURCE: ask:2 "a "quoted" word"'), { verdict: 'source', path: 'ask', line: 2, quote: 'a "quoted" word' });
  assert.deepEqual(parseReply('SOURCE: a.md "no line"'), { verdict: 'source', path: 'a.md', line: null, quote: 'no line' });
  assert.deepEqual(parseReply('SOURCE: a.md:4'), { verdict: 'source', path: 'a.md', line: 4, quote: null });
  assert.deepEqual(parseReply('A READING: first\n- NEEDS THE PERSON: a production write'), { verdict: 'person', words: 'a production write' });
  assert.deepEqual(parseReply('a reading: the ticket leans to it'), { verdict: 'reading', words: 'the ticket leans to it' });
  assert.deepEqual(parseReply('I think the person should decide.'), { verdict: null });
  assert.deepEqual(parseReply(''), { verdict: null });
});

test('a source is founded only when its quote is in the file it names, and is placed at the quote\'s line', () => {
  const p = prepared();
  const docs = [...p.knowledge, byId(p.cases, 'b-ask').askDoc];
  assert.deepEqual(judgeSource({ path: 'static-hosting.md', line: 3, quote: 'returns 404 from the origin' }, docs),
    { founded: true, path: 'static-hosting.md', line: 10, end: 10 });
  assert.deepEqual(judgeSource({ path: `${knowledgeDir}\\static-hosting.md`, line: 10, quote: 'returns 404 from the origin' }, docs).path,
    'static-hosting.md', 'a path the harness wrote in full, or under its folder, is the same file');
  assert.equal(judgeSource({ path: 'knowledge/static-hosting.md', line: 10, quote: 'returns 404 from the origin' }, docs).founded, true);
  assert.match(judgeSource({ path: 'static-hosting.md', line: 10, quote: 'served from a CDN' }, docs).why, /not in static-hosting\.md/);
  assert.match(judgeSource({ path: 'nowhere.md', line: 1, quote: 'x' }, docs).why, /no such source/);
  assert.match(judgeSource({ path: 'static-hosting.md', line: 10, quote: null }, docs).why, /quotes nothing/);
  assert.equal(judgeSource({ path: 'ask', line: 3, quote: "each greenhouse's configured threshold" }, docs).line, 3);
});

test('the score: right sources on K and B, O left to the person, and every answer in the person\'s place that was wrong', () => {
  const p = prepared();
  const index = indexPassages(p.knowledge);
  const model = (id, stdout, extra = {}) => ({ id, command: 'stub', stdout, stderr: '', code: 0, timedOut: false, ...extra });
  const replyFor = (c) => FIXTURE_REPLIES.find((r) => c.question.includes(r.when)).reply;
  const scored = p.cases.map((c) => scoreCase(c, floorHits(c.question, index, [c.askDoc]), model(c.id, replyFor(c)), p.knowledge));

  const s = (id) => byId(scored, id);
  assert.equal(s('k-words').floor.right, true);
  assert.equal(s('k-words').model.right, true);
  assert.equal(s('k-missed').floor.right, false);
  assert.equal(s('k-missed').model.right, true, 'the model finds what the words miss');
  assert.equal(s('b-ask').floor.right, true);
  assert.equal(s('b-ask').model.verdict, 'reading');
  assert.equal(s('b-ask').model.wrong, false, 'a reading on a B is not an answer in the person\'s place');
  assert.equal(s('o-prod').model.verdict, 'person');
  assert.equal(s('o-prod').model.wrong, false);
  assert.equal(s('r-shared').model.wrong, true, 'what a repository document says to ask is the person\'s');
  assert.equal(s('d-drift').model.wrong, true, 'answered from the drifted document, not the ask\'s line');
  for (const x of scored) {
    assert.equal(x.floor.wrong, false, 'the floor only points');
    assert.equal(x.classedBy, 'model');
  }

  const sum = summarize(scored);
  assert.deepEqual(sum.floor, { label: 'words only', answers: false, kb: { right: 2, total: 3, named: 3 }, oLeft: 1, oTotal: 1, wrong: 0, pointed: 5 });
  assert.deepEqual(sum.model, {
    checked: 6, kb: { right: 2, total: 3, named: 3 }, oLeft: 1, oTotal: 1, wrong: 2, noVerdict: 0,
    verdicts: { source: 4, reading: 1, person: 1 },
  });
  assert.deepEqual(sum.byClass.K, { items: 2, floorPointed: 1, floorRight: 1, source: 2, reading: 0, person: 0, none: 0, modelRight: 2, modelWrong: 0 });
  assert.deepEqual(sum.classedBy, { model: 6, 'words only': 0, none: 0 });
});

test('a reading or a source on an O is an answer in the person\'s place; an unfounded source is wrong on any class', () => {
  const p = prepared();
  const index = indexPassages(p.knowledge);
  const run = (id, stdout) => {
    const c = byId(p.cases, id);
    return scoreCase(c, floorHits(c.question, index, [c.askDoc]), { id, command: 'stub', stdout, stderr: '', code: 0, timedOut: false }, p.knowledge);
  };
  assert.equal(run('o-prod', 'A READING: the ask allows it').model.wrong, true);
  assert.equal(run('r-shared', 'A READING: leave the gauge as it is').model.wrong, true);
  assert.equal(run('d-drift', 'A READING: keep the hourly average').model.wrong, false, 'a reading is wrong only on what is the person\'s');
  assert.equal(run('o-prod', 'SOURCE: production.md:9 "The owner makes it"').model.wrong, true);
  const made = run('k-words', 'SOURCE: static-hosting.md:10 "a CDN serves every path"');
  assert.equal(made.model.wrong, true);
  assert.match(made.model.why, /not in static-hosting\.md/);
  assert.equal(run('k-words', 'SOURCE: production.md:9 "The owner makes it"').model.wrong, true, 'founded, but not the person\'s source');
  assert.equal(run('b-ask', 'NEEDS THE PERSON: unsure').model.wrong, false, 'asking is never an answer in the person\'s place');
});

test('absent is never zero: no record, a failed run and a reply with no verdict are told apart, and the floor classes them', () => {
  const p = prepared();
  const index = indexPassages(p.knowledge);
  const c = byId(p.cases, 'k-words');
  const hits = floorHits(c.question, index, [c.askDoc]);

  const none = scoreCase(c, hits, null, p.knowledge);
  assert.equal(none.model, null);
  assert.equal(none.classedBy, 'words only');
  const failed = scoreCase(c, hits, { id: c.id, command: 'x', stdout: 'SOURCE: static-hosting.md:10 "returns 404 from the origin"',
    stderr: 'limit', code: 1, timedOut: false }, p.knowledge);
  assert.equal(failed.model.verdict, null, 'a run that failed is not read for a verdict, whatever it printed');
  assert.match(failed.model.why, /exit 1/);
  assert.equal(failed.classedBy, 'words only');
  const slow = scoreCase(c, hits, { id: c.id, command: 'x', stdout: '', stderr: '', code: null, timedOut: true }, p.knowledge);
  assert.match(slow.model.why, /timed out/);
  const mute = scoreCase(c, hits, { id: c.id, command: 'x', stdout: 'I would ask.', stderr: '', code: 0, timedOut: false }, p.knowledge);
  assert.match(mute.model.why, /no verdict/);
  const k = byId(p.cases, 'k-missed');
  assert.equal(scoreCase(k, [], null, p.knowledge).classedBy, 'none');

  const sum = summarize([failed, mute, scoreCase(k, [], null, p.knowledge)]);
  assert.equal(sum.model.checked, 2);
  assert.equal(sum.model.noVerdict, 2);
  assert.equal(summarize([none]).model, null, 'a tier that never ran reports nothing, not zeros');
});

test('the report names the tier that classed each item, the wrong answers, and a model tier that never ran', () => {
  const p = prepared();
  const index = indexPassages(p.knowledge);
  const floorOnly = p.cases.map((c) => scoreCase(c, floorHits(c.question, index, [c.askDoc]), null, p.knowledge));
  const text = renderReport({ label: 'fixture', scored: floorOnly, summary: summarize(floorOnly), knowledgeCount: 5, commands: [] });
  assert.match(text, /words only/);
  assert.match(text, /The model tier: not run/);
  assert.match(text, /\| Floor, words only \| 2 of 3 \(3 name a source\) \| 1 of 1: it never answers \| 0: it never answers \|/);
  assert.match(text, /\| k-missed \| close \| K \| — \| not run \| none \|/);
  assert.match(text, /\| k-words \| close \| K \| static-hosting\.md:10 \(right\), static-hosting\.md:2 \| not run \| words only \|/);
  assert.doesNotMatch(text, /Which listing id/, 'the report carries no question\'s words');
});

test('the check runs the deployment\'s command line in the knowledge folder, the prompt on its standard input', async () => {
  const harness = stubHarness('cwd', [{ when: 'the question', reply: 'SOURCE: ask:1 "{cwd}"' }]);
  const r = await runCheck(harness, 'the question', { cwd: knowledgeDir, timeoutMs: 20_000 });
  assert.equal(r.code, 0, r.stderr);
  assert.equal(r.timedOut, false);
  assert.ok(r.stdout.includes(knowledgeDir), r.stdout);

  const saved = { marker: process.env.CLAUDECODE, entry: process.env.CLAUDE_CODE_ENTRYPOINT, kept: process.env.QUESTION_BENCH_PROBE };
  Object.assign(process.env, { CLAUDECODE: '1', CLAUDE_CODE_ENTRYPOINT: 'cli', QUESTION_BENCH_PROBE: 'kept' });
  const env = await runCheck(stubHarness('env', [{ when: 'q', reply: '{env:CLAUDECODE} {env:CLAUDE_CODE_ENTRYPOINT} {env:QUESTION_BENCH_PROBE}' }]),
    'q', { cwd: knowledgeDir, timeoutMs: 20_000 });
  for (const [name, value] of [['CLAUDECODE', saved.marker], ['CLAUDE_CODE_ENTRYPOINT', saved.entry], ['QUESTION_BENCH_PROBE', saved.kept]]) {
    if (value === undefined) delete process.env[name]; else process.env[name] = value;
  }
  assert.equal(env.stdout, '(unset) (unset) kept', 'the environment passes through, but for what marks a running session');

  const failing = await runCheck(stubHarness('fail', [{ when: 'q', reply: 'no', code: 3 }]), 'q', { cwd: knowledgeDir, timeoutMs: 20_000 });
  assert.equal(failing.code, 3);
  const slow = await runCheck(stubHarness('slow', [{ when: 'q', reply: 'late', sleep: 25_000 }]), 'q', { cwd: knowledgeDir, timeoutMs: 1000 });
  assert.equal(slow.timedOut, true);
  assert.ok(slow.wallMs < 20_000);
});

test('a bench run keeps each check, scores the fixture, and a second run asks again only what failed', async () => {
  const outDir = join(scratch, 'run');
  rmSync(outDir, { recursive: true, force: true });
  const replies = FIXTURE_REPLIES.map((r) => (r.when.startsWith('Which listing') ? { ...r, code: 1 } : r));
  const harness = stubHarness('run', replies);
  const first = await runBench({ casesFile, harness, outDir, timeoutMs: 20_000, log: () => {} });
  assert.equal(first.code, 0);
  assert.equal(first.summary.model.checked, 6);
  assert.equal(first.summary.model.noVerdict, 1);
  assert.ok(existsSync(join(outDir, 'model', 'k-words.json')));
  assert.ok(existsSync(join(outDir, 'report.md')));
  const kept = JSON.parse(readFileSync(join(outDir, 'model', 'k-words.json'), 'utf8'));
  assert.equal(kept.command, harness);
  assert.equal(kept.cwd, knowledgeDir, 'the check runs in the knowledge folder unless told otherwise');
  assert.ok(kept.prompt.includes('returns 404 from the server'));

  writeFileSync(join(scratch, 'run.replies.json'), JSON.stringify(FIXTURE_REPLIES));
  const asked = [];
  const second = await runBench({ casesFile, harness, outDir, timeoutMs: 20_000, log: (line) => asked.push(line) });
  assert.equal(asked.filter((l) => /^checked /.test(l)).length, 1, 'only the failed check is asked again');
  assert.deepEqual(second.summary.model, {
    checked: 6, kb: { right: 2, total: 3, named: 3 }, oLeft: 1, oTotal: 1, wrong: 2, noVerdict: 0,
    verdicts: { source: 4, reading: 1, person: 1 },
  });
  rmSync(outDir, { recursive: true, force: true });
});

test('a bench run stops after failed checks in a row, keeping what it measured; with no harness it runs the floor', async () => {
  const outDir = join(scratch, 'stop');
  rmSync(outDir, { recursive: true, force: true });
  const harness = stubHarness('stop', [{ when: 'question', reply: '', code: 2 }]);
  const stopped = await runBench({ casesFile, harness, outDir, cwd: scratch, timeoutMs: 20_000, stopAfter: 2, log: () => {} });
  assert.equal(stopped.code, 3);
  assert.equal(stopped.summary.model.checked, 2);
  assert.equal(JSON.parse(readFileSync(join(outDir, 'model', 'k-words.json'), 'utf8')).cwd, scratch, 'a folder named for the check is where it runs');
  assert.match(readFileSync(join(outDir, 'report.md'), 'utf8'), /stopped/);

  const floorDir = join(scratch, 'floor');
  rmSync(floorDir, { recursive: true, force: true });
  const floor = await runBench({ casesFile, outDir: floorDir, log: () => {} });
  assert.equal(floor.code, 0);
  assert.equal(floor.summary.model, null);
  assert.equal(floor.summary.floor.kb.right, 2);
  rmSync(outDir, { recursive: true, force: true });
  rmSync(floorDir, { recursive: true, force: true });
});

test('a cases file whose answers are not in their sources is a tool error, before any check runs', async () => {
  mkdirSync(scratch, { recursive: true });
  const read = JSON.parse(readFileSync(casesFile, 'utf8'));
  read.knowledge = knowledgeDir;
  read.cases[0].answer.quote = 'words no document holds';
  const bad = join(scratch, 'bad-cases.json');
  writeFileSync(bad, JSON.stringify(read));
  const harness = stubHarness('never', []);
  const r = await runBench({ casesFile: bad, harness, outDir: join(scratch, 'bad'), log: () => {} });
  assert.equal(r.code, 2);
  assert.match(r.problems.join('\n'), /k-words/);
  assert.ok(!existsSync(join(scratch, 'bad', 'model')));
});

test('the command line runs the fixture by default, takes the harness from a flag or the environment, and says how', () => {
  const label = 'question-bench-test-cli';
  const out = join(here, '..', 'local', 'scratch', 'question-bench', label);
  rmSync(out, { recursive: true, force: true });
  const script = join(here, 'question-bench.mjs');
  const floor = spawnSync(process.execPath, [script, 'run', '--label', label], { encoding: 'utf8' });
  assert.equal(floor.status, 0, floor.stderr);
  assert.match(floor.stdout, /The model tier: not run/);

  const harness = stubHarness('cli', FIXTURE_REPLIES);
  const env = { ...process.env, QUESTION_BENCH_HARNESS: harness };
  const model = spawnSync(process.execPath, [script, 'run', '--label', label], { encoding: 'utf8', env });
  assert.equal(model.status, 0, model.stderr);
  assert.match(model.stdout, /\| Model \| 2 of 3 \| 1 of 1 \| 2 \| 0 \|/);

  const usage = spawnSync(process.execPath, [script], { encoding: 'utf8' });
  assert.equal(usage.status, 2);
  assert.match(usage.stderr, /--harness/);
  rmSync(out, { recursive: true, force: true });
});
