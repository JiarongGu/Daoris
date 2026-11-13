import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { BRANCH_NEWER, BRANCH_OUTPUTS, PLAN_REVIEW } from '../review/run/runReviewFixtures';
import { OutputsView } from './OutputsView';
import { pinnedAttempt } from './OutputsPage';

// The Outputs view, props in: what the review record presents, the change set first as one scroll of commits and file hunks, the
// handbacks as documents, an unknown display raw; a newer revision is a banner naming the outputs that moved, never swapped in.

const marks = { isViewed: () => false, mark: vi.fn() };

describe('the Outputs view', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the changes before the handbacks, under the revision it reads', () => {
    render(<OutputsView answer={BRANCH_OUTPUTS} viewed={marks} />);
    const group = screen.getByRole('region', { name: 'Under review · r2' });
    const headings = within(group).getAllByRole('heading', { level: 4 }).map((heading) => heading.textContent);
    expect(headings[0]).toMatch(/^Changes/);
    expect(headings.slice(1)).toEqual(['Handback · Tidy the budget notes', 'Handback · Check the totals']);
  });

  it('draws each file\'s hunks in one scroll, and a commit narrows the files to its own', () => {
    render(<OutputsView answer={BRANCH_OUTPUTS} viewed={marks} />);
    expect(screen.getByText('new summary')).toBeTruthy();
    expect(screen.getByText('total 12')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /Tidy the summary/ }));
    expect(screen.queryByText('total 12')).toBeNull();
    expect(screen.getByText('new summary')).toBeTruthy();
  });

  it('announces a newer revision by the outputs that moved, and reads it only when asked', () => {
    const onRead = vi.fn();
    render(<OutputsView answer={BRANCH_NEWER} viewed={marks} onRead={onRead} />);
    expect(screen.getByRole('status').textContent).toContain('r3 is ready: Changes, Handback · Check the totals changed since this revision.');
    expect(screen.getByRole('region', { name: 'Under review · r2' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Read r3' }));
    expect(onRead).toHaveBeenCalledWith(3);
  });

  it('draws an output of a display it does not know as it was given', () => {
    const odd = { ...PLAN_REVIEW, outputs: [{ id: 'chart', type: 'chart', display: 'chart', title: 'Load chart', digest: 'x' }] };
    render(<OutputsView answer={odd} viewed={marks} />);
    expect(screen.getByText('Drawn as it was given: this window has no display for it.')).toBeTruthy();
  });

  it('names itself and its groups in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<OutputsView answer={BRANCH_OUTPUTS} viewed={marks} />);
    expect(screen.getByRole('region', { name: '产出' })).toBeTruthy();
    expect(screen.getByRole('region', { name: '审阅中 · r2' })).toBeTruthy();
    expect(screen.getByRole('region', { name: '采纳之后' })).toBeTruthy();
  });
});

describe('the attempt the page reads', () => {
  it('pins the attempt first answered, so a newer one is never swapped in', () => {
    expect(pinnedAttempt(0, BRANCH_OUTPUTS)).toBe(2);
    expect(pinnedAttempt(2, BRANCH_NEWER)).toBe(2);
    expect(pinnedAttempt(0, undefined)).toBe(0);
  });
});
