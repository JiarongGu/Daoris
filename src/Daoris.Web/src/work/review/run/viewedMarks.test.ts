import { beforeEach, describe, expect, it } from 'vitest';
import { lineDiff } from './lineDiff';
import { contentOf, keptViewed, markViewed } from './viewedMarks';

// A file's viewed mark is kept by its path and its content, so a reader's mark stands on a file a newer revision left untouched and
// is gone from one it changed; and a file's two versions are compared line by line where no patch is kept.

describe('viewed marks by content', () => {
  beforeEach(() => window.localStorage.clear());

  it('keeps a mark on a file whose content a newer revision left as it was, and clears it on one it changed', () => {
    markViewed('notes.md', 'b1', true);
    markViewed('plan.md', 'p1', true);

    const marks = keptViewed();
    expect(marks.has('notes.md', 'b1')).toBe(true);
    expect(marks.has('plan.md', 'p1')).toBe(true);
    // The next revision wrote notes.md again and left plan.md.
    expect(marks.has('notes.md', 'b2')).toBe(false);
    expect(marks.has('plan.md', 'p1')).toBe(true);
  });

  it('takes a mark back, and reads one kept by another page of this window', () => {
    markViewed('notes.md', 'b1', true);
    markViewed('notes.md', 'b1', false);
    expect(keptViewed().has('notes.md', 'b1')).toBe(false);
  });

  it('names a row\'s content by its hash after, a removed file by its hash before, and a patch by its text', () => {
    expect(contentOf({ path: 'a.md', change: 'modified', before: 'x', after: 'y', marks: [] })).toBe('y');
    expect(contentOf({ path: 'a.md', change: 'deleted', before: 'x', after: null, marks: [] })).toBe('deleted:x');
    expect(contentOf({ path: 'a.md', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n+a' }))
      .toBe(contentOf({ path: 'a.md', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n+a' }));
    expect(contentOf({ path: 'a.md', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n+a' }))
      .not.toBe(contentOf({ path: 'a.md', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n+b' }));
  });
});

describe('a file\'s two versions compared', () => {
  it('draws the lines kept, removed and added, numbered on each side', () => {
    const { lines, whole } = lineDiff('one\ntwo\nthree\n', 'one\n2\nthree\nfour\n');
    expect(whole).toBe(true);
    expect(lines).toEqual([
      { kind: 'same', text: 'one', old: 1, new: 1 },
      { kind: 'del', text: 'two', old: 2 },
      { kind: 'add', text: '2', new: 2 },
      { kind: 'same', text: 'three', old: 3, new: 3 },
      { kind: 'add', text: 'four', new: 4 },
    ]);
  });

  it('draws an added file as added lines, and one too large to compare as removed and added', () => {
    expect(lineDiff(null, 'a\nb').lines).toEqual([{ kind: 'add', text: 'a', new: 1 }, { kind: 'add', text: 'b', new: 2 }]);
    const big = Array.from({ length: 3000 }, (_, at) => `line ${at}`).join('\n');
    const other = Array.from({ length: 3000 }, (_, at) => `row ${at}`).join('\n');
    const { whole, lines } = lineDiff(big, other);
    expect(whole).toBe(false);
    expect(lines).toHaveLength(6000);
  });
});
