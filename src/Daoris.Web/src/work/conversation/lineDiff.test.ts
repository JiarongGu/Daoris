import { describe, expect, it } from 'vitest';
import { diffCounts, LIMIT, lineDiff } from './lineDiff';

describe('lineDiff', () => {
  it('keeps what is common and marks only what changed', () => {
    const diff = lineDiff('fn cap() {\n  let n = 0;\n}', 'fn cap() {\n  let n = 4;\n}');

    expect(diff).toEqual([
      { kind: 'same', text: 'fn cap() {' },
      { kind: 'del', text: '  let n = 0;' },
      { kind: 'add', text: '  let n = 4;' },
      { kind: 'same', text: '}' },
    ]);
    expect(diffCounts(diff)).toEqual({ added: 1, removed: 1 });
  });

  it('reads a new file as all added, and a deleted one as all removed', () => {
    expect(diffCounts(lineDiff(null, 'a\nb'))).toEqual({ added: 2, removed: 0 });
    expect(diffCounts(lineDiff('a\nb', ''))).toEqual({ added: 0, removed: 2 });
  });

  it('treats CRLF and LF as the same line ending', () => {
    expect(diffCounts(lineDiff('a\r\nb', 'a\nb'))).toEqual({ added: 0, removed: 0 });
  });

  /** Past the limit the table is not built: the answer is still true, only less precise. */
  it('reads a very long edit as removed then added rather than building a huge table', () => {
    const long = Array.from({ length: LIMIT + 1 }, (_, i) => `line ${i}`).join('\n');
    const diff = lineDiff(long, 'one');

    expect(diffCounts(diff)).toEqual({ added: 1, removed: LIMIT + 1 });
  });
});
