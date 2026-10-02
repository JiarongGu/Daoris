/**
 * The knowledge bench's helpers (KNOW3): the corpus's frontmatter from the router, each design's fixture,
 * the push design's ranker, the root's isolation, and the scoring of a recorded stream.
 *
 *   node --test tools/knowledge-bench.test.mjs
 *
 * Outside `npm run verify`, whose tests are the CLI package's: this branch's lanes are the tools and the docs,
 * and the CLI suite is another lane's (TEST3 moved `setup-kit`'s tests there for the same reason, in its own
 * branch). Its scratch is a gitignored folder of the repository, and nothing here starts a model.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  DESIGNS, TASKS, PILOT_TASKS, aggregate, alwaysLoaded, assertIsolatedRoot, claudeArgs, docNameOf,
  fixtureFiles, frontmatterFor, hookSource, isolationProblems, limitReached, parseRouterRows, parseStream,
  project, projectIndex, rankBm25, readFrontmatter, renderDocument, scoreRun, targetOf, tokenize,
} from './knowledge-bench.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'knowledge-bench-test');
const fixture = (name) => readFileSync(join(here, 'knowledge-bench-fixtures', name), 'utf8');

const ROUTER = [
  '# The documents',
  '',
  '## Contracts and methods',
  '',
  '| Document | Kind | For | Where it stands |',
  '|---|---|---|---|',
  '| `2026-09-23-plugin-design.md` | contract | plugins (D64), and making one with the kit (§9, D101) | Current, with **D71**: noted. `daoris tool list\\|path` too |',
  '| `2026-09-21-working-surface-components.md` | method | how a screen is built | Current: a story first, and a molecule imports no hook |',
  '',
  '## Studies and evidence',
  '',
  '| Document | Kind | Carried by |',
  '|---|---|---|',
  '| `2026-09-21-dsh-evaluation.md` | evidence | D53 |',
  '',
  '## Records',
  '',
  '| Document | What it holds |',
  '|---|---|',
  '| `DECISIONS.md` | Every decision, numbered, with its reasoning |',
  '| `2026-10-01-naming-audit.md` | NAME1a\'s audit: every label key, its kind |',
].join('\n');

const doc = (name, applies, enforces, title, body) => ({
  name, applies_when: applies, enforces, title, body: `# ${title}\n\n${body}\n`, source: 'docs',
});
const CORPUS = [
  doc('2026-09-23-plugin-design', 'working on plugins', 'the contract for plugins', 'Plugins: a folder that declares',
    'A plugin declares harnesses on the ACP door and servers every session is handed. Never code loaded into a host.'),
  doc('2026-10-01-account-rotation-design', 'working on account rotation', 'the contract for rotation', 'Account rotation',
    'A limit read from the agent\'s own words, a cool-off to the reset it names, the next account of the order.'),
  doc('leak-repair', 'a credential has been committed', 'repair history', 'Leak repair',
    'Scrub blobs, paths and messages together, and prove the scrub with a scan you have seen fail.'),
];

test('the router rows give each document its kind, what it is for and where it stands', () => {
  const rows = parseRouterRows(ROUTER);
  assert.deepEqual(rows.get('2026-09-23-plugin-design.md'), {
    kind: 'contract', about: 'plugins (D64), and making one with the kit (§9, D101)',
    standing: 'Current, with **D71**: noted. `daoris tool list|path` too',
  });
  assert.equal(rows.get('2026-09-21-dsh-evaluation.md').kind, 'evidence');
  assert.equal(rows.get('2026-09-21-dsh-evaluation.md').about, 'D53');
  assert.equal(rows.get('2026-10-01-naming-audit.md').kind, 'record');
  assert.equal(rows.get('DECISIONS.md').about, 'Every decision, numbered, with its reasoning');
});

test('frontmatter is written from the row, plain, with no colon-space, pipe, backtick or emphasis in a value', () => {
  const rows = parseRouterRows(ROUTER);
  const contract = frontmatterFor('2026-09-23-plugin-design', rows.get('2026-09-23-plugin-design.md'));
  assert.equal(contract.name, '2026-09-23-plugin-design');
  assert.match(contract.applies_when, /plugins \(D64\), and making one with the kit/);
  assert.match(contract.enforces, /^the contract for plugins/);
  for (const value of Object.values(contract)) assert.doesNotMatch(value, /: |\||`|\*/);

  const evidence = frontmatterFor('2026-09-21-dsh-evaluation', rows.get('2026-09-21-dsh-evaluation.md'), 'dsh: an evaluation');
  assert.equal(evidence.applies_when, 'revisiting the reasoning behind D53 — dsh — an evaluation');
  assert.match(evidence.enforces, /evidence/);

  const long = frontmatterFor('x', { kind: 'contract', about: 'a', standing: 'word '.repeat(200) });
  assert.ok(Buffer.byteLength(long.enforces) <= 240, 'a long standing is cut on a word');
});

test('a document is rendered with its frontmatter first, and read back the same', () => {
  const text = renderDocument(CORPUS[0]);
  assert.match(text, /^---\nname: 2026-09-23-plugin-design\napplies_when: working on plugins\nenforces: the contract for plugins\n---\n\n# Plugins/);
  const back = readFrontmatter(text);
  assert.equal(back.fields.applies_when, 'working on plugins');
  assert.match(back.body, /^# Plugins/);
  assert.deepEqual(readFrontmatter('# No frontmatter\n').fields, {});
});

test('every design shares the one brief and differs only in its own part', () => {
  const all = Object.keys(DESIGNS).map((id) => [id, fixtureFiles(id, CORPUS)]);
  const brief = all.find(([id]) => id === '0')[1].get('AGENTS.md');
  for (const [id, files] of all) {
    assert.equal(files.get('CLAUDE.md'), '@AGENTS.md\n', id);
    assert.ok(files.get('AGENTS.md').startsWith(brief), `${id} starts with the control's brief`);
  }
});

test('design A puts the index table in AGENTS.md; B points AGENTS.md at a generated INDEX.md', () => {
  const a = fixtureFiles('A', CORPUS);
  assert.match(a.get('AGENTS.md'), /\| \[leak-repair\]\(knowledge\/leak-repair\.md\) \| a credential has been committed \| repair history \|/);
  assert.ok(a.has('knowledge/leak-repair.md'));
  assert.ok(!a.has('INDEX.md'));

  const b = fixtureFiles('B', CORPUS);
  assert.match(b.get('AGENTS.md'), /`INDEX\.md`/);
  assert.doesNotMatch(b.get('AGENTS.md'), /leak-repair/);
  assert.match(b.get('INDEX.md'), /\| `knowledge\/leak-repair\.md` \| a credential has been committed \| repair history \|/);
});

test('design C makes each document a skill described by when it applies; E and 0 have no index at all', () => {
  const c = fixtureFiles('C', CORPUS);
  const skill = c.get('.claude/skills/leak-repair/SKILL.md');
  assert.match(skill, /^---\nname: leak-repair\ndescription: a credential has been committed\n---\n/);
  assert.match(skill, /# Leak repair/);
  assert.ok(![...c.keys()].some((p) => p.startsWith('knowledge/')));

  const e = fixtureFiles('E', CORPUS);
  assert.match(e.get('AGENTS.md'), /`knowledge\/`/);
  assert.doesNotMatch(e.get('AGENTS.md'), /leak-repair/);
  const zero = fixtureFiles('0', CORPUS);
  assert.doesNotMatch(zero.get('AGENTS.md'), /knowledge/);
  assert.ok(zero.has('knowledge/leak-repair.md'));
});

test('design G declares a project prompt hook that runs the ranker beside it', () => {
  const g = fixtureFiles('G', CORPUS);
  const settings = JSON.parse(g.get('.claude/settings.json'));
  assert.equal(settings.hooks.UserPromptSubmit[0].hooks[0].command, 'node .claude/hooks/rank.mjs');
  assert.equal(g.get('.claude/hooks/rank.mjs'), hookSource());
});

test('a reference to another corpus document follows it into the fixture', () => {
  const corpus = [doc('a-doc', 'x', 'y', 'A', 'See `docs/b-doc.md` and `canon/core/knowledge/b-doc.md`, not `docs/DECISIONS.md`.'),
    doc('b-doc', 'x', 'y', 'B', 'b')];
  assert.match(fixtureFiles('E', corpus).get('knowledge/a-doc.md'), /`knowledge\/b-doc\.md` and `knowledge\/b-doc\.md`, not `docs\/DECISIONS\.md`/);
  assert.match(fixtureFiles('C', corpus).get('.claude/skills/a-doc/SKILL.md'), /`\.claude\/skills\/b-doc\/SKILL\.md`/);
});

test('the ranker scores frontmatter and body by BM25, and stems the plural away', () => {
  assert.deepEqual(tokenize('The Plugins declare harnesses!'), ['plugin', 'declare', 'harness']);
  const ranked = rankBm25(CORPUS.map((d) => ({ path: `knowledge/${d.name}.md`, fields: d, body: d.body })),
    'what may a plugin declare about its harness?');
  assert.equal(ranked[0].path, 'knowledge/2026-09-23-plugin-design.md');
  assert.equal(rankBm25([], 'anything').length, 0);
});

test('the generated hook reads the prompt on stdin and prints the top titles and paths', () => {
  const dir = join(scratch, 'hook');
  rmSync(dir, { recursive: true, force: true });
  for (const [path, text] of fixtureFiles('G', CORPUS)) {
    mkdirSync(dirname(join(dir, path)), { recursive: true });
    writeFileSync(join(dir, path), text);
  }
  const out = spawnSync(process.execPath, ['.claude/hooks/rank.mjs'], {
    cwd: dir, input: JSON.stringify({ prompt: 'my access token was committed and pushed: how do I clean it?' }), encoding: 'utf8',
  });
  assert.equal(out.status, 0, out.stderr);
  const lines = out.stdout.trim().split('\n');
  assert.match(lines[0], /local search/);
  assert.match(lines[1], /^1\. Leak repair \(knowledge\/leak-repair\.md\)$/);
  assert.ok(lines.length <= 6);
  rmSync(dir, { recursive: true, force: true });
});

test('the root is refused when an instruction file sits in it or above it', () => {
  const present = new Set(['/a/CLAUDE.md']);
  const exists = (p) => present.has(p.replace(/\\/g, '/').replace(/^[A-Za-z]:/, ''));
  assert.throws(() => assertIsolatedRoot('/a/b/c', { exists }), /CLAUDE\.md/);
  present.clear();
  present.add('/a/b/.claude/CLAUDE.md');
  assert.throws(() => assertIsolatedRoot('/a/b/c', { exists }), /\.claude\/CLAUDE\.md/);
  present.clear();
  present.add('/a/b/c/AGENTS.md');
  assert.throws(() => assertIsolatedRoot('/a/b/c', { exists }), /AGENTS\.md/);
  present.clear();
  assert.doesNotThrow(() => assertIsolatedRoot('/a/b/c', { exists }));
  assert.throws(() => assertIsolatedRoot(undefined, { exists }), /--root/);
});

test('the run is the brief\'s flags, with the tool set made exact and nothing persisted', () => {
  const args = claudeArgs('find it', { maxTurns: 20 });
  assert.deepEqual(args.slice(0, 2), ['-p', 'find it']);
  const at = (flag) => args[args.indexOf(flag) + 1];
  assert.equal(at('--output-format'), 'stream-json');
  assert.equal(at('--setting-sources'), 'project');
  assert.equal(at('--mcp-config'), '{"mcpServers":{}}');
  assert.equal(at('--allowedTools'), 'Read,Grep,Glob,Skill');
  assert.equal(at('--disallowedTools'), 'Edit,Write,Bash,WebFetch,WebSearch');
  assert.equal(at('--tools'), 'Read,Grep,Glob,Skill');
  assert.equal(at('--max-turns'), '20');
  for (const flag of ['--verbose', '--strict-mcp-config', '--include-hook-events', '--no-session-persistence']) {
    assert.ok(args.includes(flag), flag);
  }
});

test('a doc is named by its path in either layout, and by nothing else', () => {
  assert.equal(docNameOf('C:\\x\\A\\knowledge\\leak-repair.md'), 'leak-repair');
  assert.equal(docNameOf('/x/C/.claude/skills/leak-repair/SKILL.md'), 'leak-repair');
  assert.equal(docNameOf('/x/A/AGENTS.md'), null);
  assert.equal(docNameOf('/x/A/knowledge'), null);
});

// The `recorded-*` fixtures are pilot streams (KNOW3, 2026-10-02), scrubbed of machine paths and trimmed of the
// documents' text; `constructed-miss` is written by hand, because the pilot had no miss.

test('a recorded search-first run is scored: the hit, the search before it, the wrong read after it, the tokens', () => {
  const s = scoreRun(parseStream(fixture('recorded-E-T5.jsonl')), ['leak-repair']);
  assert.equal(s.hit, true);
  assert.equal(s.hitAt, 2);
  assert.equal(s.targetedAt, 2);
  assert.equal(s.callsBeforeHit, 1);
  assert.equal(s.searchesBeforeHit, 1);
  assert.deepEqual(s.wrongRead, ['2026-08-04-daoris-design']);
  assert.deepEqual(s.wrongBeforeHit, []);
  assert.equal(s.indexReads, 0);
  assert.equal(s.calls, 4);
  assert.deepEqual(s.searchTerms, [
    'Grep: secret|token|credential|leak|history|rotat|revok',
    'Grep: applies_when:.*(secret|leak|credential|commit|history|git)',
  ]);
  assert.equal(s.turns, 5);
  assert.equal(s.inputTokens, 8);
  assert.equal(s.cacheRead, 35688);
  assert.equal(s.cacheCreation, 8350);
  assert.equal(s.outputTokens, 934);
  assert.equal(s.model, 'claude-opus-5-5');
  assert.equal(s.harness, '2.1.287');
  assert.equal(s.subtype, 'success');
});

test('a recorded push run that the ranking misled: four searches, one inside the wrong document, then the read', () => {
  const s = scoreRun(parseStream(fixture('recorded-G-T12.jsonl')), ['2026-09-22-first-deployment-case-study']);
  assert.equal(s.hitAt, 5);
  assert.equal(s.targetedAt, 5, 'a search inside another document is not aiming at the truth');
  assert.equal(s.searchesBeforeHit, 4);
  assert.deepEqual(s.wrongRead, [], 'a search inside a document is not a read of it');
});

test('a recorded index run aims at the truth with its first call, a search inside it, and reads it second', () => {
  const s = scoreRun(parseStream(fixture('recorded-A-T1.jsonl')), ['2026-09-23-plugin-design']);
  assert.equal(s.targetedAt, 1);
  assert.equal(s.hitAt, 2);
  assert.equal(s.searchesBeforeHit, 1);
});

test('a search confined to one document or skill folder aims at it; a search of the folder aims at nothing', () => {
  const call = (name, input) => ({ name, input });
  assert.equal(targetOf(call('Grep', { pattern: 'harness', path: 'knowledge\\2026-09-23-plugin-design.md' })), '2026-09-23-plugin-design');
  assert.equal(targetOf(call('Grep', { pattern: 'harness', path: '.claude/skills/2026-09-23-plugin-design' })), '2026-09-23-plugin-design');
  assert.equal(targetOf(call('Grep', { pattern: 'harness', path: 'knowledge' })), null);
  assert.equal(targetOf(call('Glob', { pattern: 'knowledge/*plugin*' })), null);
  assert.equal(targetOf(call('Glob', { pattern: '.claude/skills/model-decoupling/**' })), 'model-decoupling');
  assert.equal(targetOf(call('Glob', { pattern: '.claude/skills/*/SKILL.md' })), null);
  assert.equal(targetOf(call('Glob', { pattern: '**/*.md' })), null);
  assert.equal(targetOf(call('Read', { file_path: 'INDEX.md' })), null);
  assert.equal(targetOf(call('Skill', { skill: 'leak-repair' })), 'leak-repair');

  const events = [{ type: 'assistant', parent_tool_use_id: null, message: { content: [
    { type: 'tool_use', name: 'Grep', input: { pattern: 'harness', path: 'knowledge/2026-09-23-plugin-design.md' } },
    { type: 'tool_use', name: 'Read', input: { file_path: 'knowledge/2026-09-23-plugin-design.md' } },
  ] } }];
  const s = scoreRun(events, ['2026-09-23-plugin-design']);
  assert.equal(s.targetedAt, 1);
  assert.equal(s.hitAt, 2);
});

test('a skill invoked is a document read, and a run that never reads the truth is a miss', () => {
  const skill = scoreRun(parseStream(fixture('recorded-C-T5.jsonl')), ['leak-repair']);
  assert.equal(skill.hit, true);
  assert.equal(skill.hitAt, 1);
  assert.equal(skill.calls, 1);
  assert.deepEqual(skill.searchTerms, ['Skill: leak-repair']);

  const miss = scoreRun(parseStream(fixture('constructed-miss.jsonl')), ['leak-repair']);
  assert.equal(miss.hit, false);
  assert.equal(miss.hitAt, null);
  assert.equal(miss.callsBeforeHit, null);
  assert.deepEqual(miss.wrongRead, ['2026-09-24-permission-scopes-design']);
  assert.equal(miss.indexReads, 1);
  assert.equal(miss.calls, 3);
});

/** The harness's own skills in the recorded canaries: what the control listed. */
const HARNESS_SKILLS = parseStream(fixture('recorded-canary-A.jsonl')).find((e) => e.subtype === 'init').skills;

test('a canary is isolated only with the exact tools, no server, built-in plugins only and no stray hook', () => {
  const events = parseStream(fixture('recorded-canary-A.jsonl'));
  assert.deepEqual(isolationProblems(events, { design: 'A', baselineSkills: HARNESS_SKILLS }), []);
  assert.match(isolationProblems(events, { design: 'A', baselineSkills: ['dataviz'] }).join('\n'), /beyond the harness's own/);

  const init = events.find((e) => e.type === 'system' && e.subtype === 'init');
  const dirty = [
    { ...init, tools: [...init.tools, 'Bash'], mcp_servers: [{ name: 'memory', status: 'connected' }],
      plugins: [...init.plugins, { name: 'helper', path: '/home/x/.claude/plugins/helper', source: 'helper@market' }],
      skills: [...init.skills, 'helper:recall'] },
    { type: 'system', subtype: 'hook_started', hook_event: 'UserPromptSubmit', hook_name: 'UserPromptSubmit' },
    ...events.filter((e) => e !== init),
  ];
  const problems = isolationProblems(dirty, { design: 'A', baselineSkills: HARNESS_SKILLS }).join('\n');
  assert.match(problems, /tools/);
  assert.match(problems, /MCP/);
  assert.match(problems, /plugin/);
  assert.match(problems, /skill/);
  assert.match(problems, /hook/);
});

test('the push design must show its own hook firing, and nothing else', () => {
  const g = parseStream(fixture('recorded-canary-G.jsonl'));
  assert.deepEqual(isolationProblems(g, { design: 'G', baselineSkills: HARNESS_SKILLS }), []);
  const silent = g.filter((e) => !String(e.subtype ?? '').startsWith('hook_'));
  assert.match(isolationProblems(silent, { design: 'G', baselineSkills: HARNESS_SKILLS }).join('\n'), /ranker/);
  const stray = [...g, { type: 'system', subtype: 'hook_started', hook_name: 'SessionStart', hook_event: 'SessionStart' }];
  assert.match(isolationProblems(stray, { design: 'G', baselineSkills: HARNESS_SKILLS }).join('\n'), /other than the ranker/);
});

test('a limit stops the bench: a rejected window, an error result, or a window nearly spent', () => {
  assert.equal(limitReached([{ type: 'rate_limit_event', rate_limit_info: { status: 'allowed', unifiedWindows: { five_hour: { utilization: 0.3 } } } }]), null);
  assert.match(limitReached([{ type: 'rate_limit_event', rate_limit_info: { status: 'rejected' } }]), /rejected/);
  assert.match(limitReached([{ type: 'rate_limit_event', rate_limit_info: { status: 'allowed', unifiedWindows: { five_hour: { utilization: 0.9 } } } }]), /five_hour/);
  assert.match(limitReached([{ type: 'result', is_error: true, result: 'You have hit your usage limit' }]), /limit/);
  assert.equal(limitReached([{ type: 'result', is_error: false, subtype: 'error_max_turns' }]), null);
});

test('the aggregate gives each design its hit rate, median calls to the hit and tokens per hit', () => {
  const runs = [
    { design: 'A', hit: true, hitAt: 2, targetedAt: 1, searchesBeforeHit: 0, wrongRead: [], inputTokens: 10, outputTokens: 90, cacheRead: 900, cacheCreation: 0, wallMs: 1000, turns: 3, costUsd: 0.1 },
    { design: 'A', hit: true, hitAt: 4, searchesBeforeHit: 1, wrongRead: ['x'], inputTokens: 10, outputTokens: 90, cacheRead: 900, cacheCreation: 0, wallMs: 3000, turns: 5, costUsd: 0.1 },
    { design: 'A', hit: false, hitAt: null, searchesBeforeHit: null, wrongRead: ['y', 'z'], inputTokens: 10, outputTokens: 90, cacheRead: 900, cacheCreation: 0, wallMs: 2000, turns: 9, costUsd: 0.1 },
  ];
  const [a] = aggregate(runs);
  assert.equal(a.design, 'A');
  assert.equal(a.runs, 3);
  assert.equal(a.hits, 2);
  assert.equal(a.medianCallsToHit, 3);
  assert.equal(a.medianCallsToTarget, 1, 'a run that never aimed at the truth adds nothing to the median');
  assert.equal(a.tokensPerHit, 1500);
  assert.ok(Math.abs(a.costPerHit - 0.15) < 1e-9, 'every run\'s spend, over the hits');
  assert.equal(a.meanWrongReads, 1);
  assert.equal(a.medianWallMs, 2000);
});

test('the always-loaded bytes are measured per design and projected to 169 documents by arithmetic', () => {
  const a = alwaysLoaded('A', fixtureFiles('A', CORPUS), CORPUS);
  const b = alwaysLoaded('B', fixtureFiles('B', CORPUS), CORPUS);
  const zero = alwaysLoaded('0', fixtureFiles('0', CORPUS), CORPUS);
  assert.ok(a.total > b.total && b.total > zero.total);
  const c = alwaysLoaded('C', fixtureFiles('C', CORPUS), CORPUS);
  assert.ok(c.skillListing > 0);
  const p = project('A', fixtureFiles('A', CORPUS), CORPUS, 169);
  assert.equal(p, a.fixed + Math.round(a.perDocument * 169));
  assert.equal(project('B', fixtureFiles('B', CORPUS), CORPUS, 169), b.total);
  const index = Buffer.byteLength(fixtureFiles('B', CORPUS).get('INDEX.md'));
  assert.equal(projectIndex(fixtureFiles('B', CORPUS), CORPUS, CORPUS.length), index, 'at its own count, the index is itself');
  assert.ok(projectIndex(fixtureFiles('B', CORPUS), CORPUS, 169) > index * 20);
});

test('the tasks are a dozen, each with one truth or two, and the pilot is three of them', () => {
  assert.ok(TASKS.length >= 10 && TASKS.length <= 14);
  for (const t of TASKS) assert.ok(t.truth.length >= 1 && t.truth.length <= 2, t.id);
  assert.equal(PILOT_TASKS.length, 3);
  for (const id of PILOT_TASKS) assert.ok(TASKS.some((t) => t.id === id));
});
