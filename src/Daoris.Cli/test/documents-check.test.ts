// The declared documents in `sync` and `check` (DOC3; D122, the development documents design §2.7–§2.8).
//
// `sync` renders a *Where things are* table into the region; `check` fails on a fact (a declared path
// absent, a link, the table stale) and only reports a judgement (a ceiling, the root file's bytes, no
// backlog or decisions declared), D54's line. A repository that declares nothing sees no change. Each
// case was written before the code, and watched failing.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync, symlinkSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { layoutFixture } from './_layout.ts';

// ---------------------------------------------------------------- sync: the table

const DECLARED = {
  changelog: 'CHANGELOG.md',
  decisions: 'docs/DECISIONS.md',
  backlog: { path: 'TASKS.md', words: 50 },
  brief: { words: 40 },
};

/** A repository on the older layout that keeps three records and declares them, synced. */
function declaring(tag: string, documents: unknown = DECLARED): LayoutFixture {
  const fx = layoutFixture(tag);
  fx.repoFx.write('CHANGELOG.md', '# Changelog\n\n- One change.\n');
  fx.repoFx.write('docs/DECISIONS.md', '# Decisions\n\n## D1 — one\n');
  fx.repoFx.write('TASKS.md', '# Tasks\n\n- [ ] One open row.\n');
  fx.manifest({ documents });
  fx.sync();
  return fx;
}

const WHERE_TABLE = [
  '## Where things are',
  '',
  'Declared in `daoris.json`; look here before searching.',
  '',
  '| Role | Where | Its job |',
  '|---|---|---|',
  '| decisions | `docs/DECISIONS.md` | numbered decisions, with why and what each rejected |',
  '| backlog | `TASKS.md` | open work only |',
  '| changelog | `CHANGELOG.md` | what a user of a release sees changed |',
].join('\n');

test('sync renders Where things are into the region: one row per declared path, in the roles\' order', () => {
  const fx = declaring('documents-table');

  const region = fx.repoFx.region()!;
  assert.ok(region.includes(WHERE_TABLE), region);
  // The brief has a ceiling and no path: it is the file this region sits in, so it has no row.
  assert.doesNotMatch(region, /\| brief \|/);
  // After the pointer to the index, before the first rule.
  assert.ok(region.indexOf('## Read on demand') < region.indexOf('## Where things are'));
  assert.ok(region.indexOf('## Where things are') < region.indexOf('<!-- daoris: core/core/rules/'));
  assert.equal(fx.cli('check').code, 0, fx.cli('check').out);
  fx.cleanup();
});

/**
 * ORIENT2b (D151 point 4, the orientation design §1.4): the index of where things are is the row that
 * sends a session there before it searches, so it sits after the router and says so in its job.
 */
test('sync renders a declared index after the router, saying to open it before searching', () => {
  const fx = layoutFixture('documents-index');
  fx.repoFx.write('docs/README.md', '# The documents\n');
  fx.repoFx.write('docs/index/README.md', '# Where things are\n');
  fx.repoFx.write('docs/decisions/D1.md', '# D1\n');
  fx.manifest({ documents: { decisions: 'docs/decisions', index: 'docs/index/README.md', router: 'docs/README.md' } });
  fx.sync();

  const region = fx.repoFx.region()!;
  assert.ok(region.includes([
    '| router | `docs/README.md` | every document, its kind and its standing |',
    '| index | `docs/index/README.md` | where things are in the code and the records, generated: open it before searching |',
    '| decisions | `docs/decisions` |',
  ].join('\n')), region);
  assert.equal(fx.cli('check').code, 0, fx.cli('check').out);
  fx.cleanup();
});

test('check fails on a declared index that is absent, naming the role', () => {
  const fx = layoutFixture('documents-index-missing');
  fx.repoFx.write('TASKS.md', '# Tasks\n');
  fx.manifest({ documents: { index: 'docs/index/README.md', backlog: 'TASKS.md', decisions: 'TASKS.md' } });
  fx.sync();

  const { code, out } = fx.cli('check');

  assert.equal(code, 1, out);
  assert.match(out, /document\s+docs\/index\/README\.md \(index\) is declared in daoris\.json, and absent/);

  fx.repoFx.write('docs/index/README.md', '# Where things are\n');
  assert.equal(fx.cli('check').code, 0, 'written, it passes');
  fx.cleanup();
});

test('a repository that declares nothing gets no table, and its region is byte for byte what it was', () => {
  const fx = layoutFixture('documents-none');
  fx.sync();
  const before = fx.repoFx.read('AGENTS.md');

  fx.manifest({ documents: {} });
  fx.sync();

  assert.equal(fx.repoFx.read('AGENTS.md'), before);
  assert.doesNotMatch(before, /Where things are/);
  fx.cleanup();
});

test('a declaration with only ceilings renders no table', () => {
  const fx = declaring('documents-ceilings', { brief: { words: 1500 }, room: { words: 600 } });
  assert.doesNotMatch(fx.repoFx.region()!, /Where things are/);
  fx.cleanup();
});

/** Rendered from the manifest, so no such file is made: Windows refuses a pipe in a name, and Linux does not. */
test('a path with a pipe in it is escaped, so the table stays a table', () => {
  const fx = layoutFixture('documents-pipe');
  fx.manifest({ documents: { glossary: 'docs/a|b.md' } });
  fx.sync();
  assert.match(fx.repoFx.region()!, /\| glossary \| `docs\/a\\\|b\.md` \| the names people and code use/);
  assert.doesNotMatch(fx.cli('check').out, /Where things are table differs/, 'check rebuilds the same escaped row');
  fx.cleanup();
});

test('sync names a declared document that is absent, and still writes the table', () => {
  const fx = layoutFixture('documents-sync-missing');
  fx.manifest({ documents: { decisions: 'docs/DECISIONS.md' } });

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 0, `an absent document is check's fact, not a refusal of sync: ${dry.out}`);
  assert.match(dry.out, /document\s+docs\/DECISIONS\.md \(decisions\) is declared in daoris\.json, and absent/);

  const run = fx.cli('sync');
  assert.equal(run.code, 0, run.out);
  assert.match(run.out, /docs\/DECISIONS\.md \(decisions\)/);
  assert.match(fx.repoFx.region()!, /\| decisions \| `docs\/DECISIONS\.md` \|/);
  fx.cleanup();
});

test('sync refuses a declared document held as text, even with --force, and writes nothing', () => {
  const fx = layoutFixture('documents-sync-link');
  fx.repoFx.write('notes/DECISIONS.md', '# Decisions\n');
  fx.repoFx.write('DECISIONS.md', 'notes/DECISIONS.md');
  fx.manifest({ documents: { decisions: 'DECISIONS.md' } });

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1);
  assert.match(dry.out, /LINK\s+DECISIONS\.md \(decisions\)/);

  const forced = fx.cli('sync', '--force');
  assert.equal(forced.code, 1, forced.out);
  assert.match(forced.out, /DECISIONS\.md, declared as decisions, looks like a link checked out as text/);
  assert.equal(fx.repoFx.exists('AGENTS.md'), false, 'refused before anything is written');
  fx.cleanup();
});

// ---------------------------------------------------------------- check: the facts, which fail

test('check fails on a declared document that is absent, naming the role', () => {
  const fx = declaring('documents-check-missing');
  rmSync(join(fx.repoFx.root, 'docs/DECISIONS.md'));

  const { code, out } = fx.cli('check');

  assert.equal(code, 1, out);
  assert.match(out, /document\s+docs\/DECISIONS\.md \(decisions\) is declared in daoris\.json, and absent/);
  fx.cleanup();
});

test('a declared folder is a document too: a folder of decision records', () => {
  const fx = layoutFixture('documents-check-folder');
  fx.repoFx.write('docs/adr/0001-one.md', '# One\n');
  fx.manifest({ documents: { decisions: 'docs/adr' } });
  fx.sync();

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(fx.repoFx.region()!, /\| decisions \| `docs\/adr` \|/);
  fx.cleanup();
});

test('check fails on a declared document held as text, and on a file where its folder must be', () => {
  const fx = declaring('documents-check-text');
  fx.repoFx.write('notes/TASKS.md', '# Tasks\n');
  fx.repoFx.write('TASKS.md', 'notes/TASKS.md');
  rmSync(join(fx.repoFx.root, 'docs'), { recursive: true });
  fx.repoFx.write('docs', '../elsewhere/docs');

  const { code, out } = fx.cli('check');

  assert.equal(code, 1, out);
  assert.match(out, /LINK\s+TASKS\.md \(backlog\) — looks like a link checked out as text/);
  assert.match(out, /LINK\s+docs\/DECISIONS\.md \(decisions\) — docs looks like a link checked out as text/);
  assert.doesNotMatch(out, /document\s+TASKS\.md/, 'a link is not also called absent');
  fx.cleanup();
});

test('check fails on a declared document that is a real link, where this runner can make one', (t) => {
  const fx = declaring('documents-check-reallink');
  rmSync(join(fx.repoFx.root, 'docs'), { recursive: true });
  fx.repoFx.write('elsewhere/DECISIONS.md', '# Decisions\n');
  try {
    // A folder junction, which Windows grants without a privilege; a plain link elsewhere.
    symlinkSync(join(fx.repoFx.root, 'elsewhere'), join(fx.repoFx.root, 'docs'), 'junction');
  } catch {
    fx.cleanup();
    t.skip('this runner can make no link');
    return;
  }

  const { code, out } = fx.cli('check');

  assert.equal(code, 1, out);
  assert.match(out, /LINK\s+docs\/DECISIONS\.md \(decisions\) — docs is a link: every session sent there reads through it/);
  fx.cleanup();
});

test('check fails when the table differs from the manifest, and names the table', () => {
  const fx = declaring('documents-check-stale');
  fx.repoFx.write('docs/FIX-LOG.md', '# Fixes\n');
  fx.manifest({ documents: { ...DECLARED, fixes: 'docs/FIX-LOG.md' } });

  const stale = fx.cli('check');
  assert.equal(stale.code, 1, stale.out);
  assert.match(stale.out, /where\s+the region's Where things are table differs from daoris\.json's documents — run 'daoris sync'/);
  assert.doesNotMatch(stale.out, /roster|index /, 'the pointer, the rooms and the index did not move');

  fx.sync();
  assert.equal(fx.cli('check').code, 0);

  // A hand edit to the generated table is the same fact.
  fx.repoFx.write('AGENTS.md', fx.repoFx.read('AGENTS.md').replace('| backlog | `TASKS.md`', '| backlog | `TODO.md`'));
  assert.match(fx.cli('check').out, /Where things are table differs/);
  fx.cleanup();
});

test('check fails when a declaration is withdrawn and the table is left behind', () => {
  const fx = declaring('documents-check-withdrawn');
  fx.manifest({});

  const { code, out } = fx.cli('check');

  assert.equal(code, 1, out);
  assert.match(out, /Where things are table differs/);
  fx.sync();
  assert.doesNotMatch(fx.repoFx.region()!, /Where things are/);
  assert.equal(fx.cli('check').code, 0);
  fx.cleanup();
});

// ---------------------------------------------------------------- check: the reports, which never fail

test('check reports a document over its ceiling in words, and does not fail on it (D54)', () => {
  const fx = declaring('documents-report-words');
  // Whitespace-separated tokens, as the doc-budgets tool counts them: 2 for the heading, 60 below.
  fx.repoFx.write('TASKS.md', `# Tasks\n\n${'word '.repeat(60)}\n`);

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /words\s+TASKS\.md \(backlog\) is 62 words of 50 — over by 12/);
  assert.match(out, /advisory/);
  fx.cleanup();
});

test('check reports the brief over its ceiling: the root file\'s own part, outside the region', () => {
  const fx = declaring('documents-report-brief');
  fx.repoFx.write('AGENTS.md', `# Our brief\n\n${'word '.repeat(45)}\n\n${fx.repoFx.read('AGENTS.md')}`);

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /words\s+the brief \(AGENTS\.md, outside the region\) is 48 words of 40 — over by 8/);
  fx.cleanup();
});

test('a brief within its ceiling is not reported, however long the region is', () => {
  const fx = declaring('documents-report-brief-ok');
  fx.repoFx.write('AGENTS.md', `# Our brief\n\nShort.\n\n${fx.repoFx.read('AGENTS.md')}`);

  const { out } = fx.cli('check');

  assert.doesNotMatch(out, /the brief/);
  fx.cleanup();
});

test('check reports a room over its ceiling', () => {
  const fx = layoutFixture('documents-report-room');
  fx.repoFx.write('src/cli/AGENTS.md', `# The CLI\n\n${'word '.repeat(20)}\n`);
  fx.repoFx.write('src/web/AGENTS.md', '# The web\n');
  fx.manifest({ rooms: ['src/cli', 'src/web'], documents: { room: { words: 10 }, backlog: 'TASKS.md', decisions: 'TASKS.md' } });
  fx.repoFx.write('TASKS.md', '# Tasks\n');
  fx.sync();

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /words\s+src\/cli\/AGENTS\.md \(room\) is 23 words of 10 — over by 13/);
  assert.doesNotMatch(out, /src\/web\/AGENTS\.md \(room\)/);
  fx.cleanup();
});

test('check reports a ceiling on a folder, which measures nothing', () => {
  const fx = layoutFixture('documents-report-folder-ceiling');
  fx.repoFx.write('docs/adr/0001.md', '# One\n');
  fx.repoFx.write('TASKS.md', '# Tasks\n');
  fx.manifest({ documents: { decisions: { path: 'docs/adr', words: 100 }, backlog: 'TASKS.md' } });
  fx.sync();

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /docs\/adr \(decisions\) is a folder; its ceiling of 100 words measures nothing/);
  fx.cleanup();
});

test('check reports no backlog or decisions declared, and does not fail on it', () => {
  const fx = declaring('documents-report-records', { changelog: 'CHANGELOG.md' });

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /records\s+no backlog and no decisions declared in daoris\.json's documents — the canon's records have nowhere to point here/);

  fx.manifest({ documents: { changelog: 'CHANGELOG.md', decisions: 'docs/DECISIONS.md' } });
  fx.sync();
  assert.match(fx.cli('check').out, /records\s+no backlog declared/);
  fx.cleanup();
});

test('a repository that declares nothing hears nothing about records', () => {
  const fx = layoutFixture('documents-report-silent');
  fx.sync();
  assert.doesNotMatch(fx.cli('check').out, /records|words|Where things are/);
  fx.cleanup();
});

/** LAYOUT3's report and DOC3's are one line (design §6, *amended rows*): the root file in bytes. */
test('the root file over the smallest limit is one line, beside the brief\'s words', () => {
  const fx = declaring('documents-report-bytes');
  fx.repoFx.write('AGENTS.md', `# Our brief\n\n${'word '.repeat(7000)}\n\n${fx.repoFx.read('AGENTS.md')}`);

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.equal(out.split('\n').filter((line) => /32768/.test(line)).length, 1, out);
  assert.match(out, /words\s+the brief/);
  fx.cleanup();
});

test('status carries the documents and every fact check fails on', () => {
  const fx = declaring('documents-status');
  rmSync(join(fx.repoFx.root, 'CHANGELOG.md'));

  const json = JSON.parse(fx.cli('status', '--json').out);

  assert.deepEqual(json.documents.map((row: { role: string }) => row.role), ['brief', 'decisions', 'backlog', 'changelog']);
  assert.deepEqual(json.documentsMissing, [{ role: 'changelog', path: 'CHANGELOG.md' }]);
  assert.deepEqual(json.documentLinks, []);
  assert.equal(json.documentsStale, false);
  assert.match(fx.cli('status').out, /document\s+CHANGELOG\.md \(changelog\) — 'daoris check' says which/);
  fx.cleanup();
});
