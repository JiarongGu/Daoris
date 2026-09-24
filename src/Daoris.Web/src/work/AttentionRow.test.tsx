import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type Attention, AttentionRow } from './AttentionRow';

const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  where: 'engine',
  since: '2026-09-21T09:00:00Z',
  detail: 'Two ways forward; I recommend the second.',
};

/** An ask the declarations proposed and nobody has settled (INT4d) — it waits in a circle. */
const PROPOSAL: Attention = {
  id: '7c1e9a04b2d5',
  kind: 'proposal',
  title: 'The chunk streamer stalls on a cold cache.',
  where: 'aurora',
  since: '2026-09-21T09:00:00Z',
  detail: 'The declarations propose engine, game. Nothing is published until you choose.',
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

  /** A bare workspace name among repository names reads as one more repository. */
  it('names an ask by what it waits for, and its place as a workspace', () => {
    render(<AttentionRow item={PROPOSAL} />);

    expect(screen.getByText('proposed, not yet published')).toBeInTheDocument();
    expect(screen.getByText('workspace aurora')).toBeInTheDocument();
    expect(screen.getByText(/propose engine, game/)).toBeInTheDocument();
  });

  it('names an ask whose intake parked asking by that', () => {
    render(<AttentionRow item={{ ...PROPOSAL, kind: 'intake', detail: 'published nothing.' }} />);
    expect(screen.getByText('its intake asked you')).toBeInTheDocument();
  });

  it('renders without a detail — a park that said nothing is still worth a row', () => {
    render(<AttentionRow item={{ ...PARKED, detail: null }} onOpen={() => {}} />);
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

  /**
   * A door opens something, or it is not a door (platform language §4): a browser's parked row was a
   * button that did nothing. Knowing is the half that travels, so the row is still there — as text.
   */
  it('is no door where there is nowhere to go, and still says what is waiting', () => {
    render(<AttentionRow item={PARKED} />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
  });
});
