import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Answered } from '../../work/InlineConfirm';
import { GATES_CHANGED, INTEGRATION_ACCEPTED, INTEGRATION_CHANGED, START } from '../fixtures';
import { PlaceGates } from './PlaceGates';

// Gates shows the line's declaration as one acceptance for every purpose it serves: what changed in plain words, one press, one withdrawal.
// A molecule: every state is reached by its props.

const landedWaits = GATES_CHANGED.landed;

const keeps = () => vi.fn((_request: unknown, answered: Answered) => answered.done());

// The language goes back inside `act`, with the page still drawn.
afterEach(async () => { await act(async () => { await i18n.changeLanguage('en'); }); });

describe("a place's Gates", () => {
  const draw = (extra: Partial<Parameters<typeof PlaceGates>[0]> = {}) => {
    const accept = keeps();
    render(<PlaceGates landed={landedWaits} integration={INTEGRATION_CHANGED} onAcceptIntegration={accept} {...extra} />);
    return accept;
  };

  it('says what changed as checks added, removed and changed by name with their commands, and no blob id', () => {
    draw();

    expect(screen.getByText(/Added/)).toHaveTextContent('Added comments: node tools/comment-gate.mjs');
    expect(screen.getByText(/Removed/)).toHaveTextContent('Removed old: echo old');
    expect(screen.getByText(/Changed/)).toHaveTextContent('Changed cli: now npm test (it ran npm run verify)');
    expect(document.body.textContent).not.toMatch(/[0-9a-f]{7,}/);
  });

  it('is one acceptance and one press for every purpose: no tick, no second confirmation', async () => {
    const accept = draw();

    expect(screen.queryByRole('checkbox')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(accept).toHaveBeenCalledOnce();
    expect(accept).toHaveBeenCalledWith({ gates: '0e5751c026e543b2e8ab2eb06099daa1d1e5df47' }, expect.anything());
  });

  it('is asked for where only the landed route waits, with the same one press', async () => {
    const accept = draw({ integration: INTEGRATION_ACCEPTED, landed: { ...landedWaits, change: INTEGRATION_CHANGED.change } });

    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(accept).toHaveBeenCalledOnce();
  });

  it("says the driver's refusal under the press", async () => {
    const accept = vi.fn((_request: unknown, answered: Answered) => answered.refused("engine's line moved: read the declaration again."));
    draw({ onAcceptIntegration: accept });

    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("engine's line moved: read the declaration again.");
  });

  it('withdraws the one acceptance', async () => {
    const accept = draw({ integration: INTEGRATION_ACCEPTED, landed: START.landed });

    await userEvent.click(screen.getByRole('button', { name: 'Withdraw' }));

    expect(accept).toHaveBeenCalledWith({ clear: true }, expect.anything());
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });

  it('says when nothing asks', () => {
    draw({ landed: { workflow: 'current', level: 'current', gates: null, state: 'none' }, integration: null });

    expect(screen.getByText(/No gates here ask for your acceptance/)).toBeInTheDocument();
  });

  it('says its words in Chinese', async () => {
    await act(async () => { await i18n.changeLanguage('zh'); });
    draw();

    expect(screen.getByRole('heading', { name: '检查项' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '采纳' })).toBeInTheDocument();
    expect(screen.getByText(/新增/)).toHaveTextContent('新增 comments：node tools/comment-gate.mjs');
  });
});
