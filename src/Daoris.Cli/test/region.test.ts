import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError } from './_fixture.ts';
import {
  CLOSE, IMPORT, OPEN, ensureImport, findRegion, hasImport, writeRegion,
} from '../src/region.ts';

/**
 * The marked region (CANON8a/D59) — where the always-loaded tier lives once it leaves
 * `.claude/rules/`, because that directory is read by exactly one of the three harnesses.
 *
 * `docs/2026-09-22-instruction-file-design.md` §4 is the table these tests implement. It extends D19
 * — which exists because this area was corrected four times before anyone wrote a table down — with
 * the three states a file never had, and every one of them **refuses**.
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
 * would show as a whole-file diff on every checkout. D25 was this same assumption, the other way
 * round, and it was only found because the release gates run on Linux and development on Windows.
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
  // And writing what was read back changes nothing, which is the property sync depends on.
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

// ── the import line ───────────────────────────────────────────────────────────────────────────────

/**
 * `CLAUDE.md` is the same mechanism, smaller: one line pointing at `AGENTS.md`, for the one harness
 * that looks for the other name. The measured reason it is an import rather than a copy is that dsh
 * and codex read `AGENTS.md` directly, so only Claude Code ever follows this line.
 */
test('a file already holding the import needs nothing', () => {
  assert.equal(hasImport('@AGENTS.md\n', 'AGENTS.md'), true);
  assert.equal(hasImport('# Theirs\n\nSee @AGENTS.md for the rules.\n', 'AGENTS.md'), true);
  assert.equal(hasImport('# Theirs\n\nNothing points anywhere.\n', 'AGENTS.md'), false);
});

/** A file that mentions the NAME without importing it has not imported it. */
test('a mention is not an import', () => {
  assert.equal(hasImport('We keep doctrine in AGENTS.md.\n', 'AGENTS.md'), false);
});

/**
 * The boundary is about PATHS, not whitespace — three other files start with the same characters.
 * Requiring whitespace after the target failed the first sentence anybody would actually write:
 * `"Everything is in @AGENTS.md."`, where the full stop belongs to the sentence.
 */
test('a longer path that merely starts the same way is a different file', () => {
  for (const near of ['@AGENTS.mdx\n', '@AGENTS.md.backup\n', '@AGENTS.md/nested\n', '@AGENTS.md-old\n']) {
    assert.equal(hasImport(near, 'AGENTS.md'), false, near);
  }

  for (const real of ['@AGENTS.md.\n', '@AGENTS.md, and nothing else.\n', 'see @AGENTS.md)\n']) {
    assert.equal(hasImport(real, 'AGENTS.md'), true, real);
  }
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

/**
 * Creating the pointer file, and the three states §4 gives it.
 *
 * 🔴 **Always a region, even when Daoris creates the whole file.** The adopter whose layout this was
 * taken from has a bare one-line `CLAUDE.md`, which is prettier — and a bare line is a line nothing
 * can later retire, because `daoris.lock` is the authority and a line is not a file. One mechanism
 * means `check`, `sync` and retirement read this file exactly as they read the other one.
 */
test('a repository with no pointer file gets one holding the import', () => {
  const made = ensureImport(null, 'AGENTS.md');

  assert.ok(made !== null);
  assert.equal(findRegion(made!, IMPORT).kind, 'present');
  assert.equal(hasImport(made!, 'AGENTS.md'), true);
});

test('a pointer file that already imports it is left completely alone', () => {
  // The exact shape the style was taken from: one line, no region, nothing of ours.
  assert.equal(ensureImport('@AGENTS.md\n', 'AGENTS.md'), null);
  // And a repository that wrote its own sentence around the import is equally done.
  assert.equal(ensureImport('# Ours\n\nEverything is in @AGENTS.md.\n', 'AGENTS.md'), null);
});

test('a pointer file with its own content keeps every word and gains the region', () => {
  const theirs = '# Ours\n\nRules we wrote, and no pointer anywhere.\n';

  const made = ensureImport(theirs, 'AGENTS.md');

  assert.ok(made!.startsWith(theirs));
  assert.equal(hasImport(made!, 'AGENTS.md'), true);
});

test('the pointer file is idempotent through the region, not just through the text', () => {
  const once = ensureImport('# Ours\n', 'AGENTS.md')!;

  assert.equal(ensureImport(once, 'AGENTS.md'), null);
});
