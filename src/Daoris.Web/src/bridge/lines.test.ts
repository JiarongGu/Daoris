import { describe, expect, it } from 'vitest';
import { linesSlice } from './lines';

// The lines slice's page half (PUSH1b2, D182): one partition per repository, composed into the seven arrays `LINES` answers.

const part = (repository: string, branch: string | null, extra: Record<string, unknown> = {}) => ({
  lines: [{ repository, workspace: 'default', branch, source: branch ? 'repository' : 'none', ...extra }],
  landings: [{ repository, workspace: 'default' }],
  languages: [], reviews: [], opinions: [], helpers: [], commits: [],
});

describe('the lines slice (PUSH1b2)', () => {
  it('is the lines entry, named as the shell names it', () => {
    expect(linesSlice.name).toBe('lines');
    expect(linesSlice.key).toEqual(['driver', 'lines']);
  });

  it("composes every repository's rows into the seven arrays, in the repositories' order", () => {
    const composed = linesSlice.compose(new Map([['zeta', part('zeta', 'main')], ['alpha', part('alpha', 'dev')]])) as
      { lines: { repository: string }[]; landings: { repository: string }[]; commits: unknown[] };
    expect(composed.lines.map((line) => line.repository)).toEqual(['alpha', 'zeta']);
    expect(composed.landings.map((line) => line.repository)).toEqual(['alpha', 'zeta']);
    expect(composed.commits).toEqual([]);
  });

  it('keeps a line not read yet as the flag says, and answers nothing while no repository is judged', () => {
    const composed = linesSlice.compose(new Map([['alpha', part('alpha', null, { notReadYet: true })]])) as
      { lines: { notReadYet?: boolean }[] };
    expect(composed.lines[0].notReadYet).toBe(true);
    expect(linesSlice.compose(new Map())).toBeUndefined();
  });

  it('answers nothing for a partition of another shape, which leaves the pull', () => {
    expect(linesSlice.compose(new Map([['alpha', 'x']]))).toBeUndefined();
  });
});
