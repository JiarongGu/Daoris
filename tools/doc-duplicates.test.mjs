import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { check, resurrected } from './doc-duplicates.mjs';

const SCRATCH = join(fileURLToPath(new URL('..', import.meta.url)), '_fixtures', 'doc-duplicates');

const OPEN = '- [ ] **EXTENDSEAMS1 — each area says how its part takes an addition** (canon, docs; M): the rest.';
const DONE = '- [x] **EXTENDSEAMS1 — each area says how its part takes an addition** (canon, docs; 2026-11-26): what shipped.';

test('a backlog row the archive holds done under the same id and title is named with both lines', () => {
  const found = resurrected(`# Tasks\n\n## Now\n${OPEN}\n`, `# Task archive\n\nintro\n${DONE}\n`);
  assert.deepEqual(found, [{ id: 'EXTENDSEAMS1', backlogLine: 4, archiveLine: 4 }]);
});

test('a row the archive holds only in part, or a different row of the same id, is no resurrection', () => {
  const backlog = '- [ ] **FOUNDATION1 — one generic foundation** (driver): left.\n- [ ] **FABRIC-PIPELINE1 — verification as a pipeline** (driver): left.\n';
  const archive = '- [x] **FOUNDATION1 — one foundation for who acts** (D212): every row but n.\n- [x] **FABRIC-PIPELINE1d — the check performed** (driver).\n'
    + '- **FOUNDATION1 (2026-11-16):** a dated line is no title to compare.\n';
  assert.deepEqual(resurrected(backlog, archive), []);
});

test('an open archive line, a done backlog line and a fenced example are no resurrection', () => {
  assert.deepEqual(resurrected(OPEN.replace('[ ]', '[x]'), DONE), []);
  assert.deepEqual(resurrected(OPEN, OPEN), []);
  assert.deepEqual(resurrected(`\`\`\`\n${OPEN}\n\`\`\`\n`, DONE), []);
  assert.deepEqual(resurrected(OPEN, `\`\`\`\n${DONE}\n\`\`\`\n`), []);
});

test('check reads the declared backlog and archive and fails the resurrected row, naming both lines', () => {
  mkdirSync(SCRATCH, { recursive: true });
  const root = mkdtempSync(join(SCRATCH, 'resurrect-'));
  try {
    mkdirSync(join(root, 'docs'));
    writeFileSync(join(root, 'daoris.json'), JSON.stringify({ documents: { backlog: { path: 'ROWS.md' }, archive: 'docs/done.md' } }));
    writeFileSync(join(root, 'ROWS.md'), `# Rows\n\n${OPEN}\n`);
    writeFileSync(join(root, 'docs', 'done.md'), `# Done\n\n\n${DONE}\n`);
    const found = check(root).filter((each) => each.fact === 'a row the archive holds done');
    assert.equal(found.length, 1);
    assert.equal(found[0].file, 'ROWS.md');
    assert.match(found[0].key, /EXTENDSEAMS1/);
    assert.match(found[0].key, /line 3/);
    assert.match(found[0].key, /docs\/done\.md line 4/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
