import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Answered } from '../InlineConfirm';
import { TrustAsk } from './TrustAsk';

// The question the agent asks the first time it runs in a folder, asked by Daoris for a folder the
// driver is holding (D73) — and answered only by the person pressing the grant.

const HOLD = {
  folder: 'C:/somewhere/engine',
  trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json',
  quest: 'q1',
};

const show = (props: Partial<Parameters<typeof TrustAsk>[0]> = {}) =>
  render(<TrustAsk hold={HOLD} onGrant={() => {}} {...props} />);

describe('asking for an agent\'s trust in a folder', () => {
  it('names the folder, what trusting it means, and the one file that is written', () => {
    show();

    expect(screen.getByText('C:/somewhere/engine')).toBeInTheDocument();
    expect(screen.getByText(/permissions\.allow/)).toBeInTheDocument();
    expect(screen.getByText(HOLD.trustFile)).toBeInTheDocument();
    expect(screen.getByText(/nothing else/)).toBeInTheDocument();
  });

  it('says what the driver is holding for it', () => {
    show();
    expect(screen.getByText(/quest #q1/)).toBeInTheDocument();

    show({ hold: { folder: 'C:/somewhere/data/intake/aurora', trustFile: HOLD.trustFile, ask: '7c1e9a' } });
    expect(screen.getByText(/ask #7c1e9a/)).toBeInTheDocument();
  });

  it('grants only when the person presses the grant', async () => {
    const onGrant = vi.fn();
    show({ onGrant });

    expect(onGrant).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    expect(onGrant).toHaveBeenCalledTimes(1);
  });

  it('can be put down, and cannot be pressed twice while the grant is being written', async () => {
    const onCancel = vi.fn();
    show({ onCancel, busy: true });

    expect(screen.getByRole('button', { name: 'Trust this folder' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});

describe('the grant, answered in the question (UXFIX2b3b)', () => {
  it('waits saying so, then says a refusal inside and can be pressed again', async () => {
    let answer: Answered | null = null;
    const onGrant = vi.fn((answered: Answered) => { answer = answered; });
    show({ onGrant, onCancel: () => {} });

    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    expect(screen.getByRole('status')).not.toBeEmptyDOMElement();
    expect(screen.getByRole('button', { name: 'Trust this folder' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Not now' })).toBeDisabled();

    act(() => answer!.refused('The file could not be written.'));
    expect(screen.getByRole('alert')).toHaveTextContent('The file could not be written.');
    expect(screen.getByRole('button', { name: 'Trust this folder' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    expect(onGrant).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('is released when the grant lands', async () => {
    let answer: Answered | null = null;
    show({ onGrant: (answered) => { answer = answered; } });
    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    act(() => answer!.done());
    expect(screen.getByRole('button', { name: 'Trust this folder' })).toBeEnabled();
  });
});

/**
 * POOL1c (D197 point 3; the account-pool design §3 point 4, §4.3): a hold whose file is your own sign-in's offers no press,
 * since Daoris writes nothing there; it shows the folder and says to trust it with the tool itself, at your terminal.
 */
describe('a hold on your own sign-in’s file', () => {
  const OWN = { folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/home/.claude.json', quest: 'q1', own: true };

  it('shows the folder and one sentence, with no grant and no file Daoris would write', async () => {
    const onGrant = vi.fn();
    show({ hold: OWN, onGrant });
    expect(screen.getByText('C:/somewhere/engine')).toBeInTheDocument();
    expect(screen.getByText(
      "This conversation goes on in your own sign-in, outside Daoris's accounts, so Daoris writes nothing in its file: trust the folder with the tool itself, at your terminal.",
    )).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Trust this folder' })).toBeNull();
    expect(screen.queryByText(OWN.trustFile)).toBeNull();
    expect(screen.queryByText(/one flag in the agent's own file/)).toBeNull();
    expect(onGrant).not.toHaveBeenCalled();
  });

  it('can still be put down', async () => {
    const onCancel = vi.fn();
    show({ hold: OWN, onCancel });
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onCancel).toHaveBeenCalled();
  });
});
