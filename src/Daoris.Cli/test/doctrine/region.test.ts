import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError } from '../_fixture.ts';
import {
  CLOSE, IMPORT, OPEN, findRegion, hasImport, removeRegion, retireImport, writeRegion,
} from '../../src/doctrine/region.ts';

/**
 * The marked region — where the always-loaded tier lives once it leaves
 * `.claude/rules/`, because that directory is read by exactly one of the three harnesses.
 *
 * `docs/archive/2026-09-22-instruction-file-design.md` §4 is the table these tests implement: the three
 * states of a file beyond the lock × disk × canon table, and every one of them **refuses**.
 *
 * 🔴 The whole safety argument is one sentence: **never guess at a boundary.** The text on the other
 * side of it is the adopter's own doctrine, and `file-tool-discipline` already says what a computed
 * boundary does when the guess is wrong — it takes the rest of the file with it.
 */

const NAME = 'rules';
const open = OPEN(NAME);
const close = CLOSE(NAME);

/** A file with the adopter's own text around an intact region. */
const intact = [
  '# Design System',
  '',
  'Their own doctrine, which Daoris never reads.',
  '',
  open,
  'the tier',
  close,
  '',
  'More of theirs, after it.',
  '',
].join('\n');

// ── absent ────────────────────────────────────────────────────────────────────────────────────────

test('a file with no region reports absent, and gains one appended', () => {
  const theirs = '# Theirs\n\nAll of it.\n';

  assert.equal(findRegion(theirs, NAME).kind, 'absent');

  const written = writeRegion(theirs, NAME, 'the tier');
  // 🔴 Their text is not read, moved or rewritten — it is a PREFIX of the result, byte for byte.
  assert.ok(written.startsWith(theirs), written);
  assert.match(written, /<!-- daoris:rules[^>]*-->\nthe tier\n<!-- \/daoris:rules -->/);
});

test('a file of mixed line endings keeps each line outside the region as it was', () => {
  const before = 'a\r\nb\r\nc\nd\r\n';
  const after = 'e\nf\r\n';
  const held = `${before}${open}\r\nold\r\n${close}\r\n${after}`;

  const written = writeRegion(held, NAME, 'new one\nnew two');

  assert.equal(written, `${before}${open}\r\nnew one\r\nnew two\r\n${close}\r\n${after}`);
});

test('taking a region out keeps each line left as it was, and ends the file once', () => {
  assert.equal(removeRegion(`a\r\nb\n\n${open}\nx\n${close}\n`, NAME), 'a\r\nb\n');
  assert.equal(removeRegion(`a\r\nb\r\n\r\n${open}\r\nx\r\n${close}\r\n`, NAME), 'a\r\nb\r\n');
});

test('a region that ends a file with no newline stays without one, in either line ending', () => {
  for (const eol of ['\n', '\r\n']) {
    const held = `a${eol}b${eol}${open}${eol}old${eol}${close}`;

    assert.equal(writeRegion(held, NAME, 'new'), `a${eol}b${eol}${open}${eol}new${eol}${close}`);
  }
});

test('an empty file, or none at all, becomes the region alone', () => {
  for (const nothing of ['', '\n']) {
    const written = writeRegion(nothing, NAME, 'the tier');
    assert.ok(written.startsWith(open), JSON.stringify(written));
    assert.ok(written.endsWith(`${close}\n`), JSON.stringify(written));
  }
});

// ── present ───────────────────────────────────────────────────────────────────────────────────────

test('an intact region is found, and its body read back', () => {
  const found = findRegion(intact, NAME);

  assert.equal(found.kind, 'present');
  assert.equal(found.kind === 'present' ? found.body : null, 'the tier');
});

test('rewriting a region replaces its body and preserves every byte outside it', () => {
  const written = writeRegion(intact, NAME, 'a better tier');

  assert.match(written, /a better tier/);
  assert.doesNotMatch(written, /^the tier$/m);
  assert.ok(written.startsWith('# Design System\n\nTheir own doctrine, which Daoris never reads.\n'));
  assert.ok(written.endsWith('\nMore of theirs, after it.\n'));
});

/** Writing the same body twice is the same file — otherwise every sync is a diff. */
test('writing is idempotent', () => {
  const once = writeRegion(intact, NAME, 'the tier');
  assert.equal(writeRegion(once, NAME, 'the tier'), once);
});

/**
 * An adopter's file may be CRLF — one of them ships a `.gitattributes` — and a region written in LF
 * would show as a whole-file diff on every checkout. The same holds the other way round, and the
 * release gates run on Linux while development runs on Windows.
 */
test('the region takes the line ending the file already uses', () => {
  const crlf = '# Theirs\r\n\r\nAll of it.\r\n';

  const written = writeRegion(crlf, NAME, 'the tier');

  assert.ok(!/[^\r]\n/.test(written), JSON.stringify(written));
  assert.equal(findRegion(written, NAME).kind, 'present');
  assert.equal(
    findRegion(written, NAME).kind === 'present' ? (findRegion(written, NAME) as { body: string }).body : null,
    'the tier');
});

/**
 * 🔴 A **multi-line** body in a CRLF file. The single-line case above passed while this one did not:
 * the read stripped only the last line's `\r`, so every interior line came back carrying one and the
 * body never round-tripped. Caught by running the real thing against a real adopter's file rather
 * than by another fixture — which is the whole argument for doing that before calling it done.
 */
test('a multi-line body round-trips through a CRLF file', () => {
  const crlf = '# Theirs\r\n\r\nAll of it.\r\n';
  const body = '## Always loaded\n\nOne rule.\n\nAnother rule.';

  const written = writeRegion(crlf, NAME, body);
  const found = findRegion(written, NAME);

  assert.ok(!/[^\r]\n/.test(written), 'the file stopped being CRLF');
  assert.equal(found.kind === 'present' ? found.body : null, body);
  // And writing what is read back changes nothing, which is the property sync depends on.
  assert.equal(writeRegion(written, NAME, found.kind === 'present' ? found.body : ''), written);
});

// ── the three refusals ────────────────────────────────────────────────────────────────────────────

test('an opening marker with no closing one is refused, naming the line', () => {
  const damaged = ['# Theirs', '', open, 'the tier', '', 'and then nothing closed it'].join('\n');

  const error = captureError(() => findRegion(damaged, NAME));

  assert.match(error.message, /line 3/);
  assert.match(error.message, /clos/i);
  // It must say what to do, like every other refusal in this tool.
  assert.match(error.message, /daoris:rules/);
});

test('a closing marker with no opening one is refused', () => {
  const damaged = ['# Theirs', '', 'the tier', close].join('\n');

  const error = captureError(() => findRegion(damaged, NAME));

  assert.match(error.message, /line 4/);
  assert.match(error.message, /open/i);
});

test('a closing marker before its opening one is refused', () => {
  const damaged = ['# Theirs', close, 'the tier', open].join('\n');

  const error = captureError(() => findRegion(damaged, NAME));

  assert.match(error.message, /line 2/);
});

test('two regions are refused, naming every line a marker sits on', () => {
  const twice = ['# Theirs', open, 'one', close, '', open, 'two', close].join('\n');

  const error = captureError(() => findRegion(twice, NAME));

  assert.match(error.message, /2/);
  assert.match(error.message, /6/);
});

/**
 * 🔴 The refusals must hold on WRITE as well as on read. A damaged file that read as absent would be
 * appended to, leaving two regions; one that threw only on read would still be silently clobbered by
 * a writer that did its own scanning. One implementation, one set of refusals.
 */
test('a damaged file refuses the write too, rather than appending a second region', () => {
  const damaged = ['# Theirs', open, 'the tier'].join('\n');

  const error = captureError(() => writeRegion(damaged, NAME, 'anything'));

  assert.match(error.message, /clos/i);
});

/**
 * Markers are matched as whole lines. A marker quoted inside a fenced code block — which is exactly
 * what this repository's own documentation does when explaining the shape — is prose, not a boundary,
 * and treating it as one would cut a file in half at a sentence about cutting files in half.
 */
test('an indented or quoted marker is not a boundary', () => {
  const quoted = ['# Theirs', '', '```', `    ${open}`, '```', '', `> ${close}`].join('\n');

  assert.equal(findRegion(quoted, NAME).kind, 'absent');
});

// ── the retired import line ──────────────────────────────────────────────────────────────────────

/**
 * `CLAUDE.md` held one import region so the one harness that looked for the other name found the tier. Every
 * agent in use reads `AGENTS.md` itself (`docs/2026-11-22-instruction-file-evidence.md`), so a file holding nothing
 * else goes, and a file with text of its own keeps the region: Claude Code reads that file and then not
 * `AGENTS.md`.
 */
const REGION = `${OPEN(IMPORT)}\n@AGENTS.md\n${CLOSE(IMPORT)}\n`;

test('a file holding only the import region is retired', () => {
  assert.equal(retireImport(REGION), 'remove');
  assert.equal(retireImport(REGION.replace(/\n/g, '\r\n')), 'remove', 'a CRLF file too');
});

test("a byte-order mark before the region is the file's, not a damaged marker", () => {
  assert.equal(retireImport(`﻿${REGION}`), 'remove');
  assert.equal(retireImport(`﻿# Ours\n\n${REGION}`), 'kept');
});

test('a mention of the brief is not an import of it, and a hand-written import is one', () => {
  assert.equal(hasImport('We keep doctrine in AGENTS.md.\n', 'AGENTS.md'), false);
  assert.equal(hasImport('@AGENTS.mdx\n', 'AGENTS.md'), false);
  assert.equal(hasImport('Everything is in @AGENTS.md.\n', 'AGENTS.md'), true);
  assert.equal(hasImport(REGION, 'AGENTS.md'), true);
});

test('a file with text of its own beside the region is kept, every word', () => {
  assert.equal(retireImport(`# Ours\n\nRules we wrote.\n\n${REGION}`), 'kept');
  assert.equal(retireImport(`${REGION}\n# After\n`), 'kept');
});

test('a file with no region is not a pointer of ours: a hand-written import is left alone', () => {
  assert.equal(retireImport('@AGENTS.md\n'), 'none');
  assert.equal(retireImport('# Ours\n\nEverything is in @AGENTS.md.\n'), 'none');
  assert.equal(retireImport(''), 'none');
});

test('a damaged import region is refused, not guessed at', () => {
  assert.match(captureError(() => retireImport(`# Ours\n${OPEN(IMPORT)}\n@AGENTS.md\n`)).message, /never closed/);
});

/**
 * The refusals hold on a CRLF file too. Markers are compared as whole lines, and a `\r` on the end of
 * one is the file's business — but a comparison that forgot it would read every marker in a Windows
 * checkout as ordinary prose, so a damaged region there would be silently appended to instead of
 * refused. The quiet failure is the dangerous direction.
 */
test('a damaged region in a CRLF file is refused, not overlooked', () => {
  const damaged = ['# Theirs', '', open, 'the tier'].join('\r\n');

  assert.match(captureError(() => findRegion(damaged, NAME)).message, /never closed/);
  assert.match(captureError(() => writeRegion(damaged, NAME, 'x')).message, /never closed/);
});

/** And an intact CRLF region is found, which is the other half of the same comparison. */
test('an intact region in a CRLF file is found', () => {
  const written = writeRegion('# Theirs\r\n', NAME, 'the tier');

  assert.equal(findRegion(written, NAME).kind, 'present');
});
