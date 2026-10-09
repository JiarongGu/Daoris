import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { ConversationFind } from './ConversationFind';

// SESS1 S9: the way through a long run — the jumps and the search — props in, presses out.

const draw = (props: Partial<Parameters<typeof ConversationFind>[0]> = {}) => {
  const calls = { failure: vi.fn(), words: vi.fn(), query: vi.fn(), step: vi.fn() };
  render(
    <ConversationFind
      hasFailure
      onFirstFailure={calls.failure}
      hasWords
      onLastWords={calls.words}
      query=""
      onQuery={calls.query}
      onStep={calls.step}
      {...props}
    />,
  );
  return calls;
};

describe('the way through a long run', () => {
  it('jumps to the first failure and to the last words', async () => {
    const calls = draw();

    await userEvent.click(screen.getByRole('button', { name: 'First failure' }));
    await userEvent.click(screen.getByRole('button', { name: 'Last words' }));

    expect(calls.failure).toHaveBeenCalledOnce();
    expect(calls.words).toHaveBeenCalledOnce();
  });

  it('offers no jump to a failure a run never had, nor to words it never said', () => {
    draw({ hasFailure: false, hasWords: false });

    expect(screen.queryByRole('button', { name: 'First failure' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Last words' })).toBeNull();
  });

  it('says where it is among the places found, and steps through them, Enter included', async () => {
    const calls = draw({ query: 'gates', hits: 12, at: 2 });

    expect(screen.getByText('3 of 12')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Next match' }));
    await userEvent.click(screen.getByRole('button', { name: 'Previous match' }));
    await userEvent.type(screen.getByRole('searchbox', { name: 'find in this session' }), '{Enter}');
    await userEvent.type(screen.getByRole('searchbox', { name: 'find in this session' }), '{Shift>}{Enter}{/Shift}');

    expect(calls.step.mock.calls).toEqual([[1], [-1], [1], [-1]]);
  });

  // IME1: words found through an input method are composed, and the Enter that accepts a candidate steps nowhere.
  it('steps nowhere on an Enter an input method is composing with', () => {
    const calls = draw({ query: '闸门', hits: 3, at: 0 });
    const box = screen.getByRole('searchbox', { name: 'find in this session' });

    expect(fireEvent.keyDown(box, { key: 'Enter', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(box, { key: 'Enter', keyCode: 229 })).toBe(true);
    expect(calls.step).not.toHaveBeenCalled();

    fireEvent.keyDown(box, { key: 'Enter' });
    expect(calls.step).toHaveBeenCalledWith(1);
  });

  it('says so when nothing is found, and when the host left places out', () => {
    const { unmount } = render(
      <ConversationFind hasFailure={false} onFirstFailure={() => {}} hasWords={false} onLastWords={() => {}}
        query="nowhere" onQuery={() => {}} hits={0} onStep={() => {}} />,
    );
    expect(screen.getByText('nothing found')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next match' })).toBeDisabled();
    unmount();

    draw({ query: 'gates', hits: 100, at: 0, cut: true });
    expect(screen.getByText('1 of 100+')).toBeInTheDocument();
  });

  it('claims nothing about a search too short to ask', () => {
    draw({ query: 'g', hits: 0 });

    expect(screen.queryByText('nothing found')).toBeNull();
  });
});
