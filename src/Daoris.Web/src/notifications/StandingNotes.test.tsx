import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// The two standing lines in Notifications, the terminal's presses run as the person's and what Ask Daoris pressed in full access, are one
// compact line each: shown only where something happened since the person last said they had seen it, the rest behind *Show*.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: () => undefined,
}));

import i18n from '../locales/i18n';
import type { HelpPressNow, StandingConfirmationNow } from '../person/standing';
import type { TerminalActsNow, TerminalPress } from '../person/terminalActs';
import { HelpPressLine } from './HelpPressLine';
import { NotificationsStanding } from './NotificationsStanding';
import { TerminalTierLine } from './TerminalTierLine';

const terminalPress = (id: string, at: string): TerminalPress => ({
  id, module: 'DAORIS.DRIVER', route: 'WORK_PAUSE', scope: 'quest', target: 'q1', command: null, at, outcome: 'done', code: null, ended: null,
});
const TERMINAL: TerminalActsNow = {
  changes: [{ tier: 'act', at: '2026-11-11T09:00:00Z', by: 'window', session: null }],
  presses: [terminalPress('t2', '2026-11-11T10:05:00Z'), terminalPress('t1', '2026-11-11T10:00:00Z')],
};
const helpPress = (id: string, at: string): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route: 'WORK_PAUSE', session: 's1', scope: 'quest', target: 'q1', at, outcome: 'done',
});
const HELP: StandingConfirmationNow = {
  holds: [], holders: [], stewardActs: [], stewardDesk: [], helpPresses: [helpPress('h2', '2026-11-11T10:05:00Z'), helpPress('h1', '2026-11-11T10:00:00Z')],
};

describe('The standing lines in Notifications are one compact line shown only for what is new', () => {
  beforeEach(() => window.localStorage.clear());
  afterEach(async () => {
    invoke.mockReset();
    window.localStorage.clear();
    await i18n.changeLanguage('en');
  });

  it.each([
    ['en', "A terminal's presses", 'Show what a terminal pressed', 'Got it', 'Turned on by you, in this window'],
    ['zh', '终端的操作', '显示终端执行的操作', '知道了', '由你在此窗口启用'],
  ])("the terminal's line is one line, with who turned it on behind Show, and says it is seen (%s)", async (language, label, show, seen, by) => {
    await i18n.changeLanguage(language);
    const onSeen = vi.fn();
    render(<TerminalTierLine tier="act" acts={TERMINAL} since={null} onSeen={onSeen} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    const line = within(screen.getByRole('region', { name: label }));
    expect(line.queryByText(new RegExp(by))).toBeNull();
    expect(line.queryByRole('list')).toBeNull();
    expect(document.querySelectorAll('[data-terminal-tier-line] p')).toHaveLength(1);
    await userEvent.click(line.getByRole('button', { name: show }));
    expect(line.getByText(new RegExp(by))).toBeInTheDocument();
    await userEvent.click(line.getByRole('button', { name: seen }));
    expect(onSeen).toHaveBeenCalledWith('2026-11-11T10:05:00Z');
  });

  it('the terminal line draws nothing once its newest press and change are seen, and again for a newer one', () => {
    const { rerender } = render(<TerminalTierLine tier="act" acts={TERMINAL} since="2026-11-11T10:05:00Z" onSeen={vi.fn()} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(document.querySelector('[data-terminal-tier-line]')).toBeNull();
    rerender(<TerminalTierLine tier="act" acts={{ ...TERMINAL, presses: [terminalPress('t3', '2026-11-11T10:06:00Z'), ...TERMINAL.presses] }} since="2026-11-11T10:05:00Z" onSeen={vi.fn()} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(document.querySelector('[data-terminal-tier-line]')).not.toBeNull();
  });

  it.each([
    ['en', 'What was pressed for you', 'Show what was pressed for you', 'Got it'],
    ['zh', '为你代按的操作', '显示为你代按的操作', '知道了'],
  ])("Ask Daoris's line is one line, its presses behind Show, and says it is seen (%s)", async (language, label, show, seen) => {
    await i18n.changeLanguage(language);
    const onSeen = vi.fn();
    render(<HelpPressLine standing={HELP} since={null} onSeen={onSeen} onOpen={vi.fn()} onReverse={vi.fn()} />);
    const line = within(screen.getByRole('region', { name: label }));
    expect(line.queryByRole('list')).toBeNull();
    await userEvent.click(line.getByRole('button', { name: show }));
    expect(line.getAllByRole('listitem')).toHaveLength(2);
    await userEvent.click(line.getByRole('button', { name: seen }));
    expect(onSeen).toHaveBeenCalledWith('2026-11-11T10:05:00Z');
  });

  it("Ask Daoris's line draws nothing once its newest press is seen", () => {
    render(<HelpPressLine standing={HELP} since="2026-11-11T10:05:00Z" onSeen={vi.fn()} onOpen={vi.fn()} onReverse={vi.fn()} />);
    expect(document.querySelector('[data-help-press-line]')).toBeNull();
  });

  it('as Notifications holds them, a press seen stays seen across a reload until something newer is pressed', async () => {
    const state = (presses: HelpPressNow[]) => ({
      drivable: [], holds: [], trees: [], running: [], terminal: 'ask', terminalActs: { changes: [], presses: [] },
      standingConfirmation: { ...HELP, helpPresses: presses },
    });
    invoke.mockImplementation(async () => state(HELP.helpPresses));
    const draw = () => render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <NotificationsStanding notify={() => {}} onOpenHelp={() => {}} />
      </QueryClientProvider>);
    const first = draw();
    await userEvent.click(await screen.findByRole('button', { name: 'Got it' }));
    expect(document.querySelector('[data-help-press-line]')).toBeNull();
    first.unmount();

    draw();
    await vi.waitFor(() => expect(invoke).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.querySelector('[data-help-press-line]')).toBeNull();
    first.unmount();

    invoke.mockImplementation(async () => state([helpPress('h3', '2026-11-11T10:06:00Z'), ...HELP.helpPresses]));
    document.body.innerHTML = '';
    draw();
    expect(await screen.findByRole('region', { name: 'What was pressed for you' })).toBeInTheDocument();
  });
});
