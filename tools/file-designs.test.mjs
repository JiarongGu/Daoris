import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { main, planFiling, planSkippingRefused, unclaimed } from './file-designs.mjs';

const here = dirname(fileURLToPath(import.meta.url));

const ROUTER = [
  '# Documentation router', '', '| Document | Kind and subject', '|---|---|',
  '|`2026-09-01-gate-design.md`|Contract: the gate', '|`2026-10-01-open-design.md`|Accepted design: open work', '',
].join('\n');
const ARCHIVE_ROUTER = [
  '# Archive', '', '| Document | What it was | What replaced it |', '|---|---|---|', '| `2026-08-01-note.md` | A note | D1 |', '',
  'Read the router.', '',
].join('\n');

function tree() {
  return {
    'TASKS.md': '- [ ] **OPEN1** → `docs/2026-10-01-open-design.md`\n',
    'AGENTS.md': '# Brief\n',
    'docs/README.md': ROUTER,
    'docs/archive/README.md': ARCHIVE_ROUTER,
    'docs/archive/2026-08-01-note.md': '# Note\n',
    'docs/areas/gates.md': '# Gates\n',
    'docs/2026-09-01-gate-design.md':
      '# The gate design\n\nSee [the area](areas/gates.md), [open](2026-10-01-open-design.md), `2026-09-02-gate-reads.md` and [D1](decisions/D1.md).\n',
    'docs/2026-09-02-gate-reads.md': '# Gate reads\n\nFor `2026-09-01-gate-design.md`.\n',
    'docs/2026-10-01-open-design.md': '# Open design\n\nAfter `2026-09-01-gate-design.md` §2.\n',
    'docs/decisions/D1.md': '## D1 — The gate\n\nThe contract is `docs/2026-09-01-gate-design.md` §3.\n',
    'src/Gate.cs': '// The gate keeps docs/2026-09-01-gate-design.md §4; 2026-09-02-gate-reads.md has the reads.\n',
    'src/README.md': 'Read [the design](../docs/2026-09-01-gate-design.md#rules).\n',
  };
}

const planOf = (files, paths) => planFiling(Object.keys(files), (path) => files[path], paths);

test('a dated document at the top that no open row, area or brief cites is unclaimed', () => {
  const files = tree();
  assert.deepEqual(unclaimed(Object.keys(files), (path) => files[path], null).sort(),
    ['docs/2026-09-01-gate-design.md', 'docs/2026-09-02-gate-reads.md']);
});

test('every citation is respelled to the new place, a mention by name is kept, the router row moves', () => {
  const files = tree();
  const plan = planOf(files, ['docs/2026-09-01-gate-design.md', 'docs/2026-09-02-gate-reads.md']);
  const edited = (path) => plan.edits.get(path) ?? files[path];

  assert.deepEqual(plan.refused, []);
  assert.equal(edited('docs/decisions/D1.md'), '## D1 — The gate\n\nThe contract is `docs/archive/2026-09-01-gate-design.md` §3.\n');
  assert.equal(edited('src/README.md'), 'Read [the design](../docs/archive/2026-09-01-gate-design.md#rules).\n');
  assert.equal(edited('docs/2026-10-01-open-design.md'), '# Open design\n\nAfter `archive/2026-09-01-gate-design.md` §2.\n');
  assert.match(edited('src/Gate.cs'), /docs\/archive\/2026-09-01-gate-design\.md §4; 2026-09-02-gate-reads\.md has/);
  assert.equal(plan.byName, 1);
  assert.equal(edited('docs/2026-09-01-gate-design.md'),
    '# The gate design\n\nSee [the area](../areas/gates.md), [open](../2026-10-01-open-design.md), `2026-09-02-gate-reads.md` and [D1](../decisions/D1.md).\n');
  assert.equal(edited('docs/2026-09-02-gate-reads.md'), '# Gate reads\n\nFor `2026-09-01-gate-design.md`.\n');
  assert.doesNotMatch(edited('docs/README.md'), /gate-design/);
  assert.match(edited('docs/README.md'), /2026-10-01-open-design\.md/);
  assert.match(edited('docs/archive/README.md'),
    /\| `2026-08-01-note\.md` \| A note \| D1 \|\n\| `2026-09-01-gate-design\.md` \| Contract: the gate \| Filed: no open row or area cites it \|\n\| `2026-09-02-gate-reads\.md` \| Gate reads \| Filed/);
});

test('a citation the move may not edit, reaching neither place, a fixture\'s or past the ratchet is refused; skipping leaves its design', () => {
  const big = `// docs/2026-09-02-gate-reads.md\n${'x'.repeat(41 * 1024)}\n`;
  const files = {
    ...tree(),
    'daoris.lanes.json': '{ "_why": "see docs/2026-09-02-gate-reads.md" }',
    'tools/x.test.mjs': "const f = 'fixture/2026-09-01-gate-design.md';\n",
    'tools/y.test.mjs': "write('docs/2026-09-01-gate-design.md', 'a fixture');\n",
    'tools/big.mjs': big,
    'tools/size-ratchet.json': JSON.stringify({ 'tools/big.mjs': Buffer.byteLength(big) }),
  };
  const plan = planOf(files, ['docs/2026-09-01-gate-design.md', 'docs/2026-09-02-gate-reads.md']);
  assert.deepEqual(plan.refused.map((r) => r.file).sort(), ['daoris.lanes.json', 'tools/big.mjs', 'tools/x.test.mjs', 'tools/y.test.mjs']);

  const skipping = planSkippingRefused(Object.keys(files), (path) => files[path], ['docs/2026-09-01-gate-design.md', 'docs/2026-09-02-gate-reads.md']);
  assert.deepEqual([...skipping.moved.keys()], []);
  assert.equal(skipping.skipped.length, 4);
});

test('a run moves the files with git, writes the citations, and leaves none behind', () => {
  const root = join(here, '..', '_fixtures', `file-designs-${process.pid}`);
  rmSync(root, { recursive: true, force: true });
  const git = (...args) => spawnSync('git', args, { cwd: root, encoding: 'utf8' });
  try {
    for (const [path, text] of Object.entries(tree())) {
      mkdirSync(dirname(join(root, path)), { recursive: true });
      writeFileSync(join(root, path), text);
    }
    git('init', '-q');
    git('add', '.');
    git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '-m', 'tree');
    const said = [];
    const code = main(['--root', root, '--unclaimed', '--expect-moved', '2'], { out: (line) => said.push(line), err: (line) => said.push(line) });

    assert.equal(code, 0, said.join('\n'));
    assert.ok(existsSync(join(root, 'docs/archive/2026-09-01-gate-design.md')));
    assert.ok(!existsSync(join(root, 'docs/2026-09-01-gate-design.md')));
    assert.match(readFileSync(join(root, 'src/README.md'), 'utf8'), /docs\/archive\/2026-09-01-gate-design\.md/);
    assert.match(git('status', '--porcelain').stdout, /R. docs\/2026-09-01-gate-design\.md -> docs\/archive\/2026-09-01-gate-design\.md/);
    assert.equal(main(['--root', root, 'docs/2026-10-01-open-design.md', '--expect-moved', '2', '--plan'], { out: () => {}, err: () => {} }), 1);
    assert.equal(main(['--root', root, '--check'], { out: () => {}, err: () => {} }), 0);
    writeFileSync(join(root, 'src/Late.cs'), '// merged later: docs/2026-09-01-gate-design.md\n');
    git('add', 'src/Late.cs');
    const late = [];
    assert.equal(main(['--root', root, '--check'], { out: () => {}, err: (line) => late.push(line) }), 1);
    assert.match(late.join('\n'), /src\/Late\.cs:1: docs\/2026-09-01-gate-design\.md reaches/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
