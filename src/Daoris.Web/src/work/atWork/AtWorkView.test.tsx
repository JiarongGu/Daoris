import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { AtWorkView } from './AtWorkView';
import { PART_STATES, partStateKey } from './words';
import { CODEX_NONE, ELSEWHERE, EMPTY, FAILED, IDS_ONLY, PARTS, READING, STALE, THREE, UNREADABLE } from './atWork.fixtures';

// Who is at work, drawn from the read's answer.

describe('who is at work on a session', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws every record state by a key both catalogues hold', () => {
    for (const state of PART_STATES) expect([state, partStateKey(state) in en, partStateKey(state) in zh]).toEqual([state, true, true]);
  });

  it('draws no subagents of its own: the session\'s timeline draws each under the call that spawned it (D320 point 9)', () => {
    render(<AtWorkView held={THREE} />);
    expect(screen.queryByText('Subagents')).toBeNull();
    expect(screen.queryByText('1 working')).toBeNull();
    expect(screen.queryByRole('button', { name: /^Console/ })).toBeNull();
  });

  it('reads before an answer, never nobody and never a zero', () => {
    const { rerender, container } = render(<AtWorkView held={undefined} />);
    expect(screen.getByText('Reading who is at work…')).toBeTruthy();
    expect(container.querySelector('[aria-busy="true"]')).toBeTruthy();
    rerender(<AtWorkView held={READING} />);
    expect(screen.queryByText('Nobody else is working on this')).toBeNull();
  });

  it('says a failure with its words and Try again, and a refusal of the bridge with nothing held the same way', () => {
    const retry = vi.fn();
    const { rerender } = render(<AtWorkView held={FAILED} onRetry={retry} />);
    expect(screen.getByText(/Who is at work could not be read: the sessions could not be read/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledOnce();

    rerender(<AtWorkView held={undefined} error="the window closed" />);
    expect(screen.getByText(/Who is at work could not be read: the window closed/)).toBeTruthy();
  });

  it('keeps a stale answer’s rows dimmed under its words, and never says nobody while stale', () => {
    const { container, rerender } = render(<AtWorkView held={{ ...(PARTS as object), stale: (STALE as { stale: unknown }).stale } as never} />);
    expect(screen.getByText(/Could not read again: the service timed out/)).toBeTruthy();
    expect(container.querySelector('.opacity-60')?.textContent).toContain('codex');

    rerender(<AtWorkView held={{ ...(EMPTY as object), stale: { why: 'refused' } } as never} />);
    expect(screen.queryByText('Nobody else is working on this')).toBeNull();
  });

  it('says nobody only where every source was read and holds nobody', () => {
    const { rerender } = render(<AtWorkView held={EMPTY} />);
    expect(screen.getByText('Nobody else is working on this')).toBeTruthy();

    for (const held of [THREE, CODEX_NONE, IDS_ONLY, UNREADABLE, ELSEWHERE, PARTS]) {
      rerender(<AtWorkView held={held} />);
      expect(screen.queryByText('Nobody else is working on this')).toBeNull();
    }
  });

  it('lists the entry’s parts with their own state, a teammate’s as not visible here, each opening its part', () => {
    const open = vi.fn();
    render(<AtWorkView held={PARTS} onOpen={open} />);

    expect(screen.getByText('completed')).toBeTruthy();
    expect(screen.getByText('2 subagents · 0 working')).toBeTruthy();
    expect(screen.getByText('2 more parts')).toBeTruthy();
    fireEvent.click(screen.getByText('codex'));
    expect(open).toHaveBeenCalledWith('s0');
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<AtWorkView held={EMPTY} />);
    expect(screen.getByText('没有其他成员参与')).toBeTruthy();
  });
});
