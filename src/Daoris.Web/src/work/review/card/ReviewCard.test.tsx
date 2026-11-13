import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import i18n from '../../../locales/i18n';
import type { NodeWordAnswer, NodeWordAsk } from '../../../workflow/run';
import { BRANCH_OUTPUTS, BRANCH_REVIEW, FOLDER_REVIEW } from '../run/runReviewFixtures';
import { ReviewCard } from './ReviewCard';

// The Accept card, props in: bound to the attempt and revision it draws, one press to Accept, Send back with words, Stop asked once; a
// newer revision supersedes it and a stale press is said `moved`, never retargeted.

const kept = (ask: NodeWordAsk): Promise<NodeWordAnswer> => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: true });
const viewed = { isViewed: () => false, mark: vi.fn() };

describe('the Accept card', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says what is accepted, its outputs and what follows, and accepts in one press with its attempt and revision', async () => {
    const onWord = vi.fn(kept);
    render(<ReviewCard answer={BRANCH_OUTPUTS} viewed={viewed} onWord={onWord} />);
    const card = screen.getByRole('group', { name: 'Your Accept · budget-notes' });
    expect(card.textContent).toContain('Accept the commits on daoris/q41.');
    expect(card.textContent).toContain('Changes, 2 files (0 viewed)');
    expect(card.textContent).toContain('Then: land');
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({
      run: 'q41', node: 'review', act: 'accept', attempt: 2, revision: 'b7c1e09a44f2',
    })));
  });

  it('says a stale press moved and offers the newer revision, never pressing it', async () => {
    const onWord = vi.fn((ask: NodeWordAsk) => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: false, refused: 'moved' }));
    const onOpenOutputs = vi.fn();
    render(<ReviewCard answer={BRANCH_OUTPUTS} viewed={viewed} onWord={onWord} onOpenOutputs={onOpenOutputs} />);
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }));
    await screen.findByText('The work moved since r2, so nothing was accepted. Read the newer revision.');
    fireEvent.click(screen.getByRole('button', { name: 'Read the newer revision' }));
    expect(onOpenOutputs).toHaveBeenCalledWith(null);
    expect(onWord).toHaveBeenCalledTimes(1);
  });

  it('takes no press once a newer revision supersedes it', () => {
    render(<ReviewCard answer={BRANCH_OUTPUTS} viewed={viewed} onWord={vi.fn(kept)} supersededBy={3} />);
    expect(screen.getByText('Superseded by r3: this card takes no press.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });

  it('sends back with the person\'s words to the part chosen', async () => {
    const onWord = vi.fn(kept);
    render(<ReviewCard answer={BRANCH_OUTPUTS} viewed={viewed} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Send back…' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'What should change' }), { target: { value: 'Name each total.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Send back' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({ act: 'changes', words: 'Name each total.', part: 'q42' })));
  });

  it('asks once before it stops the run', async () => {
    const onWord = vi.fn(kept);
    render(<ReviewCard answer={FOLDER_REVIEW} viewed={viewed} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(onWord).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Stop the run' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({ act: 'stop', whole: true })));
  });

  it('holds the supervisor\'s decision as its held state', () => {
    render(<ReviewCard answer={BRANCH_REVIEW} viewed={viewed} onWord={vi.fn(kept)} onOverrule={vi.fn()} />);
    expect(screen.getByRole('button', { name: /Overrule/ })).toBeTruthy();
  });

  it('says what it accepts in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<ReviewCard answer={BRANCH_OUTPUTS} viewed={viewed} onWord={vi.fn(kept)} />);
    expect(screen.getByRole('group', { name: '等你采纳 · budget-notes' }).textContent).toContain('采纳 daoris/q41 上的提交。');
    expect(screen.getByRole('button', { name: '采纳' })).toBeTruthy();
  });
});
