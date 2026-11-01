import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Answered } from '../../work/InlineConfirm';
import { GATES_CHANGED, INTEGRATION_ACCEPTED, INTEGRATION_CHANGED, INTEGRATION_UNACCEPTED, START } from '../fixtures';
import { PlaceGates } from './PlaceGates';

// AGENTSPLACE1h (the Agents-place design §3): Gates shows the declaration once, with two choices, each naming its purpose, blob and standing,
// and one confirmation applying the ones chosen, each by the blob that was read. A molecule: every state is reached by its props.

const landedWaits = GATES_CHANGED.landed;

/** An acceptance that answers as the driver does: kept, or refused in a sentence. */
const keeps = () => vi.fn((answered: Answered) => answered.done());
const keepsIntegration = () => vi.fn((_request: unknown, answered: Answered) => answered.done());

// The language goes back inside `act`, with the page still drawn (FRAMEACT1).
afterEach(async () => { await act(async () => { await i18n.changeLanguage('en'); }); });

describe("a place's Gates", () => {
  const draw = (extra: Partial<Parameters<typeof PlaceGates>[0]> = {}) => {
    const landed = keeps();
    const integration = keepsIntegration();
    render(<PlaceGates landed={landedWaits} integration={INTEGRATION_CHANGED} onAcceptLanded={landed} onAcceptIntegration={integration} {...extra} />);
    return { landed, integration };
  };
  const row = (purpose: 'landed' | 'integration') => document.querySelector(`[data-purpose="${purpose}"]`) as HTMLElement;

  it('shows each declaration once, names each purpose, the blob and the standing it leaves, and says a changed landed gate stops verification', () => {
    draw();

    expect(screen.getAllByLabelText("daoris.gates.json at main's tip aa11bb2")).toHaveLength(2);

    expect(within(row('landed')).getByText('Accept for landed verification')).toBeInTheDocument();
    expect(row('landed')).toHaveTextContent('verification stops until you accept it again');
    expect(row('landed')).toHaveTextContent('Blob 0e5751c at main\'s tip aa11bb2');
    expect(within(row('integration')).getByText('Accept for integration')).toBeInTheDocument();
    // Changed integration goes on under the last accepted blob.
    expect(row('integration')).toHaveTextContent('integration goes on under blob 3b18e51 until you accept this one');
  });

  it('applies both choices on one confirmation, each by the blob it read', async () => {
    const { landed, integration } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Accept the chosen…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(landed).toHaveBeenCalledOnce();
    expect(integration).toHaveBeenCalledWith({ gates: '0e5751c026e543b2e8ab2eb06099daa1d1e5df47' }, expect.anything());
  });

  it('applies only the ones chosen, and offers nothing while none is', async () => {
    const { landed, integration } = draw();

    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for landed verification' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept the chosen…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));
    expect(landed).not.toHaveBeenCalled();
    expect(integration).toHaveBeenCalledOnce();
  });

  it('is disabled while no choice is made', async () => {
    draw();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for landed verification' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for integration' }));

    expect(screen.getByRole('button', { name: 'Accept the chosen…' })).toBeDisabled();
    expect(screen.getByText('Choose what to accept, then confirm.')).toBeInTheDocument();
  });

  it("says a refusal for its own purpose and leaves the other's acceptance standing", async () => {
    const landed = keeps();
    const integration = vi.fn((_request: unknown, answered: Answered) => answered.refused('engine\'s line moved: read the declaration again.'));
    render(<PlaceGates landed={landedWaits} integration={INTEGRATION_CHANGED} onAcceptLanded={landed} onAcceptIntegration={integration} />);

    await userEvent.click(screen.getByRole('button', { name: 'Accept the chosen…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(landed).toHaveBeenCalledOnce();
    expect(await screen.findByRole('alert')).toHaveTextContent("Accept for integration: engine's line moved: read the declaration again.");
    // The confirmation stays, so the refused one can be pressed again.
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
  });

  it('withdraws the integration acceptance on its own, never the landed route', async () => {
    const { landed, integration } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Withdraw the integration acceptance' }));

    expect(integration).toHaveBeenCalledWith({ clear: true }, expect.anything());
    expect(landed).not.toHaveBeenCalled();
  });

  it('offers no choice for what stands accepted, and draws no declaration', () => {
    draw({ landed: START.landed, integration: INTEGRATION_ACCEPTED });

    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Accept the chosen…' })).toBeNull();
    expect(row('landed')).toHaveTextContent('Accepted: blob 3b18e51 at main\'s tip aa11bb2.');
    expect(row('integration')).toHaveTextContent('Accepted: blob 3b18e51 at main\'s tip aa11bb2.');
    expect(document.querySelector('pre')).toBeNull();
  });

  it('draws a declaration two choices share a single time', () => {
    const shared = '{\n  "gates": []\n}';
    draw({
      landed: { ...landedWaits, declaration: shared },
      integration: { ...INTEGRATION_UNACCEPTED, declaration: shared },
    });

    expect(document.querySelectorAll('pre')).toHaveLength(1);
  });

  it('offers no choice a route without verification does not have, and says when nothing asks', () => {
    draw({ landed: { workflow: 'current', level: 'current', gates: null, state: 'none' }, integration: null });

    expect(row('landed')).toBeNull();
    expect(screen.getByText(/No gates here ask for your acceptance/)).toBeInTheDocument();
  });

  it('offers no press where the page is handed none', () => {
    draw({ onAcceptLanded: undefined, onAcceptIntegration: undefined });

    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says its words in Chinese', async () => {
    await act(async () => { await i18n.changeLanguage('zh'); });
    draw();

    expect(screen.getByRole('heading', { name: '检查' })).toBeInTheDocument();
    expect(within(row('landed')).getByText('采纳，用于落地验证')).toBeInTheDocument();
    expect(within(row('integration')).getByText('采纳，用于集成')).toBeInTheDocument();
  });
});
