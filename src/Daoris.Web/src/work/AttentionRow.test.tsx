import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type Attention, AttentionRow } from './AttentionRow';

const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  repository: 'engine',
  since: '2026-09-21T09:00:00Z',
  detail: 'Two ways forward; I recommend the second.',
};

describe('a row in what needs you', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-21T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('says what it is, where, which kind of waiting, and for how long', () => {
    render(<AttentionRow item={PARKED} />);

    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('parked at a checkpoint')).toBeInTheDocument();
    expect(screen.getByText('waiting 3h ago')).toBeInTheDocument();
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
  });

  it('names the other kind by what is actually wrong with it', () => {
    render(<AttentionRow
      item={{ ...PARKED, kind: 'unanswerable', detail: '`retired` is not on this register.' }}
    />);

    expect(screen.getByText('nobody here can take this')).toBeInTheDocument();
  });

  it('renders without a detail — a park that said nothing is still worth a row', () => {
    render(<AttentionRow item={{ ...PARKED, detail: null }} />);
    expect(screen.getByRole('button')).toBeInTheDocument();
  });
});

/** No pinned clock: nothing below reads a duration, and userEvent's own waits are real. */
describe('opening one', () => {
  it('is a door, and hands back the whole item so the caller knows where to go', async () => {
    const open = vi.fn();
    render(<AttentionRow item={PARKED} onOpen={open} />);

    await userEvent.click(screen.getByRole('button'));
    expect(open).toHaveBeenCalledWith(PARKED);
  });

  it('stays inert where there is nowhere to go — knowing is the half that travels', async () => {
    render(<AttentionRow item={PARKED} />);
    await userEvent.click(screen.getByRole('button'));
    expect(screen.getByRole('button')).toBeInTheDocument();
  });
});
