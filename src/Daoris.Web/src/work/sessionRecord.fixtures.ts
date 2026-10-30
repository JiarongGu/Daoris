import type { SessionRecord } from './sessionRecord';

/** A finished session's record as the service kept it (KEEPSIZE1): landed, a ref holding the tip, two commits, one file no commit held. */
export const RECORD: SessionRecord = {
  at: '2026-10-24T10:00:00Z',
  post: {
    branch: 'daoris/s-1', line: 'main', tip: 'c2c2c2c2c2c2', grewFrom: 'c0', ref: 'refs/daoris/tips/s-1', hold: { kind: 'landing', where: 'daoris/land-1' },
    landing: { branch: 'daoris/land-1', line: 'main', at: '2026-10-24T09:30:00Z', pullRequest: null },
    commits: [
      { id: 'a1a1a1a1a1a1', subject: 'Teach the parser dates', files: [{ path: 'src/parse.ts', added: 12, removed: 2 }] },
      { id: 'c2c2c2c2c2c2', subject: 'Cover the leap day', files: [{ path: 'a.ts', added: 1, removed: 0 }, { path: 'b.ts', added: 3, removed: 1 }] },
    ],
    left: { names: ['notes.md'], count: 1, cut: false },
  },
};
