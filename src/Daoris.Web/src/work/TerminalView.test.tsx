import { StrictMode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// CONSOLE4b (D96): the terminal view over the mocked bridge, held the way the Work frame holds it — the
// terminals in the frame, the view drawing them. The renderer is stood in for: jsdom lays nothing out,
// and what the view owes the person is the traffic, which is asserted here; the look is the window's.

const { invoke, eventHandlers, screens, createScreen } = vi.hoisted(() => {
  type Fake = {
    attached: HTMLElement[]; written: string[]; typed?: (data: string) => void;
    size: { cols: number; rows: number } | null; focused: number; disposed: boolean; looks: number;
  };
  const screens: Fake[] = [];
  return {
    invoke: vi.fn(),
    eventHandlers: new Map<string, (payload: unknown) => void>(),
    screens,
    createScreen: vi.fn(() => {
      const fake: Fake = { attached: [], written: [], size: { cols: 100, rows: 30 }, focused: 0, disposed: false, looks: 0 };
      screens.push(fake);
      return {
        attach: (host: HTMLElement) => { fake.attached.push(host); },
        write: (data: string) => { fake.written.push(data); },
        onData: (listener: (data: string) => void) => {
          fake.typed = listener;
          return () => { if (fake.typed === listener) fake.typed = undefined; };
        },
        fit: () => fake.size,
        look: () => { fake.looks += 1; },
        focus: () => { fake.focused += 1; },
        dispose: () => { fake.disposed = true; },
      };
    }),
  };
});

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));
vi.mock('./terminalScreen', () => ({ createScreen }));

import '../i18n';
import { useTerminals } from '../shell';
import { setThemeChoice } from '../theme';
import { TerminalView } from './TerminalView';

/** The view as the frame holds it: the terminals above, outliving the view. */
function Held({ shown = true, cwd }: { shown?: boolean; cwd?: string }) {
  const terminals = useTerminals();
  return shown ? <TerminalView terminals={terminals} cwd={cwd} /> : null;
}

let next = 0;
/** A fresh id per open, as the module gives one: screens are kept per terminal across the tests' renders. */
const opened = (shell = 'pwsh', cwd = 'C:/somewhere/engine') => ({ id: `t${++next}`, shell, cwd });

const say = (id: string, data: string) => act(() => eventHandlers.get('DAORIS.TERMINAL_OUTPUT')!({ id, data }));
const calls = (type: string) => invoke.mock.calls.filter(([module, kind]) => module === 'DAORIS.TERMINAL' && kind === type);

describe('the terminal view (CONSOLE4b)', () => {
  let answer: ReturnType<typeof opened>;

  beforeEach(() => {
    // 🔴 Here, not only after: an open answered as the last test ended draws its screen during that test's
    // clean-up, and the next test found a stale screen first (seen as a flake in the full run).
    screens.length = 0;
    answer = opened();
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'OPEN' ? answer : { written: true }));
  });
  afterEach(() => {
    invoke.mockReset();
    eventHandlers.clear();
    screens.length = 0;
    createScreen.mockClear();
  });

  it('opens one terminal over the bridge when it is shown, where it was asked to start', async () => {
    render(<StrictMode><Held cwd="C:/somewhere/engine" /></StrictMode>);

    await waitFor(() => expect(screens).toHaveLength(1));
    // One, though strict mode runs the view's effect twice: a second open waits on the first.
    expect(calls('OPEN')).toEqual([['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/engine' } }]]);
    expect(screens[0]!.attached).toHaveLength(1);
  });

  it('lets the module choose where it starts when the frame has nowhere to say', async () => {
    render(<Held />);

    await waitFor(() => expect(screens).toHaveLength(1));
    expect(calls('OPEN')).toHaveLength(1);
    expect(calls('OPEN')[0]![2]).toEqual({ payload: {} });
  });

  it('writes what the shell says into the renderer, and sends what the person types to the shell', async () => {
    render(<Held cwd="C:/somewhere/engine" />);
    await waitFor(() => expect(screens).toHaveLength(1));

    await say(answer.id, 'PS C:\\somewhere\\engine> ');
    expect(screens[0]!.written).toEqual(['PS C:\\somewhere\\engine> ']);

    await act(async () => screens[0]!.typed!('git status\r'));
    expect(calls('INPUT')).toEqual([['DAORIS.TERMINAL', 'INPUT', { payload: { id: answer.id, data: 'git status\r' } }]]);
  });

  /** A shell prints its prompt at once, and the batch can beat the answer that names its terminal. */
  it('keeps what the shell said before its screen existed, and writes it there first', async () => {
    let answerOpen!: (value: unknown) => void;
    invoke.mockImplementation((_module: string, type: string) =>
      (type === 'OPEN' ? new Promise((resolve) => { answerOpen = resolve; }) : Promise.resolve({})));
    render(<Held />);
    await waitFor(() => expect(calls('OPEN')).toHaveLength(1));

    await say(answer.id, 'Windows PowerShell\r\n');
    await act(async () => answerOpen(answer));

    await waitFor(() => expect(screens[0]?.written).toEqual(['Windows PowerShell\r\n']));
  });

  it('sizes the shell to the view it is drawn in', async () => {
    render(<Held />);

    await waitFor(() => expect(calls('RESIZE')).toEqual([['DAORIS.TERMINAL', 'RESIZE', { payload: { id: answer.id, cols: 100, rows: 30 } }]]));
  });

  /** The view unmounts when it moves or another view is shown: the shell and its screen outlive it. */
  it('keeps the shell when the view goes, and shows the same screen, caught up, when it comes back', async () => {
    const { rerender } = render(<Held />);
    await waitFor(() => expect(screens).toHaveLength(1));
    await say(answer.id, 'one ');

    rerender(<Held shown={false} />);
    await say(answer.id, 'two ');
    expect(calls('CLOSE')).toEqual([]);

    rerender(<Held shown />);
    await waitFor(() => expect(screens[0]!.written).toEqual(['one ', 'two ']));
    expect(createScreen).toHaveBeenCalledTimes(1);
    expect(screens[0]!.attached).toHaveLength(2);
    expect(calls('OPEN')).toHaveLength(1);
  });

  it('says a shell ended, with its code, and starts another like it on asking', async () => {
    render(<Held cwd="C:/somewhere/engine" />);
    await waitFor(() => expect(screens).toHaveLength(1));

    await act(() => eventHandlers.get('DAORIS.TERMINAL_EXITED')!({ id: answer.id, code: 3 }));
    expect(screen.getByText('PowerShell ended with code 3.')).toBeInTheDocument();

    const ended = answer;
    answer = opened();
    await userEvent.click(screen.getByRole('button', { name: 'Start again' }));

    expect(calls('CLOSE')).toEqual([['DAORIS.TERMINAL', 'CLOSE', { payload: { id: ended.id } }]]);
    await waitFor(() => expect(calls('OPEN')).toHaveLength(2));
    expect(calls('OPEN')[1]![2]).toEqual({ payload: { shell: 'pwsh', cwd: 'C:/somewhere/engine' } });
    await waitFor(() => expect(screens).toHaveLength(2));
    expect(screens[0]!.disposed).toBe(true);
    expect(screen.queryByText('PowerShell ended with code 3.')).toBeNull();
  });

  it('takes the theme the person chooses', async () => {
    render(<Held />);
    await waitFor(() => expect(screens).toHaveLength(1));

    act(() => setThemeChoice('dark'));
    act(() => setThemeChoice('system'));

    expect(screens[0]!.looks).toBe(2);
  });

  it('says why a terminal would not open, in the reader\'s language, and offers to try again', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'OPEN') throw Object.assign(new Error('no shell'), { code: 'TERMINAL_NO_SHELL', parameters: {} });
      return {};
    });
    render(<Held />);

    expect(await screen.findByText('This machine has no shell on its PATH for a terminal: PowerShell, Command Prompt or Git Bash.'))
      .toBeInTheDocument();
    expect(screens).toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Open a terminal' })).toBeInTheDocument();
  });
});
