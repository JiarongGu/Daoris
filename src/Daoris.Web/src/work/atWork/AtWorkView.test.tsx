import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { AtWorkView } from './AtWorkView';
import { AT_WORK_CHILD_STATES } from './types';
import { CHILD_STATE_KEY, PART_STATES, partStateKey } from './words';
import {
  BOUND, CODEX, CODEX_NONE, ELSEWHERE, EMPTY, FAILED, IDS_ONLY, LOST, NOW, PARTS, READING, STALE, THREE, TURN_WAITS, UNCERTAIN, UNREADABLE,
} from './atWork.fixtures';

// COWORKVIEW1a: who is at work, drawn from the read's answer (the cowork view design §5.2, rules 15 to 19).

describe('who is at work on a session', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws every child state and every record state by a key both catalogues hold', () => {
    for (const state of AT_WORK_CHILD_STATES) {
      const key = CHILD_STATE_KEY[state];
      expect([state, key in en, key in zh]).toEqual([state, true, true]);
    }
    for (const state of PART_STATES) expect([state, partStateKey(state) in en, partStateKey(state) in zh]).toEqual([state, true, true]);
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
    const { container, rerender } = render(<AtWorkView held={STALE} now={NOW} />);
    expect(screen.getByText(/Could not read again: the service timed out/)).toBeTruthy();
    expect(container.querySelector('.opacity-60')).toBeTruthy();
    expect(screen.getByText('Read the parser')).toBeTruthy();

    rerender(<AtWorkView held={{ ...(EMPTY as object), stale: { why: 'refused' } } as never} />);
    expect(screen.queryByText('Nobody else is working on this')).toBeNull();
  });

  it('draws a chat’s three subagents with their states and ends, the heading counting the one working', () => {
    render(<AtWorkView held={THREE} now={NOW} />);

    expect(screen.getByText('1 working')).toBeTruthy();
    expect(screen.getByText('working')).toBeTruthy();
    expect(screen.getByText(/^completed \d\d:\d\d$/)).toBeTruthy();
    expect(screen.getByText(/^failed \d\d:\d\d$/)).toBeTruthy();
    expect(screen.queryByText('its turn waits for them')).toBeNull();
  });

  it('says its turn waits for them on the protocol door', () => {
    render(<AtWorkView held={TURN_WAITS} />);
    expect(screen.getByText('its turn waits for them')).toBeTruthy();
  });

  it('says nobody only where every source was read and holds nobody', () => {
    const { rerender } = render(<AtWorkView held={EMPTY} />);
    expect(screen.getByText('Nobody else is working on this')).toBeTruthy();

    for (const held of [CODEX_NONE, IDS_ONLY, UNREADABLE, ELSEWHERE, PARTS]) {
      rerender(<AtWorkView held={held} />);
      expect(screen.queryByText('Nobody else is working on this')).toBeNull();
    }
    rerender(<AtWorkView held={CODEX_NONE} />);
    expect(screen.getByText('Its agent’s subagents appear here only when they call Daoris.'.replace('’', "'"))).toBeTruthy();
    rerender(<AtWorkView held={IDS_ONLY} />);
    expect(screen.getByText('its hook also named 2 agent ids it could not match')).toBeTruthy();
    expect(screen.getByText('2 agent ids it named could not be tied to a process')).toBeTruthy();
    rerender(<AtWorkView held={ELSEWHERE} />);
    expect(screen.getByText('its subagents are not visible on this machine')).toBeTruthy();
  });

  it('says a child not known, one whose agent ended and one first seen with its last or first moment, none of them working', () => {
    const { rerender } = render(<AtWorkView held={UNCERTAIN} now={NOW} />);
    expect(screen.getByText(/^not known whether it still runs · last seen \d\d:\d\d$/)).toBeTruthy();
    expect(screen.getByText('1 working')).toBeTruthy();

    rerender(<AtWorkView held={LOST} now={NOW} />);
    expect(screen.getByText(/^lost when its agent ended \d\d:\d\d$/)).toBeTruthy();
    expect(screen.getByText(/^its agent ended · last seen \d\d:\d\d$/)).toBeTruthy();

    rerender(<AtWorkView held={CODEX} now={NOW} />);
    expect(screen.getByText(/^first seen \d\d:\d\d · its end is not said$/)).toBeTruthy();
  });

  it('draws the bound’s count of what it left out and what it folded', () => {
    render(<AtWorkView held={BOUND} />);
    expect(screen.getByText('41 working')).toBeTruthy();
    expect(screen.getByText('1 more not drawn, 1 of them working')).toBeTruthy();
    expect(screen.getByText('3 ended')).toBeTruthy();
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

  it('offers a child’s console only while the window holds its stream, and opens that stream on a press', async () => {
    const open = vi.fn();
    const held = (key: string) => (key === 'a1' ? 's1/subagent/a1' : undefined);
    render(<AtWorkView held={THREE} now={NOW} streamOf={held} onOpenStream={open} />);

    const consoles = screen.getAllByRole('button', { name: /^Console/ });
    expect(consoles).toHaveLength(1);
    fireEvent.click(consoles[0]!);
    expect(open).toHaveBeenCalledWith('s1/subagent/a1');

    cleanup();
    await i18n.changeLanguage('zh');
    render(<AtWorkView held={THREE} now={NOW} streamOf={held} onOpenStream={open} />);
    expect(screen.getAllByRole('button', { name: /^控制台/ })).toHaveLength(1);
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<AtWorkView held={LOST} now={NOW} />);
    expect(screen.getByText(/^其智能体结束时丢失 \d\d:\d\d$/)).toBeTruthy();
    expect(screen.getByText(/^其智能体已结束 · 最后出现于 \d\d:\d\d$/)).toBeTruthy();
    rerender(<AtWorkView held={EMPTY} />);
    expect(screen.getByText('没有其他成员参与')).toBeTruthy();
    rerender(<AtWorkView held={BOUND} />);
    expect(screen.getByText('另有 1 个未显示，其中 1 个在工作')).toBeTruthy();
  });
});
