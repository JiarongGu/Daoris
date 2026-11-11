import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { PluginCommandPress, PluginCommandRound } from '../bridge/pluginCommand';
import type { CommandShown } from './interfaces';
import { RoundCard } from './RoundCard';

// The round card: a plugin's command run from the window, round by round, its lines, its page and code, its masked ask, its end,
// and the person's Cancel. The rounds are a stand-in plugin's; the bridge is `send`.

const RUN = '0123456789abcdef0123456789abcdef';

const SIGN_IN: CommandShown = {
  name: 'login', title: { en: 'Sign in', zh: '登录' }, about: 'Signs in to the boards service.', interactive: true, tool: false,
  writes: false, confirm: null, on: [], args: [], adopts: null,
};

const round = (over: Partial<PluginCommandRound>): PluginCommandRound => ({
  run: RUN, said: [], ask: null, open: null, code: null, then: null, data: null, exit: 0, ...over,
});

function card(command: CommandShown, rounds: PluginCommandRound[], over: Partial<Parameters<typeof RoundCard>[0]> = {}) {
  const send = vi.fn(async (_press: PluginCommandPress) => rounds.shift() ?? round({ exit: 2 }));
  const onEnded = vi.fn();
  const onClose = vi.fn();
  render(<RoundCard plugin="acme-boards" pluginName="Acme boards" command={command} place="machine" send={send} onEnded={onEnded} onClose={onClose} {...over} />);
  return { send, onEnded, onClose };
}

describe('the round card', () => {
  it('starts a command that takes nothing at once, and answers a masked ask with the run and the answers whole', async () => {
    const { send, onEnded } = card(SIGN_IN, [
      round({ said: ['Paste a personal access token.'], ask: { name: 'token', prompt: 'Token', masked: true } }),
      round({ said: ['Signed in as the token\'s own identity.'], then: 'reload', exit: 0 }),
    ]);
    await screen.findByText('Paste a personal access token.');
    expect(send).toHaveBeenCalledWith({ id: 'acme-boards', command: 'login', args: {}, answers: {}, place: 'machine', language: 'en' });

    const field = screen.getByLabelText('Token');
    expect(field).toHaveAttribute('type', 'password');
    await userEvent.type(field, 's3cret-value');
    await userEvent.click(screen.getByRole('button', { name: 'Go on' }));

    await screen.findByText('Signed in as the token\'s own identity.');
    expect(send).toHaveBeenLastCalledWith({
      id: 'acme-boards', command: 'login', run: RUN, args: {}, answers: { token: 's3cret-value' }, place: 'machine', language: 'en',
    });
    // Each round's lines stay in order, and the hidden value is never drawn back.
    expect(screen.getByText('Paste a personal access token.')).toBeInTheDocument();
    expect(screen.queryByDisplayValue('s3cret-value')).toBeNull();
    expect(document.body.textContent).not.toContain('s3cret-value');
    expect(screen.getByText('Done.')).toBeInTheDocument();
    expect(onEnded).toHaveBeenCalledWith(expect.objectContaining({ then: 'reload', exit: 0 }));
  });

  it('draws the page to open in the person\'s own browser with its host, and the code with Copy', async () => {
    card(SIGN_IN, [round({
      said: ['Open the page and enter the code.'], open: 'https://sign-in.example/device', code: 'ABCD-EFGH',
      ask: { name: 'signed-in', prompt: 'Press Go on once you have signed in.', masked: false },
    })]);
    const link = await screen.findByRole('link', { name: /Open the page/ });
    expect(link).toHaveAttribute('href', 'https://sign-in.example/device');
    expect(link).toHaveAttribute('data-link', 'system');
    expect(screen.getByText('sign-in.example')).toBeInTheDocument();
    expect(screen.getByText('ABCD-EFGH')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy' })).toBeInTheDocument();
  });

  it('asks a declared confirm and its arguments before anything is sent, each value of its type by name', async () => {
    const start: CommandShown = {
      ...SIGN_IN, name: 'start-stage', title: 'Start a stage', interactive: false, writes: true, confirm: 'Start this stage now?',
      args: [
        { name: 'stage', type: 'text', title: 'Stage', required: true, choices: [] },
        { name: 'attempts', type: 'number', title: 'Attempts', required: false, choices: [] },
      ],
    };
    const { send } = card(start, [round({ said: ['Started.'], exit: 0 })]);
    expect(screen.getByText('Start this stage now?')).toBeInTheDocument();
    const go = screen.getByRole('button', { name: 'Run' });
    expect(go).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/Stage/), 'production');
    await userEvent.type(screen.getByLabelText(/Attempts/), '3');
    expect(send).not.toHaveBeenCalled();
    await userEvent.click(go);
    await screen.findByText('Started.');
    expect(send).toHaveBeenCalledWith(expect.objectContaining({ args: { stage: 'production', attempts: 3 }, answers: {} }));
  });

  it('says an ending other than 0 and what the plugin handed back, and Cancel sends nothing more', async () => {
    card(SIGN_IN, [round({ said: ['The service refused the token.'], data: { signedIn: false }, exit: 3 })]);
    await screen.findByText('It ended with exit 3.');
    expect(screen.getByText(/"signedIn": false/)).toBeInTheDocument();

    const { send, onClose } = card(SIGN_IN, [round({ ask: { name: 'token', prompt: 'Token again', masked: true } })]);
    await screen.findByLabelText('Token again');
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(send).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/Cancelled/)).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: 'Close' }).at(-1)!);
    expect(onClose).toHaveBeenCalled();
  });

  it('says a refused round in the driver\'s own sentence, and asks in the viewer\'s language with the title in it', async () => {
    await i18n.changeLanguage('zh');
    try {
      const send = vi.fn(async () => { throw new Error('Its commands are switched off at that place.'); });
      render(<RoundCard plugin="acme-boards" pluginName="Acme boards" command={SIGN_IN} place="machine" send={send} onEnded={() => {}} onClose={() => {}} />);
      await waitFor(() => expect(screen.getByText('Its commands are switched off at that place.')).toBeInTheDocument());
      expect(send).toHaveBeenCalledWith(expect.objectContaining({ language: 'zh' }));
      expect(screen.getByRole('region', { name: 'Acme boards：登录' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
