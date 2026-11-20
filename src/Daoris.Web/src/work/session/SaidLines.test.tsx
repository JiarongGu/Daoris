import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../../locales/i18n';
import type { Turn } from '../conversation/conversationTypes';
import { type Said, SaidDoors } from '../conversation/saidDoors';
import { storyOf } from './story';
import { StoryStream } from './StoryStream';

// A driver's note and the person's held words in the story keep the doors the conversation offered: *Say it again* for words a
// restart may have lost, the cooling account's reset on held words, and *Go on in a new session* under them.

const ACTOR = { maker: 'Claude', model: 'opus-4.1' };
const AT = '2026-11-20T10:00:00Z';

const drawn = (turns: Turn[], doors: Partial<Said>) => render(
  <SaidDoors.Provider value={{ chat: false, reasons: {}, ...doors }}>
    <StoryStream story={storyOf({ turns, steps: [], exchanges: [], rows: [] })} actor={ACTOR} />
  </SaidDoors.Provider>,
);

describe('the story\'s said lines', () => {
  it('offers Say it again for the person\'s own words a restart may have lost', () => {
    const onSayAgain = vi.fn();
    drawn([{
      key: 't1', items: [{ key: 'n1', kind: 'note', at: AT, code: 'lost', persons: true, said: ['Run the burst tests.'], text: 'lost' }],
    } as unknown as Turn], { onSayAgain });
    fireEvent.click(screen.getByRole('button', { name: 'Say it again' }));
    expect(onSayAgain).toHaveBeenCalledWith(['Run the burst tests.']);
  });

  it('says held words wait for the cooling account, and offers to go on in a new session', () => {
    const onGoOnNew = vi.fn();
    drawn([{
      key: 't1', waiting: true,
      items: [{ key: 'h1', kind: 'held', at: AT, text: 'Then land it.', reaches: 'resume' }],
    } as unknown as Turn], { cooling: { until: '2026-11-20T15:00:00Z', onGoOnNew } });
    expect(screen.getByText(/its account is cooling until/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Go on in a new session' }));
    expect(onGoOnNew).toHaveBeenCalled();
  });
});
