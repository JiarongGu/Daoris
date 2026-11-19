import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import '../../locales/i18n';
import { WaitCard } from './WaitCard';
import { BARE_WAIT, RELEASE_WAIT } from './session.fixtures';

// The one wait card: what it is, what yes and no each do, where to look, how it is known, who asked on which model; one press
// answers it, and a field its asker left out says so rather than leaving a blank.

const NOW = Date.parse('2026-11-20T15:18:00Z');

describe('WaitCard', () => {
  it('says what it is, what yes and no do, where to look, how it is known and who asked', () => {
    render(<WaitCard wait={RELEASE_WAIT} now={NOW} onAnswer={() => {}} />);
    const card = screen.getByRole('article', { name: /Release 2.4 to beta/ });
    for (const said of [
      RELEASE_WAIT.what, RELEASE_WAIT.yes!.effect, RELEASE_WAIT.no!.effect, 'release notes', 'run #418', 'checks 46/46 at 13:40',
      'release manager', 'sonnet-4.5', 'infra waits; the docs go on', 'holds 2',
    ]) expect(within(card).getAllByText(new RegExp(said.replace(/[.*+?^${}()|[\]\\#]/g, '\\$&'))).length).toBeGreaterThan(0);
  });

  it('answers yes and no with one press each', () => {
    const onAnswer = vi.fn();
    render(<WaitCard wait={RELEASE_WAIT} now={NOW} onAnswer={onAnswer} />);
    fireEvent.click(screen.getByRole('button', { name: 'Approve release to beta' }));
    fireEvent.click(screen.getByRole('button', { name: 'Hold' }));
    expect(onAnswer.mock.calls).toEqual([['yes'], ['no']]);
  });

  it('answers an option by its place, and says each field its asker left out', () => {
    const onAnswer = vi.fn();
    render(<WaitCard wait={BARE_WAIT} now={NOW} onAnswer={onAnswer} />);
    fireEvent.click(screen.getByRole('button', { name: 'Both' }));
    expect(onAnswer).toHaveBeenCalledWith({ option: 1 });
    expect(screen.getAllByText('not said by its asker')).toHaveLength(2);
  });

  it('folds to one line that opens it', () => {
    const onOpen = vi.fn();
    render(<WaitCard wait={RELEASE_WAIT} now={NOW} onAnswer={() => {}} layout="line" onOpen={onOpen} />);
    expect(screen.queryByText(RELEASE_WAIT.what)).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: /Release 2.4 to beta/ }));
    expect(onOpen).toHaveBeenCalled();
  });
});
