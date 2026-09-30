// The marked region — where the always-loaded tier lives (CANON8a, D59).
//
// `.claude/rules/` is read by exactly one of the three harnesses this family drives (measured:
// `docs/2026-09-21-dsh-evaluation.md` §6.5), so the tier moves into `AGENTS.md`, which all three
// read. That file belongs to the ADOPTER — one of them carries 133 lines of its own in it — so
// Daoris owns a region of it and nothing else.
//
// `docs/2026-09-22-instruction-file-design.md` §4 is the state space, and it extends D19 with three
// states a file never had. Every one of them REFUSES.
//
// 🔴 **Never guess at a boundary.** The text on the other side is somebody's own doctrine, and
// `file-tool-discipline` already says what a computed boundary does when the guess is wrong: "from
// this heading to the next is a guess about structure, and when the guess is wrong it takes the rest
// of the file with it." So markers are matched as WHOLE LINES, damage is reported with the line
// number, and nothing is ever inferred from position.

import { DaorisError } from './errors.ts';

/** The opening marker for a named region. Carries the warning, like every generated file's header. */
export function OPEN(name: string): string {
  return `<!-- daoris:${name} — generated; edit the canon, not this -->`;
}

export function CLOSE(name: string): string {
  return `<!-- /daoris:${name} -->`;
}

/**
 * Whether a line IS a marker — the whole line, trimmed of nothing but the trailing `\r` a CRLF file
 * carries.
 *
 * @remarks
 * Deliberately not a substring test. This repository's own documentation quotes these markers inside
 * fenced code blocks while explaining the shape, and a substring match would cut a file in half at a
 * sentence about cutting files in half. An indented or quoted marker is prose.
 */
function isMarker(line: string, marker: string): boolean {
  return line.replace(/\r$/, '') === marker;
}

/** The region that holds the always-loaded tier, in the file every harness reads. */
export const RULES = 'rules';

/** The region that holds the pointer, in the file only one harness reads. */
export const IMPORT = 'import';

export type Region =
  | { kind: 'absent' }
  | { kind: 'present'; open: number; close: number; body: string };

/**
 * Find the region, or refuse.
 *
 * @throws DaorisError when the markers are damaged — one missing, out of order, or duplicated. Each
 * refusal names the line, because the person reading it is looking at a file they are in the middle
 * of editing or have just merged badly, and "somewhere in this file" is not an answer.
 */
export function findRegion(text: string, name: string): Region {
  const lines = text.split('\n');
  const opens: number[] = [];
  const closes: number[] = [];
  const marks = { open: OPEN(name), close: CLOSE(name) };

  for (const [at, line] of lines.entries()) {
    if (isMarker(line, marks.open)) opens.push(at + 1);
    else if (isMarker(line, marks.close)) closes.push(at + 1);
  }

  if (opens.length === 0 && closes.length === 0) return { kind: 'absent' };

  if (opens.length > 1 || closes.length > 1) {
    throw new DaorisError(
      `more than one \`daoris:${name}\` region — opening marker(s) on line `
      + `${opens.join(', ') || '(none)'}, closing on line ${closes.join(', ') || '(none)'}.\n`
      + '  daoris will not guess which one it owns. Delete the ones that are not yours and\n'
      + '  re-run; the text between them is generated and will be rebuilt.',
      1);
  }

  if (closes.length === 0) {
    throw new DaorisError(
      `the \`daoris:${name}\` region opens on line ${opens[0]} and is never closed.\n`
      + `  Add \`${marks.close}\` where it should end, or delete the opening marker and\n`
      + '  re-run — daoris will not guess where somebody else\'s text begins.',
      1);
  }

  if (opens.length === 0) {
    throw new DaorisError(
      `a \`daoris:${name}\` region closes on line ${closes[0]} with nothing opening it.\n`
      + `  Add \`${marks.open}\` where it should begin, or delete the closing marker and re-run.`,
      1);
  }

  if (closes[0]! < opens[0]!) {
    throw new DaorisError(
      `the \`daoris:${name}\` region closes on line ${closes[0]} before it opens on line ${opens[0]}.\n`
      + '  Put them the right way round, or delete both and re-run.',
      1);
  }

  return {
    kind: 'present',
    open: opens[0]!,
    close: closes[0]!,
    // Between the markers, exclusive, normalised to LF.
    //
    // 🔴 Per line, not once at the end. Stripping only the last `\r` passed every single-line
    // fixture and failed on the first real adopter file, which is CRLF: the body came back with a
    // `\r` on every interior line, so it never round-tripped and `sync` would have seen drift on a
    // file nobody had touched. The line endings belong to the FILE, which `writeRegion` preserves;
    // the body is content, and content is LF here like everything else.
    body: lines.slice(opens[0]!, closes[0]! - 1).map((line) => line.replace(/\r$/, '')).join('\n'),
  };
}

/**
 * Whether a file already imports another by Claude Code's `@path` convention.
 *
 * @remarks
 * A MENTION is not an import — `"We keep doctrine in AGENTS.md"` points nothing at anything — so the
 * `@` is required and must not be part of a longer word (an email address, a handle).
 */
export function hasImport(text: string, target: string): boolean {
  const escaped = target.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  // The boundary is about PATHS, not whitespace. `"Everything is in @AGENTS.md."` imports it — the
  // full stop is the sentence's, and Claude Code follows the import anyway — where `@AGENTS.mdx`,
  // `@AGENTS.md.backup` and `@AGENTS.md/nested` are three other files. So: not followed by anything
  // that extends a path segment, and not by a dot that begins one. Requiring whitespace after it
  // failed the first sentence anybody would actually write.
  return new RegExp(`(^|[^\\w@])@${escaped}(?![\\w/\\\\-])(?!\\.\\w)`, 'm').test(text);
}

/**
 * The file's own line ending, so a region written into somebody's CRLF file does not show up as a
 * whole-file diff on their next checkout.
 *
 * @remarks
 * D25 was this assumption the other way round, and it was found only because the release gates run on
 * Linux while development happens on Windows. Majority rather than first-seen: a file that has been
 * through both worlds has some of each, and the question is which one it mostly is.
 */
function lineEnding(text: string): '\n' | '\r\n' {
  const crlf = (text.match(/\r\n/g) ?? []).length;
  const lf = (text.match(/\n/g) ?? []).length - crlf;
  return crlf > lf ? '\r\n' : '\n';
}

/**
 * Put `body` in the region, creating it if the file has none.
 *
 * Everything outside the region survives byte for byte — that is the promise the whole shape rests
 * on, and it is why an absent region is APPENDED rather than inserted anywhere clever. The adopter's
 * text is never read, parsed, moved or reflowed.
 *
 * @throws DaorisError on damaged markers, through the same `findRegion` a read goes through. One
 * implementation and one set of refusals: a writer that scanned for itself would append a second
 * region to a file whose first one is broken.
 */
export function writeRegion(text: string, name: string, body: string): string {
  const found = findRegion(text, name);
  const eol = lineEnding(text);
  const block = [OPEN(name), ...body.split('\n').map((line) => line.replace(/\r$/, '')), CLOSE(name)];

  if (found.kind === 'absent') {
    const theirs = text.replace(/[\r\n]*$/, '');
    // A blank line between their text and the region, and none at all when there was no text.
    const lead = theirs.length > 0 ? [theirs, ''] : [];
    return [...lead, ...block, ''].join(eol);
  }

  const lines = text.split('\n').map((line) => line.replace(/\r$/, ''));
  return [
    ...lines.slice(0, found.open - 1),
    ...block,
    ...lines.slice(found.close),
  ].join(eol);
}

/**
 * The file with the region taken out, or null when the region was all of it (D117 §5.4: a room taken
 * out of the manifest loses its pointer).
 *
 * @remarks
 * The inverse of `writeRegion`'s append: the marker lines and the body go, and so does the blank line
 * the append put before them, so a file that had text of its own reads as it did before. Everything
 * else is kept as it stands, in the file's own line ending.
 *
 * @throws DaorisError on damaged markers, through the same `findRegion`: removing half a region is a
 * guess about where somebody else's text begins.
 */
export function removeRegion(text: string, name: string): string | null {
  const found = findRegion(text, name);
  if (found.kind === 'absent') return text;
  const eol = lineEnding(text);
  const lines = text.split('\n').map((line) => line.replace(/\r$/, ''));
  const kept = [...lines.slice(0, found.open - 1), ...lines.slice(found.close)];
  while (kept.length && kept[kept.length - 1]!.trim() === '') kept.pop();
  return kept.length ? `${kept.join(eol)}${eol}` : null;
}

/**
 * The pointer file's content, or null when it already points where it should.
 *
 * @param text The file as it stands, or null when there is none.
 *
 * @remarks
 * `CLAUDE.md` exists so the one harness that looks for the other name finds the tier — Claude Code
 * follows `@path` imports; dsh and codex read `AGENTS.md` directly and never follow one (measured,
 * evaluation §6.5). So this line is for exactly one reader, and every other reader ignores it.
 *
 * 🔴 **Always a region, even when this creates the whole file.** A bare line is prettier and is a
 * line nothing can ever retire: `daoris.lock` is the authority (D5) and a lock entry describes a
 * file or a span, never a stray sentence. One mechanism means `check` and retirement read this file
 * exactly as they read the other one.
 *
 * A file that ALREADY imports the target — however it phrased it — is left completely alone. The
 * point is that the pointer exists, not that Daoris wrote it.
 */
export function ensureImport(text: string | null, target: string): string | null {
  const held = text ?? '';
  if (hasImport(held, target)) return null;
  return writeRegion(held, IMPORT, `@${target}`);
}
