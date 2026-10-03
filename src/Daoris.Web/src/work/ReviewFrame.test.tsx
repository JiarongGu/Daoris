import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import { REVIEW_REFUSALS, ReviewFailed, ReviewHead, ReviewReading, SLOW_MS } from './ReviewFrame';

// REVIEW4: the review says what it is doing while it loads. Its frame (what the page knows from the record), the
// skeleton and its words with how long, and a refusal worded from its code with the person's next move. Props in,
// sentences out — every state below is reached without a bridge, a repository or a git process (components plan §2).

const KNOWN = {
  title: 'Fix the report header in report-ui',
  repository: 'report-ui',
  branch: 'daoris/s-2394e5d9',
  base: '0fda18c2b7e4a9d1',
  commits: 4,
  machine: null,
};

const refused = (code: string, parameters: Record<string, string> = { session: 's1a2b3c4' }) =>
  Object.assign(new Error('the host’s English, never shown as such'), { code, parameters });

describe('ReviewHead', () => {
  it('says what the page knows before git answers: the title, the repository, the branch, the base and the commits', () => {
    render(<ReviewHead {...KNOWN} />);

    expect(screen.getByText('Fix the report header in report-ui')).toBeTruthy();
    expect(screen.getByText('report-ui')).toBeTruthy();
    expect(screen.getByText('daoris/s-2394e5d9')).toBeTruthy();
    // The range it will be measured from, short as a commit is named.
    expect(screen.getByText('since 0fda18c2')).toBeTruthy();
    expect(screen.getByText('4 commits')).toBeTruthy();
  });

  it('leaves out what the record does not hold, and says one commit as one', () => {
    render(<ReviewHead title={null} repository="report-ui" branch={null} base={null} commits={1} machine={null} />);

    expect(screen.getByText('report-ui')).toBeTruthy();
    expect(screen.getByText('1 commit')).toBeTruthy();
    expect(screen.queryByText(/since/)).toBeNull();
    expect(screen.queryByText(/daoris\//)).toBeNull();
  });

  /** A newer read in flight holds the last answer; the head says it is reading again, and for how long once it is long. */
  it('says it is reading again while a newer answer is on its way', () => {
    render(<ReviewHead {...KNOWN} refreshing={Date.now() - 12_000} />);

    expect(screen.getByRole('status').textContent).toBe('reading again… 12s');
  });
});

describe('ReviewReading', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  /** What it is doing, at once and on the line the count will take; the skeleton beneath it is for the eye alone. */
  it('says what it is reading at once, and draws the files and the patch as a skeleton', () => {
    const { container } = render(<ReviewReading repository="report-ui" since={Date.now()} />);

    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui…');
    const skeleton = container.querySelector('[data-skeleton="review"]');
    expect(skeleton?.getAttribute('aria-hidden')).toBe('true');
    expect(skeleton?.querySelectorAll('[data-skeleton="file"]').length).toBeGreaterThan(2);
    expect(skeleton?.querySelector('[data-skeleton="patch"]')).toBeTruthy();
  });

  /** For how long, once it is long enough to wonder: nothing for the first moment, then the seconds, counting. */
  it('adds how long it has been reading after the threshold, and keeps counting', () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    render(<ReviewReading repository="report-ui" since={Date.now()} />);

    act(() => vi.advanceTimersByTime(SLOW_MS - 1_000));
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui…');

    act(() => vi.advanceTimersByTime(1_000));
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 2s');

    act(() => vi.advanceTimersByTime(53_000));
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 55s');
  });

  it('counts from when the read began, not from when this pane appeared', () => {
    render(<ReviewReading repository="report-ui" since={Date.now() - 23_000} />);

    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 23s');
  });

  it('says it in 中文, its number set apart', () => {
    render(
      <I18nextProvider i18n={i18n.cloneInstance({ lng: 'zh' })}>
        <ReviewReading repository="report-ui" since={Date.now() - 65_000} />
      </I18nextProvider>,
    );

    expect(screen.getByRole('status').textContent).toBe('正在读取 report-ui 的改动… 1 分 5 秒');
  });

  it('says what it reads without a repository where the page knows none', () => {
    render(<ReviewReading since={Date.now()} />);

    expect(screen.getByRole('status').textContent).toBe('Reading the changes…');
  });
});

describe('ReviewFailed', () => {
  /** Each code the route refuses with, and the bridge's own: what happened, why in the code's words, and what next. */
  it.each([
    ['SESSION_NOT_REVIEWABLE', 'Its work is on another machine', /its record names no working tree here/, /Review it on the machine that ran it/, false],
    ['SESSION_TREE_GONE', 'Its tree is gone', /a clean-up after its landing, or a discard, removed it/, /A landing that made a branch is read from that branch/, false],
    ['SESSION_NO_BASE', 'No range to read', /does not say which commit it started from/, /Its timeline still lists the commits it reported/, false],
    ['SESSION_RANGE_UNREADABLE', 'Git could not read its changes', /git could not read this session's range/, /Read again/, true],
    ['DRIVER_NOT_READY', 'The driver is still starting', /The driver is still coming up/, /reads again by itself once the driver answers/, true],
    ['TIMEOUT', 'The changes took too long to read', /did not answer in time/, /waited 3 minutes for git/, true],
  ] as const)('words %s as what happened, why and what the person can do next', (code, headline, why, next, again) => {
    render(<ReviewFailed error={refused(code)} onRetry={() => {}} />);

    expect(screen.getByText(headline)).toBeTruthy();
    expect(screen.getByText(why).textContent).toMatch(next);
    expect(screen.queryByText(/the host’s English/)).toBeNull();
    expect(Boolean(screen.queryByRole('button', { name: 'Read again' }))).toBe(again);
    expect(REVIEW_REFUSALS[code].again).toBe(again);
  });

  it('names the machine that holds the work, where the record says which', () => {
    render(<ReviewFailed error={refused('SESSION_NOT_REVIEWABLE')} machine="studio-pc" />);

    expect(screen.getByText(/Review it on studio-pc, the machine that ran it, where its tree is/)).toBeTruthy();
  });

  /** A code this page does not know yet is still a sentence and a way on — never a blank, never the raw message. */
  it('words a code it does not know, and an error with no code, as its own and offers to read again', () => {
    const pressed = vi.fn();
    const { unmount } = render(<ReviewFailed error={refused('SOMETHING_NEW')} onRetry={pressed} />);

    expect(screen.getByText('The changes could not be read')).toBeTruthy();
    expect(screen.getByText(/refused, and did not say why/)).toBeTruthy();
    act(() => screen.getByRole('button', { name: 'Read again' }).click());
    expect(pressed).toHaveBeenCalledTimes(1);
    unmount();

    render(<ReviewFailed error={new Error('TypeError: x is undefined')} onRetry={() => {}} />);
    expect(screen.getByText('The changes could not be read')).toBeTruthy();
    expect(screen.queryByText(/TypeError/)).toBeNull();
    expect(screen.getByText(/failed, and did not say why/)).toBeTruthy();
  });

  it('offers no press while one is on its way', () => {
    render(<ReviewFailed error={refused('TIMEOUT')} onRetry={() => {}} retrying />);

    expect(screen.getByRole('button', { name: 'Read again' })).toBeDisabled();
  });

  it('says it in 中文, the two sentences joined without a space', () => {
    render(
      <I18nextProvider i18n={i18n.cloneInstance({ lng: 'zh' })}>
        <ReviewFailed error={refused('DRIVER_NOT_READY')} onRetry={() => {}} />
      </I18nextProvider>,
    );

    expect(screen.getByText('驱动仍在启动')).toBeTruthy();
    expect(screen.getByText(/驱动仍在启动，其服务尚未应答。稍候。驱动应答后，审阅会自动重新读取。/)).toBeTruthy();
    expect(screen.getByRole('button', { name: '重新读取' })).toBeTruthy();
  });
});
