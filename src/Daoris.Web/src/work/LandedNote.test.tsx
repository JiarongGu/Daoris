import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import '../i18n';
import type { LandedWork } from './diff';
import { LandedNote } from './LandedNote';

// REVIEW2 (D113): where a landed session's work went, as its review says it. Props in, sentences out — every
// state below is reached without a bridge, a repository or a git process (components plan §2).

const LANDED: LandedWork = {
  branch: 'feature/0fda18-fix-the-api-gap',
  repository: 'engine',
  line: 'main',
  landedAt: '2026-10-01T09:30:00.0000000+00:00',
  plugin: null,
  pushed: false,
  pullRequest: null,
  state: 'standing',
  asLanded: true,
  reads: null,
  removed: null,
  detail: null,
};

const show = (landed: LandedWork, source: 'tree' | 'branch' = 'branch') => render(<LandedNote landed={landed} source={source} />);

describe('LandedNote', () => {
  it('says the branch the work landed on, and that the changes are read from it once the tree is gone', () => {
    show(LANDED);

    const note = screen.getByRole('region', { name: 'where this work landed' });
    expect(note.textContent).toMatch(/landed on feature\/0fda18-fix-the-api-gap/);
    expect(screen.getByText('feature/0fda18-fix-the-api-gap', { selector: 'code' })).toBeTruthy();
    expect(note.textContent).toMatch(/Its tree is gone, so the changes below are read from that branch in engine's own checkout/);
    // When, in the reader's own way of writing a moment.
    expect(note.querySelector('time')?.getAttribute('dateTime')).toBe(LANDED.landedAt);
    expect(screen.queryByRole('link')).toBeNull();
  });

  it('offers the pull request a plugin opened as a link', () => {
    show({ ...LANDED, plugin: 'example.lands', pushed: true, pullRequest: 'https://example.test/org/engine/pull/7' });

    expect(screen.getByRole('link', { name: 'open the pull request' })).toHaveAttribute('href', 'https://example.test/org/engine/pull/7');
  });

  it('says a standing branch whose tree is still here is not accepted again', () => {
    show(LANDED, 'tree');

    expect(screen.getByRole('region').textContent).toMatch(/still stands, so the work is not accepted again/);
    expect(screen.getByRole('region').textContent).not.toMatch(/read from that branch/);
  });

  it('says a branch gone since, what removed it, and whether its work reads on the line', () => {
    show({
      ...LANDED, state: 'gone',
      removed: { kind: 'on-line', where: 'origin/main', at: '2026-10-01T11:00:00Z' },
      reads: { kind: 'on-line', where: 'main', files: [], detail: null },
    });

    const note = screen.getByRole('region').textContent;
    expect(note).toMatch(/That branch is gone from engine now\./);
    expect(note).toMatch(/The clean-up removed it once its work read on origin\/main\./);
    expect(note).toMatch(/Its work reads on main\./);
  });

  it('names the files a gone branch changed that read otherwise on the line', () => {
    show({ ...LANDED, state: 'gone', reads: { kind: 'differs', where: 'main', files: ['a.ts', 'b.ts'], detail: null } });

    expect(screen.getByRole('region').textContent).toMatch(/2 files it changed read otherwise on main: a\.ts, b\.ts\./);
  });

  it('does not guess once git no longer holds the commits, and passes git\'s words through where it could not say', () => {
    show({ ...LANDED, state: 'gone', reads: { kind: 'commits-gone', where: null, files: [], detail: null } });
    expect(screen.getByRole('region').textContent).toMatch(/whether its work reads on the line cannot be said/);

    show({ ...LANDED, state: 'gone', removed: { kind: 'inside', where: 'feature/q2', at: null }, reads: { kind: 'unknown', where: null, files: [], detail: 'git said so' } });
    expect(screen.getAllByRole('region')[1]!.textContent).toMatch(/removed it inside feature\/q2, whose work read on the line\. Git could not say whether its work reads on the line: git said so/);
  });

  it('says a branch rebased or replaced since is not shown, and a repository with no checkout here', () => {
    show({ ...LANDED, state: 'not-ours' });
    expect(screen.getByRole('region').textContent).toMatch(/no longer holds the commit the landing made it at/);

    show({ ...LANDED, state: 'no-checkout' });
    expect(screen.getAllByRole('region')[1]!.textContent).toMatch(/engine has no checkout on this machine/);
  });

  it('says why a standing branch\'s changes could not be read, and no moment where the record kept none', () => {
    show({ ...LANDED, landedAt: null, detail: 'git found no point where `feature/x` left its line, so there is no range to read.' });

    const note = screen.getByRole('region');
    expect(note.querySelector('time')).toBeNull();
    expect(note.textContent).toMatch(/Its changes could not be read from that branch: git found no point/);
  });
});
