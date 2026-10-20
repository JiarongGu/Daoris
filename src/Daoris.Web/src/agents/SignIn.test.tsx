import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Signing in, on the row (2026-09-23): what the tool says arrives as console lines, and the two
// things the person can do with it — paste the code, or stop — land on the driver module.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../locales/i18n';
import { SignIn } from './SignIn';

const ID = 'claude-code:login';

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SignIn id={ID} harness="claude-code" profile="work" />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The tool speaks: lines arrive as the host's push, keyed by the action's own id, in sequence. */
let sequence = 1;
async function said(...texts: string[]) {
  await act(async () => {
    eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
      session: ID,
      lines: texts.map((text) => ({ sequence: ++sequence, text })),
    });
  });
}

describe('signing in on the row', () => {
  beforeEach(() => {
    // The action has no backlog (nothing buffers a harness action): the tail is the command line
    // the host echoes, and everything after it arrives as a push.
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: ID, lines: [{ sequence: 1, text: '$ claude auth login' }], sequence: 1, live: true, dropped: 0 }
      : {}));
  });
  afterEach(() => {
    invoke.mockReset();
    sequence = 1;
  });

  /**
   * 🔴 Measured on the real binary with no console: a hyperlink, then `Paste code here if
   * prompted >` with no newline, then a wait on stdin. The steps follow what has actually been said.
   */
  it('offers the link when it appears, the code box when asked, and sends what is pasted', async () => {
    show();
    expect(await screen.findByText(/Waiting for the tool/)).toBeTruthy();
    // The tail is in before the tool speaks, so what it says joins one stream.
    await userEvent.click(screen.getByText('The tool\'s own output'));
    await screen.findByText(/claude auth login/);
    // Nothing to paste into until the tool asks — a box before that would be a guess.
    expect(screen.queryByLabelText('sign-in code')).toBeNull();

    // The other flow the binary takes (with a stdin): it opens the browser and waits for the
    // callback, printing no link and asking for no code. The step says so rather than waiting.
    await said('Opening browser to sign in…');
    expect(await screen.findByText(/opened your browser/)).toBeTruthy();

    await said("If the browser didn't open, visit: https://x.test/oauth?code=true");
    const link = await screen.findByRole('link', { name: /x\.test/ });
    expect(link.getAttribute('href')).toBe('https://x.test/oauth?code=true');
    expect(screen.queryByLabelText('sign-in code')).toBeNull();

    await said('Paste code here if prompted >');
    await userEvent.type(await screen.findByLabelText('sign-in code'), 'abc-123');
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_INPUT', {
      payload: { harness: 'claude-code', action: 'login', text: 'abc-123' },
    });
    // Sent once: the box goes, so a second paste cannot land on a tool that is finishing.
    expect(await screen.findByText(/Code sent/)).toBeTruthy();
    expect(screen.queryByLabelText('sign-in code')).toBeNull();
  });

  /** A login nobody is going to finish is stopped from here — not by closing the window. */
  it('can be stopped', async () => {
    show();
    await userEvent.click(await screen.findByRole('button', { name: /Cancel/ }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_CANCEL', {
      payload: { harness: 'claude-code', action: 'login' },
    });
  });

  /**
   * Signing in to ANOTHER account (D66 §3): the same three steps, with no account to name yet — and
   * the stop reaches the sign-in that is actually running, under its own action.
   */
  it('signs in to another account under its own action, naming the tool rather than an account', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id="claude-code:login-new" harness="claude-code" action="login-new" />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Signing in to another claude-code account')).toBeTruthy();
    expect(screen.getByText(/under who signed in/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: /Cancel/ }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_CANCEL', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
  });

  /**
   * CODEXACCT2: Codex signs in by its device code, since its browser sign-in's callback port is reserved on some machines.
   * It prints a link and a one-time code to enter on that page (the words measured with 0.160.0, the driver's colours
   * stripped; the code invented), opens no browser and asks nothing on its input. The link is offered as Claude Code's
   * is, the code is shown to copy, and there is no box to paste into.
   */
  it('offers a device code\'s link and the code to enter there, with no box to paste into', async () => {
    const id = 'codex-acp:login-new';
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: id, lines: [{ sequence: 1, text: '$ codex login --device-auth' }], sequence: 1, live: true, dropped: 0 }
      : {}));
    const writeText = vi.fn(() => Promise.resolve());
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id={id} harness="codex-acp" action="login-new" tool="Codex" />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    expect(await screen.findByText('Signing in to another Codex account')).toBeTruthy();
    await userEvent.click(screen.getByText('The tool\'s own output'));
    await screen.findByText(/codex login --device-auth/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: id,
        lines: [
          '', 'Welcome to Codex [v0.160.0]', "OpenAI's command-line coding agent", '',
          'Follow these steps to sign in with ChatGPT using device code authorization:', '',
          '1. Open this link in your browser and sign in to your account', '   https://auth.openai.com/codex/device', '',
          '2. Enter this one-time code (expires in 15 minutes)', '   ABCD-12345', '',
          'Device codes are a common phishing target. Never share this code.',
        ].map((text) => ({ sequence: ++sequence, text })),
      });
    });

    const link = await screen.findByRole('link', { name: /auth\.openai\.com/ });
    expect(link.getAttribute('href')).toBe('https://auth.openai.com/codex/device');
    // Nothing opened by itself: the step says to open the link, not that a page opened.
    expect(screen.getByText('Open the link and sign in.')).toBeTruthy();
    expect(screen.getByText('Enter this code on that page.')).toBeTruthy();
    expect(screen.getByText('ABCD-12345')).toBeTruthy();
    expect(screen.queryByLabelText('sign-in code')).toBeNull();
    expect(screen.queryByText(/The box appears when the tool asks/)).toBeNull();

    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    await userEvent.click(screen.getByRole('button', { name: 'Copy code' }));
    expect(writeText).toHaveBeenCalledWith('ABCD-12345');
    expect(await screen.findByRole('button', { name: 'copied' })).toBeTruthy();
    // The link's own copy is still the link.
    await userEvent.click(screen.getByRole('button', { name: 'Copy link' }));
    expect(writeText).toHaveBeenLastCalledWith('https://auth.openai.com/codex/device');
  });

  /**
   * CODEXSIGNIN3: the code is read by its shape, not by one version's words — a link to the device page, then a code of letters
   * and digits in groups joined by a hyphen — so a newer pin that rewords the sentence asking for it still shows it, and until
   * a code is printed the step says it comes later.
   */
  it('reads the device code by its shape from a reworded output, and says code later until one is printed', async () => {
    const id = 'codex-acp:login-new';
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: id, lines: [{ sequence: 1, text: '$ codex login --device-auth' }], sequence: 1, live: true, dropped: 0 }
      : {}));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id={id} harness="codex-acp" action="login-new" tool="Codex" />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    expect(await screen.findByText('Signing in to another Codex account')).toBeTruthy();
    const send = async (...texts: string[]) => act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({ session: id, lines: texts.map((text) => ({ sequence: ++sequence, text })) });
    });

    await send('Welcome to Codex [v0.200.0]', 'Open this page and sign in:', '   https://auth.openai.com/codex/device', '');
    await screen.findByRole('link', { name: /auth\.openai\.com/ });
    expect(screen.getByText(/The box appears when the tool asks/)).toBeTruthy();

    await send('Then type the code below (valid for 15 minutes):', '   WXYZ-98765', 'Never share this code. A one-time-use token.');
    expect(await screen.findByText('WXYZ-98765')).toBeTruthy();
    expect(screen.getByText('Enter this code on that page.')).toBeTruthy();
    expect(screen.queryByText(/The box appears when the tool asks/)).toBeNull();
  });

  /**
   * CODEXSIGNIN2: a start the driver refused is said in this panel, whole, with the ways on: try again or close. The tool's
   * steps and its cancel are not shown, since nothing is running to cancel.
   */
  it('says a refused start in its panel, with Try again and Close', async () => {
    const retry = vi.fn();
    const close = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id={ID} harness="claude-code" profile="work" refused="Another sign-in is running." onRetry={retry} onClose={close} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    expect((await screen.findByRole('alert')).textContent).toBe('Another sign-in is running.');
    expect(screen.queryByRole('button', { name: /Cancel/ })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(close).toHaveBeenCalledTimes(1);
  });

  it('says a refused start with its ways on in Chinese too',async () => {
    await i18n.changeLanguage('zh');
    try {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <SignIn id={ID} harness="claude-code" profile="work" refused="已有登录在进行。" onRetry={() => {}} onClose={() => {}} />
          </Tooltip.Provider>
        </QueryClientProvider>,
      );
      expect((await screen.findByRole('alert')).textContent).toBe('已有登录在进行。');
      expect(screen.getByRole('button', { name: '重试' })).toBeTruthy();
      expect(screen.getByRole('button', { name: '关闭' })).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** The tool's own words are one disclosure away, never the surface. */
  it('keeps the raw output behind a disclosure', async () => {
    show();
    await said('Opening browser to sign in…');
    expect(await screen.findByText('The tool\'s own output')).toBeTruthy();
    expect(screen.getByText('The tool\'s own output').closest('details')?.open).toBe(false);
  });
});

/**
 * SIGNIN1 (the UI/UX design §3.2, R2): the sign-in says its phase and how long. The phases are the driver's, each a line of
 * its relay that begins with the record separator and names the phase and its start; the link, the code and the ask stay
 * read from the tool's lines by their shape. An older host writes no phases, and the panel draws as before with the time
 * since the press.
 */
describe('signing in, phase by phase', () => {
  const START = Date.parse('2026-10-20T09:00:00.000Z');
  const CODEX = 'codex-acp:login-new';
  const phase = (name: string, at: number) => `\u001e${JSON.stringify({ phase: name, at: new Date(at).toISOString() })}`;
  let next = 1;
  const push = async (id: string, ...texts: string[]) => act(async () => {
    eventHandlers.get('DAORIS.SESSION_OUTPUT')!({ session: id, lines: texts.map((text) => ({ sequence: ++next, text })) });
  });
  const codex = (props: Partial<Parameters<typeof SignIn>[0]> = {}) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id={CODEX} harness="codex-acp" action="login-new" tool="Codex" {...props} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
  };
  const stateOf = (name: RegExp) => screen.getAllByRole('listitem').find((item) => name.test(item.textContent ?? ''))!.textContent;

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(START + 48_000);
    next = 1;
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: CODEX, lines: [], sequence: 0, live: true, dropped: 0 }
      : {}));
  });
  afterEach(() => {
    vi.useRealTimers();
    invoke.mockReset();
  });

  it('draws Codex\'s device code under its browser phase, then the driver\'s own phases, each with its time', async () => {
    codex();
    await push(CODEX, phase('start', START));
    expect(await screen.findByText('Starting Codex')).toBeTruthy();
    expect(stateOf(/^Starting Codex/)).toMatch(/now/);
    expect(stateOf(/^Checking who signed in/)).toMatch(/to come/);
    // The whole's time, from the driver's start, and the phase now's: here the same.
    expect(screen.getAllByText('48s so far')).toHaveLength(2);

    await push(CODEX,
      phase('browser', START + 1_000), '$ codex login --device-auth', '',
      'Follow these steps to sign in with ChatGPT using device code authorization:', '',
      '1. Open this link in your browser and sign in to your account', '   https://auth.openai.com/codex/device', '',
      '2. Enter this one-time code (expires in 15 minutes)', '   ABCD-12345', '');
    expect(stateOf(/^Starting Codex/)).toMatch(/done.*1s/);
    expect(stateOf(/^Waiting on your browser/)).toMatch(/waits on you.*47s so far/);
    expect(screen.getByText('Open the page and enter this code:')).toBeTruthy();
    expect(screen.getByText('ABCD-12345')).toBeTruthy();
    // The tool's own bound, from when it was printed: fifteen minutes, forty-seven seconds gone.
    expect(screen.getByText('Expires in 14m')).toBeTruthy();
    expect(screen.getByRole('link', { name: /auth\.openai\.com/ }).getAttribute('href')).toBe('https://auth.openai.com/codex/device');
    expect(screen.getByRole('status').textContent).toBe('Waiting on your browser, waits on you');

    await push(CODEX, 'Successfully logged in', phase('who', START + 47_000));
    expect(stateOf(/^Waiting on your browser/)).toMatch(/done.*46s/);
    expect(stateOf(/^Checking who signed in/)).toMatch(/now/);
    expect(screen.queryByText('ABCD-12345')).toBeNull();

    await push(CODEX, phase('lists', START + 48_000));
    expect(stateOf(/^Joining the lists/)).toMatch(/now/);
    // A phase line is the driver's, never the tool's words.
    await userEvent.click(screen.getByText('The tool\'s own output'));
    expect(screen.getByText(/codex login --device-auth/).textContent).not.toMatch(/phase|\u001e/);
  });

  it('reads a run\'s phases after its own start, so a panel that stayed up through a second run reads that one', async () => {
    codex();
    await push(CODEX, phase('start', START), phase('browser', START + 1_000), phase('start', START + 40_000));

    expect(await screen.findAllByText('8s so far')).toHaveLength(2);
    expect(stateOf(/^Starting Codex/)).toMatch(/now/);
    expect(stateOf(/^Waiting on your browser/)).toMatch(/to come/);
  });

  it('says a phase that failed with its sentence whole, the earlier ones done, and offers Try again and Close', async () => {
    const retry = vi.fn();
    const close = vi.fn();
    codex({ failed: 'Sign-in did not finish (the tool exited 1), so nothing was kept. Its output says why.', onRetry: retry, onClose: close });
    await push(CODEX, phase('start', START), phase('browser', START + 1_000));

    expect((await screen.findByRole('alert')).textContent).toBe('Sign-in did not finish (the tool exited 1), so nothing was kept. Its output says why.');
    expect(stateOf(/^Starting Codex/)).toMatch(/done/);
    expect(stateOf(/^Waiting on your browser/)).toMatch(/failed/);
    expect(screen.queryByRole('button', { name: /Cancel/ })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect([retry.mock.calls.length, close.mock.calls.length]).toEqual([1, 1]);
  });

  it('says a refused start as its first phase failed, with the driver\'s sentence whole', async () => {
    codex({ refused: '`codex` is pinned to 0.160.0 on this machine, and nothing is installed at that version.' });

    expect((await screen.findByRole('alert')).textContent).toBe('codex is pinned to 0.160.0 on this machine, and nothing is installed at that version.');
    expect(stateOf(/^Starting Codex/)).toMatch(/failed/);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });

  it('from an older host that writes no phases, draws its steps as before with the time since the press', async () => {
    codex({ pressedAt: START + 30_000 });
    await push(CODEX, '$ codex login --device-auth');

    expect(await screen.findByText('Waiting for the tool to open the page…')).toBeTruthy();
    expect(screen.getByText('18s so far')).toBeTruthy();
    expect(screen.queryByText('Starting Codex')).toBeNull();
  });

  /**
   * CODEXLOGIN1, the browser with its port free: `codex login` 0.160.0 prints where it listens, then the page to open, then the
   * other way (D125's note; the page's query values replaced). The link offered is the maker's page, never the local server's
   * address, and the panel says the tool opened the browser; once the tool exits signed in, the driver's own phases follow.
   */
  it('offers the browser sign-in\'s page, never its local server, and says the tool opened the browser', async () => {
    codex({ other: 'device-code', onSwitch: () => {} });
    await push(CODEX,
      phase('start', START), phase('browser', START + 1_000), '$ codex login',
      'Starting local login server on http://localhost:1455.', 'If your browser did not open, navigate to this URL to authenticate:', '',
      'https://auth.openai.com/oauth/authorize?response_type=code&client_id=CLIENT&state=STATE', '',
      'On a remote or headless machine? Use `codex login --device-auth` instead.');

    const link = await screen.findByRole('link', { name: /auth\.openai\.com/ });
    expect(link.getAttribute('href')).toBe('https://auth.openai.com/oauth/authorize?response_type=code&client_id=CLIENT&state=STATE');
    expect(screen.queryByRole('link', { name: /localhost/ })).toBeNull();
    expect(screen.getByText(/opened your browser/)).toBeTruthy();
    expect(screen.queryByText('Open the page and enter this code:')).toBeNull();

    await push(CODEX, 'Successfully logged in', phase('who', START + 30_000));
    expect(stateOf(/^Waiting on your browser/)).toMatch(/done/);
    expect(stateOf(/^Checking who signed in/)).toMatch(/now/);
  });

  /**
   * CODEXLOGIN1, the other way beside the one running: a device code while the browser waits, the browser while a device code
   * waits, with a word on why, since a workspace that turns device codes off says so only on the maker's page while the tool
   * waits out its fifteen minutes. The press is the organism's, which stops the one running and starts the other.
   */
  it('offers the other way while either waits, and says why under a device code', async () => {
    const onSwitch = vi.fn();
    const { unmount } = codex({ other: 'device-code', onSwitch });
    await push(CODEX, phase('start', START), phase('browser', START + 1_000), 'https://auth.openai.com/oauth/authorize?state=STATE');
    await userEvent.click(await screen.findByRole('button', { name: 'Use a device code' }));
    expect(onSwitch).toHaveBeenCalledTimes(1);
    expect(screen.queryByText(/turns device codes off/)).toBeNull();
    unmount();

    codex({ other: 'browser', onSwitch });
    await push(CODEX, phase('start', START), phase('browser', START + 1_000),
      '1. Open this link in your browser and sign in to your account', '   https://auth.openai.com/codex/device',
      '2. Enter this one-time code (expires in 15 minutes)', '   ABCD-12345');
    expect(await screen.findByText('ABCD-12345')).toBeTruthy();
    expect(screen.getByText('A workspace that turns device codes off says so only on that page, and this one keeps waiting.')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Use the browser' }));
    expect(onSwitch).toHaveBeenCalledTimes(2);
  });

  /**
   * CODEXLOGIN1, the port held: the driver refused the browser sign-in before it started, its sentence naming each holder; the
   * panel says it whole and offers the device code first, focused, with *Try again* and *Close* beside it.
   */
  it('says a held port whole and offers the device code first', async () => {
    const onSwitch = vi.fn();
    const held = '`codex login` waits for your browser on port 1455, else 1457, and neither can be taken now: 1455 is held by '
      + 'another program; 1457 is held by a range Windows reserves, which a restart may move. Nothing was signed in — sign in '
      + 'with a device code instead.';
    codex({ refused: held, other: 'device-code', onSwitch, onRetry: () => {}, onClose: () => {} });

    expect((await screen.findByRole('alert')).textContent).toBe(held.replace('`codex login`', 'codex login'));
    const device = screen.getByRole('button', { name: 'Use a device code' });
    expect(document.activeElement).toBe(device);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Close' })).toBeTruthy();
    await userEvent.click(device);
    expect(onSwitch).toHaveBeenCalledTimes(1);
  });

  /**
   * CODEXLOGIN1, a sign-in the account refused: where the tool says why on a line of its own (`Error logging in…:`, as 0.160.0
   * ends a refused one), the panel says that line beside its own sentence, and offers the other way.
   */
  it('says the tool\'s own refusal line and offers the other way', async () => {
    codex({
      failed: 'Sign-in did not finish (the tool exited 1). Its output says why.', failedAt: START + 5_000,
      other: 'browser', onSwitch: () => {}, onRetry: () => {}, onClose: () => {},
    });
    await push(CODEX, phase('start', START), phase('browser', START + 1_000), '$ codex login --device-auth',
      'Error logging in with device code: device code login is not enabled for this Codex server. Use the browser login or verify the server URL.');

    expect(await screen.findByText('Codex said: Error logging in with device code: device code login is not enabled for this Codex '
      + 'server. Use the browser login or verify the server URL.')).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Use the browser' }));
  });

  /**
   * CODEXLOGIN1, the switch mid-sign-in: the panel stays up through both runs, and reads the latest one after its own start,
   * so the browser's page from the first run is never offered beside the device code of the second.
   */
  it('reads the latest run\'s lines after a switch, never the first run\'s page', async () => {
    codex({ other: 'browser', onSwitch: () => {} });
    await push(CODEX, phase('start', START), phase('browser', START + 1_000),
      'https://auth.openai.com/oauth/authorize?state=STATE', 'this way was stopped for the other, so nothing was kept from it.',
      phase('start', START + 20_000), phase('browser', START + 21_000),
      '1. Open this link in your browser and sign in to your account', '   https://auth.openai.com/codex/device',
      '2. Enter this one-time code (expires in 15 minutes)', '   WXYZ-98765');

    expect(await screen.findByText('WXYZ-98765')).toBeTruthy();
    expect(screen.getByRole('link', { name: /auth\.openai\.com/ }).getAttribute('href')).toBe('https://auth.openai.com/codex/device');
    expect(screen.queryByRole('link', { name: /oauth\/authorize/ })).toBeNull();
  });

  it('offers the other way in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    try {
      codex({ other: 'browser', onSwitch: () => {} });
      await push(CODEX, phase('start', START), phase('browser', START + 1_000), '   https://auth.openai.com/codex/device', '   ABCD-12345');
      expect(await screen.findByRole('button', { name: '改用浏览器' })).toBeTruthy();
      expect(screen.getByText('关闭了设备代码的工作区只会在那个页面上说明，而这里会一直等待。')).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('names its phases in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      codex();
      await push(CODEX, phase('start', START), phase('browser', START + 1_000), '   https://auth.openai.com/codex/device');
      expect(await screen.findByText('正在启动 Codex')).toBeTruthy();
      expect(screen.getByText('等你在浏览器中完成')).toBeTruthy();
      expect(screen.getByText('正在确认登录的是谁')).toBeTruthy();
      expect(screen.getByText('正在加入列表')).toBeTruthy();
      expect(screen.getByText('已用 48 秒')).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
