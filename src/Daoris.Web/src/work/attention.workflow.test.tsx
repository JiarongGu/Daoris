import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { rowRun } from './attention';
import { type Attention, AttentionRow } from './AttentionRow';

// WORKFLOW1c (the workflow design §7): *What needs you*'s row for a step that waits on the person opens that step, in the run of
// the session it names or its quest's newest here. A row that is no step of a run has no such door.

const row = (over: Partial<Attention> & Pick<Attention, 'kind'>): Attention => ({
  id: 'x1', title: 'Fix the header', where: 'engine', since: '2026-10-09T09:00:00Z', ...over,
});

describe("a row's door into its run", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the session or the quest of each kind that is a step waiting on the person', () => {
    expect(rowRun(row({ kind: 'parked', id: 's1' }))).toEqual({ session: 's1' });
    expect(rowRun(row({ kind: 'review', id: 's2' }))).toEqual({ session: 's2' });
    expect(rowRun(row({ kind: 'go-ahead', id: 'a1#1', session: 's3' }))).toEqual({ session: 's3' });
    expect(rowRun(row({ kind: 'set-up', id: 'q2', session: 's4' }))).toEqual({ session: 's4', quest: 'q2' });
    expect(rowRun(row({ kind: 'set-up', id: 'q2', session: null }))).toEqual({ session: null, quest: 'q2' });
    expect(rowRun(row({ kind: 'departure', id: 'q3' }))).toEqual({ quest: 'q3' });
    expect(rowRun(row({ kind: 'parked-quest', id: 'q4' }))).toEqual({ quest: 'q4' });
  });

  it('names none for a row that is no step of a run', () => {
    for (const kind of ['proposal', 'intake', 'trust', 'rule', 'unanswerable', 'account-wait', 'signed-out'] as const) {
      expect(rowRun(row({ kind }))).toBeNull();
    }
    // A go-ahead whose asking session the ask did not name has none either.
    expect(rowRun(row({ kind: 'go-ahead', session: null }))).toBeNull();
  });

  it('is a quiet door of its own beside the row’s, which opens the run', async () => {
    const onRun = vi.fn();
    const onOpen = vi.fn();
    render(<ul><AttentionRow item={row({ kind: 'departure', id: 'q3' })} onOpen={onOpen} onRun={onRun} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Workflow' }));
    expect(onRun).toHaveBeenCalledOnce();
    expect(onOpen).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('title', 'Open where this work stands in its workflow');
  });

  it('draws no such door where none is handed', () => {
    render(<ul><AttentionRow item={row({ kind: 'departure', id: 'q3' })} onOpen={vi.fn()} /></ul>);
    expect(screen.queryByRole('button', { name: 'Workflow' })).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<ul><AttentionRow item={row({ kind: 'parked', id: 's1' })} onRun={vi.fn()} /></ul>);
    expect(screen.getByRole('button', { name: '工作流' })).toBeInTheDocument();
  });
});
