import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { HelpPressNow, StandingConfirmationNow } from '../person/standing';
import { HelpPressLine } from './HelpPressLine';

// A press a rule refused before any run or card is listed in Notifications as refused, with the words it met: the growth switch's
// are the route's own, naming the row that turns it back on, so an `off` that keeps refusing is seen.

const OFF = 'Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).';
const press = (id: string, more: Partial<HelpPressNow> = {}): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route: 'REGISTRY_CREATE', session: 's1a2b3c4', scope: null, target: null, at: '2026-11-12T10:00:00Z', outcome: 'refused', ...more,
});
const standing = (helpPresses: HelpPressNow[]): StandingConfirmationNow =>
  ({ holds: [], holders: [], stewardActs: [], stewardDesk: [], stewardLands: [], helpPresses });

describe('a press the growth switch refused, in Notifications', () => {
  it('is listed as refused with the switch named in its words', () => {
    render(<HelpPressLine standing={standing([press('h1', { why: OFF }), press('h2', { route: 'SET_HOLD', outcome: 'done' })])} onOpen={vi.fn()} onReverse={vi.fn()} defaultOpen />);

    const rows = within(screen.getByRole('list', { name: 'Presses Ask Daoris made' })).getAllByRole('listitem');
    expect(rows[0]!.querySelector('[data-help-outcome="refused"]')).not.toBeNull();
    expect(within(rows[0]!).getByText(OFF)).toBeInTheDocument();
    expect(rows[1]!.querySelector('[data-help-why]')).toBeNull();
  });
});
