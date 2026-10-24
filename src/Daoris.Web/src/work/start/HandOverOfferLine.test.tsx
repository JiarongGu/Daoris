import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { HandOverOfferLine } from './HandOverOfferLine';

// MIXAGENT1b-ii (D163 point 3) and MODELROLE1e (D205 §3.5(e)): Daoris's offer to hand a session over says why, and where the session's
// actor has an order of its own, which of its choices it would go on with and the model that choice asks.
const props = { agentName: 'Codex', accountName: 'team', onGoOn: vi.fn(), onChoose: vi.fn(), onDismiss: vi.fn() };
const base = { why: 'limit' as const, until: '2026-10-12T21:30:00Z', agent: 'codex-acp', account: 'team' };

afterEach(cleanup);

describe('HandOverOfferLine', () => {
  it('names the actor’s next choice, its agent and its model, beside why', () => {
    render(<HandOverOfferLine {...props} actorName="Build" offer={{ ...base, choice: { n: '2', model: 'gpt-5' } }} />);

    expect(screen.getByText('Build’s next choice is 2: Codex · gpt-5.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Go on with Codex (team)' })).toBeTruthy();
  });

  it('says account default where the choice asks no model', () => {
    render(<HandOverOfferLine {...props} actorName="Chat" offer={{ ...base, choice: { n: '3', model: null } }} />);

    expect(screen.getByText('Chat’s next choice is 3: Codex · account default.')).toBeTruthy();
  });

  it('names no choice where the roster’s order stands', () => {
    render(<HandOverOfferLine {...props} actorName="Build" offer={base} />);

    expect(screen.queryByText(/next choice/)).toBeNull();
  });
});
