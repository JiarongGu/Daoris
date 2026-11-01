import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { AtWorkAskView } from './AtWorkAskView';
import { ASK_EMPTY, ASK_SAID, ASK_TEAMMATE, ASK_TWO_ENTRIES, CONSIDERED, FAILED, READING } from './atWork.fixtures';

// COWORKVIEW1d: an ask's view (the cowork view design §3.5, §R11.5): its lines of work folded to counts that open their own view, its
// quests as a session's view draws them; empty, reading and failed told apart.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('an ask’s lines of work', () => {
  it('folds each to its parts, working parts and subagents, and opens its own view on a press', () => {
    const open = vi.fn();
    render(<AtWorkAskView held={ASK_TWO_ENTRIES} onOpenEntry={open} />);

    const driver = screen.getByRole('button', { name: /driver/ });
    expect(driver.textContent).toContain('2 parts · 1 working');
    expect(driver.textContent).toContain('3 subagents · 1 working');
    fireEvent.click(driver);
    fireEvent.click(screen.getByRole('button', { name: /docs/ }));
    expect(open.mock.calls).toEqual([['s2'], ['s3']]);
    // A line with no subagent says none of them.
    expect(screen.getByRole('button', { name: /docs/ }).textContent).toContain('1 part · 0 working');
    expect(screen.getByRole('button', { name: /docs/ }).textContent).not.toContain('subagent');
  });

  it('draws the quests it asked below them, as a session’s view draws a quest, with the tick’s reason', () => {
    render(<AtWorkAskView held={ASK_TWO_ENTRIES} considered={CONSIDERED} />);

    const quest = screen.getByRole('button', { name: /Tidy the media lane/ });
    expect(quest.textContent).toContain('not taken yet: The concurrency cap (1) is spent');
    expect(screen.getByText('Asked through quests')).toBeTruthy();
  });

  it('says a teammate’s part’s subagents are not visible on this machine', () => {
    render(<AtWorkAskView held={ASK_TEAMMATE} />);

    expect(screen.getByText('its subagents are not visible on this machine')).toBeTruthy();
  });
});

describe('what is read, what is empty and what failed', () => {
  it('says an ask with nothing at work is that, and never draws the reading skeleton for it', () => {
    const { container } = render(<AtWorkAskView held={ASK_EMPTY} />);

    expect(screen.getByText('Nothing is at work on this ask yet.')).toBeTruthy();
    expect(container.querySelector('[aria-busy]')).toBeNull();
  });

  it('draws reading as a skeleton and never as nothing', () => {
    const { container } = render(<AtWorkAskView held={READING} />);

    expect(container.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(screen.queryByText('Nothing is at work on this ask yet.')).toBeNull();
  });

  it('draws a failure with its words and a way to try again', () => {
    const retry = vi.fn();
    render(<AtWorkAskView held={FAILED} onRetry={retry} />);

    expect(screen.getByRole('alert').textContent).toContain('the sessions could not be read');
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it('draws nothing for a session’s answer: the branches are apart', () => {
    const { container } = render(<AtWorkAskView held={{ answer: { of: { session: 's1' }, attended: 's1', parts: [], subagents: { read: 'none', working: 0, children: [] } } }} />);

    expect(container.textContent).toBe('');
  });

  it('keeps its rows dimmed with the words when a refresh failed over them', () => {
    const { container } = render(<AtWorkAskView held={{ ...(ASK_TWO_ENTRIES as { askAnswer: never }), stale: { why: 'timed out' } }} />);

    expect(screen.getByRole('status').textContent).toContain('Could not read again: timed out');
    expect(container.querySelector('.opacity-60')).not.toBeNull();
  });
});

describe('the ask’s channel, last words', () => {
  it('places a part’s message on the line of work that posted it and the person’s under On the channel', () => {
    const open = vi.fn();
    render(<AtWorkAskView held={ASK_TWO_ENTRIES} channel={{ words: ASK_SAID, onOpen: open }} />);

    const line = screen.getAllByRole('listitem').find((li) => li.textContent?.includes('The core lane is done') && li.querySelector('button'))!;
    expect(within(line).getByRole('button', { name: /driver/ })).toBeTruthy();
    const block = screen.getByText('On the channel').closest('section')!;
    expect(block.textContent).toContain('Start with the index, then the notes.');
    expect(block.textContent).not.toContain('The core lane is done');
    fireEvent.click(screen.getByRole('button', { name: 'All messages' }));
    expect(open).toHaveBeenCalledOnce();
  });

  it('draws no block where the page has no channel', () => {
    render(<AtWorkAskView held={ASK_TWO_ENTRIES} />);

    expect(screen.queryByText('On the channel')).toBeNull();
  });
});

describe('in Chinese', () => {
  it('words the section from the catalogue', async () => {
    await i18n.changeLanguage('zh');
    render(<AtWorkAskView held={ASK_TWO_ENTRIES} channel={{ words: ASK_SAID, onOpen: () => {} }} />);

    expect(screen.getByRole('button', { name: /driver/ }).textContent).toContain('2 段 · 1 段在工作');
    expect(screen.getByText('频道上')).toBeTruthy();
  });

  it('says an ask with nothing at work in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<AtWorkAskView held={ASK_EMPTY} />);

    expect(screen.getByText('这个需求还没有人在工作。')).toBeTruthy();
  });
});
