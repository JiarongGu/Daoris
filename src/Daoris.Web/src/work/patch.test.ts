import { describe, expect, it } from 'vitest';
import { pairRows, parsePatch } from './patch';

// A review's patch as structure (REVIEW2): git's own unified diff, read into hunks with the line
// numbers each side had, so a view can number them and set them side by side. Pure.

const PATCH = [
  'diff --git a/src/chunk.rs b/src/chunk.rs',
  'index 1111111..2222222 100644',
  '--- a/src/chunk.rs',
  '+++ b/src/chunk.rs',
  '@@ -10,4 +10,5 @@ pub fn hydrate(region: &Region) {',
  '     for tile in &region.tiles {',
  '-        load(tile);',
  '+        if budget.spent() {',
  '+            break;',
  '+        }',
  '     }',
  '@@ -40,2 +41,2 @@',
  '-old tail',
  '+new tail',
  '\\ No newline at end of file',
].join('\n');

describe('reading a patch', () => {
  it('reads each hunk with the line numbers its old and new sides had', () => {
    const { hunks } = parsePatch(PATCH);
    expect(hunks).toHaveLength(2);
    expect(hunks[0]!.header).toBe('@@ -10,4 +10,5 @@ pub fn hydrate(region: &Region) {');
    expect(hunks[0]!.lines).toEqual([
      { kind: 'same', text: '    for tile in &region.tiles {', old: 10, new: 10 },
      { kind: 'del', text: '        load(tile);', old: 11 },
      { kind: 'add', text: '        if budget.spent() {', new: 11 },
      { kind: 'add', text: '            break;', new: 12 },
      { kind: 'add', text: '        }', new: 13 },
      { kind: 'same', text: '    }', old: 12, new: 14 },
    ]);
  });

  it('keeps git\'s own notes as notes, and what came before the first hunk as its preamble', () => {
    const { preamble, hunks } = parsePatch(PATCH);
    expect(preamble).toEqual(['diff --git a/src/chunk.rs b/src/chunk.rs', 'index 1111111..2222222 100644', '--- a/src/chunk.rs', '+++ b/src/chunk.rs']);
    expect(hunks[1]!.lines.at(-1)).toEqual({ kind: 'note', text: '\\ No newline at end of file' });
  });

  /**
   * git writes a blank context line as a single space, and a patch whose trailing whitespace was
   * stripped on the way carries it as an empty line. Either is a line of the file; only the patch's
   * own final newline is not.
   */
  it('keeps a blank context line as a line, however it arrived', () => {
    const lines = parsePatch('@@ -1,4 +1,4 @@\n a\n \n\n b\n').hunks[0]!.lines;
    expect(lines.map((line) => [line.kind, line.text])).toEqual([['same', 'a'], ['same', ''], ['same', ''], ['same', 'b']]);
  });

  it('reads a patch with no hunks — a rename, a mode change — as its preamble alone', () => {
    const renamed = parsePatch('diff --git a/a.md b/b.md\nsimilarity index 100%\nrename from a.md\nrename to b.md');
    expect(renamed.hunks).toEqual([]);
    expect(renamed.preamble).toHaveLength(4);
  });
});

describe('setting a hunk side by side', () => {
  it('pairs a run of removals with the run of additions after it, and leaves the rest alone', () => {
    const rows = pairRows(parsePatch(PATCH).hunks[0]!.lines);
    expect(rows.map((row) => ('note' in row ? [row.note.text] : [row.left?.text ?? null, row.right?.text ?? null]))).toEqual([
      ['    for tile in &region.tiles {', '    for tile in &region.tiles {'],
      ['        load(tile);', '        if budget.spent() {'],
      [null, '            break;'],
      [null, '        }'],
      ['    }', '    }'],
    ]);
  });

  it('lays a note across both sides', () => {
    const rows = pairRows(parsePatch(PATCH).hunks[1]!.lines);
    expect(rows.at(-1)).toEqual({ note: { kind: 'note', text: '\\ No newline at end of file' } });
  });
});
