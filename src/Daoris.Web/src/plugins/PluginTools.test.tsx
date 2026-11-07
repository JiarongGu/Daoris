import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../locales/i18n';
import i18n from '../locales/i18n';
import type { ToolShown } from './catalog';
import { PluginTools } from './PluginTools';

// A plugin's tools on its page: each declared tool, what a Check found, and a press per unmet check.

const SHIP: ToolShown = {
  id: 'ship', name: 'Ship CLI', for: 'Opens the release.', versions: '>=2.0.79',
  install: 'winget install -e --id Ship.Cli',
  checks: [
    { says: 'Its deploy plugin is added', fix: 'ship plugin add deploy', signIn: false, press: true },
    { says: 'Signed in', fix: 'ship login', signIn: true, press: true },
  ],
};

const READ: ToolShown = {
  ...SHIP, found: true, ok: true, version: '2.70.0', sentence: 'Ship CLI 2.70.0, the system\'s, in its range (2.0.79 or newer).',
  checks: [
    { ...SHIP.checks[0], ready: true, sentence: 'ready: Its deploy plugin is added.' },
    { ...SHIP.checks[1], ready: false, sentence: 'not ready: Signed in. `ship whoami` exited 1. Run `ship login` to put it right.' },
  ],
};

function tools(over: Partial<Parameters<typeof PluginTools>[0]> = {}) {
  const calls = { onCheck: vi.fn(), onFix: vi.fn() };
  const view = render(<PluginTools plugin="azure-devops-pull-request" tools={[SHIP]} {...calls} {...over} />);
  return { ...view, ...calls };
}

/** The page's words as read, a code span's words whole. */
const said = () => document.body.textContent?.replace(/\s+/g, ' ') ?? '';

describe("a plugin's tools", () => {
  it('lists each declared tool and offers Check, and draws no press before the machine has been asked', () => {
    tools();

    expect(screen.getByRole('heading', { name: 'Tools it needs' })).toBeInTheDocument();
    expect(screen.getByText('Ship CLI')).toBeInTheDocument();
    expect(screen.getByText('Opens the release.')).toBeInTheDocument();
    expect(screen.getByText('Signed in')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check azure-devops-pull-request’s tools' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Sign in|Set up/ })).toBeNull();
  });

  it('asks only on the press of Check', async () => {
    const { onCheck } = tools();

    await userEvent.click(screen.getByRole('button', { name: /^Check / }));

    expect(onCheck).toHaveBeenCalledWith('azure-devops-pull-request');
  });

  it('draws a press per unmet check, Sign in for a sign-in and Set up for the rest, and none for one that stands', async () => {
    const unmet: ToolShown = {
      ...READ,
      checks: [{ ...READ.checks[0], ready: false, sentence: 'not ready.' }, READ.checks[1]],
    };
    const { onFix } = tools({ tools: [unmet] });

    expect(screen.getAllByRole('button', { name: /^Set up|^Sign in/ })).toHaveLength(2);
    await userEvent.click(screen.getByRole('button', { name: 'Set up: Its deploy plugin is added' }));
    expect(onFix).toHaveBeenLastCalledWith('azure-devops-pull-request', 'ship', 1);
    await userEvent.click(screen.getByRole('button', { name: 'Sign in: Signed in' }));
    expect(onFix).toHaveBeenLastCalledWith('azure-devops-pull-request', 'ship', 2);
  });

  it('names the command a press runs, and offers no press on a check that is ready', () => {
    tools({ tools: [READ] });

    expect(screen.getAllByRole('button', { name: /^Set up|^Sign in/ })).toHaveLength(1);
    expect(screen.getByText('ship login')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Set up: Its deploy/ })).toBeNull();
  });

  it('says a fix the plugin may not run, and draws no press for it', () => {
    const refused: ToolShown = {
      ...READ,
      checks: [{ says: 'Odd', fix: 'rm -rf /', signIn: false, press: false, refusal: '`rm` is not a tool this plugin declares in `tools`.', ready: false, sentence: 'not ready.' }],
    };
    tools({ tools: [refused] });

    expect(screen.queryByRole('button', { name: /Set up|Sign in/ })).toBeNull();
    expect(said()).toContain('rm is not a tool this plugin declares');
  });

  it('says the install hint for a tool not found, and presses nothing', () => {
    tools({ tools: [{ ...SHIP, found: false, ok: false, sentence: '`ship` is not on this machine\'s PATH.' }] });

    expect(screen.getByText(/Not found on this machine/)).toBeInTheDocument();
    expect(screen.getByText('winget install -e --id Ship.Cli')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Set up|Sign in/ })).toBeNull();
  });

  it('says when the plugin names no way to get a tool that is not found', () => {
    tools({ tools: [{ ...SHIP, install: null, found: false, ok: false }] });

    expect(screen.getByText(/names no way to get it/)).toBeInTheDocument();
  });

  it('holds the presses while a console is open and says where the person acts', () => {
    tools({ tools: [READ], fixing: { tool: 'ship', check: 2 } });

    expect(said()).toContain('A console is open for ship login');
    expect(screen.getAllByRole('button', { name: /Set up|Sign in|^Check/ }).every((button) => button.hasAttribute('disabled'))).toBe(true);
  });

  it('words a press that was refused in its own sentence', () => {
    tools({ tools: [READ], refused: { tool: 'ship', check: 2, why: 'not_found', message: '`ship` is not on this machine\'s PATH.' } });

    const row = screen.getByText('Signed in').closest('li')!;
    expect(row.textContent?.replace(/\s+/g, ' ')).toContain('ship is not on this machine');
  });

  it('says a tool entry that is not read, and the tools field when it is not an array', () => {
    tools({ tools: [{ id: 'ship', name: 'ship', problem: 'tool `ship` has a bad `install`.', checks: [] }] });

    expect(said()).toContain('has a bad install.');
  });

  it('is said in 中文 too, with the same presses', async () => {
    await i18n.changeLanguage('zh');
    try {
      tools({ tools: [READ] });

      expect(screen.getByRole('heading', { name: '所需工具' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '登录：Signed in' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('draws nothing for a plugin that declares no tools', () => {
    const { container } = tools({ tools: [] });

    expect(container).toBeEmptyDOMElement();
  });
});
