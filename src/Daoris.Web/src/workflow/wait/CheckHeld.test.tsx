import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../../locales/i18n';
import type { RunStep } from '../run';
import { CheckHeld } from './CheckHeld';

const HELD = {
  step: { id: 'gates' }, node: 'check', state: 'not-known', acts: ['stop'],
  held: { unit: 'run:q1', attempt: 1, words: ['again', 'stop'], resolution: null },
} as unknown as RunStep;

describe('a check held unknown for the person', () => {
  it('sends the epoch of the tenure its row read with each word, so a word read before a hand-over is refused', async () => {
    const onHeld = vi.fn().mockResolvedValue({ done: true });
    render(<CheckHeld step={{ ...HELD, epoch: 3 } as RunStep} onHeld={onHeld} />);

    await userEvent.click(screen.getByRole('button', { name: 'Run again' }));
    expect(onHeld).toHaveBeenCalledWith({ unit: 'run:q1', node: 'gates', attempt: 1, resolution: 'again', epoch: 3 });
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onHeld).toHaveBeenLastCalledWith({ unit: 'run:q1', node: 'gates', attempt: 1, resolution: 'stop', epoch: 3 });
  });

  it('names no epoch where its row read none', async () => {
    const onHeld = vi.fn().mockResolvedValue({ done: true });
    render(<CheckHeld step={HELD} onHeld={onHeld} />);

    await userEvent.click(screen.getByRole('button', { name: 'Run again' }));
    expect(onHeld).toHaveBeenCalledWith({ unit: 'run:q1', node: 'gates', attempt: 1, resolution: 'again' });
  });
});
