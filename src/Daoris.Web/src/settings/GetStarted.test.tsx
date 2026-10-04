import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import '../i18n';
import i18n from '../i18n';
import { readMachine } from '../help/machine';
import { code } from '../test/code';
import { setupSteps } from '../help/setup';
import { GetStarted } from './GetStarted';

// SETUP1a (D97): the Get started domain, drawn from props — each step in order with its state, the
// screens that do it and the command that does the same, shown and copyable (D50).

const FRESH = setupSteps(readMachine({
  attached: true, registry: [], driver: { drivable: [], helperAdapter: '' }, harnesses: [], lines: [], landings: [],
}));

const HALFWAY = setupSteps(readMachine({
  attached: true,
  registry: [{ repository: 'engine' }],
  driver: { drivable: [], helperAdapter: 'claude-code-acp' },
  harnesses: [{ harness: 'claude-code', present: true, ownLogin: 'in' }],
  lines: [],
  landings: [],
}));

const BROWSER = setupSteps(readMachine({ attached: false, registry: [] }));

const draw = (props: Partial<Parameters<typeof GetStarted>[0]> = {}) => {
  const onGo = vi.fn();
  const onAsk = vi.fn();
  const onAtStart = vi.fn();
  render(
    <Tooltip.Provider>
      <GetStarted steps={FRESH} helper={null} atStart onAtStart={onAtStart} onGo={onGo} onAsk={onAsk} {...props} />
    </Tooltip.Provider>,
  );
  return { onGo, onAsk, onAtStart };
};

const step = (name: RegExp) => screen.getByRole('listitem', { name });

describe('Get started', () => {
  afterEach(() => { void i18n.changeLanguage('en'); });

  it('lists the six steps in the order a setup goes, each with its state', () => {
    draw({ steps: HALFWAY, helper: 'claude-code-acp' });

    const names = within(screen.getByRole('list', { name: 'setup steps' })).getAllByRole('listitem')
      .map((item) => item.getAttribute('aria-label'));
    expect(names).toEqual([
      '1. An agent', "2. Ask Daoris's agent", '3. A workspace and its repositories',
      '4. What is driven', '5. How work lands', '6. What agents may do',
    ]);
    expect(within(step(/An agent/)).getByText('done')).toBeInTheDocument();
    expect(within(step(/Ask Daoris's agent/)).getByText('done')).toBeInTheDocument();
    expect(within(step(/repositories/)).getByText('done')).toBeInTheDocument();
    expect(within(step(/driven/)).getByText('to do')).toBeInTheDocument();
    expect(within(step(/work lands/)).getByText('to do')).toBeInTheDocument();
    expect(within(step(/agents may do/)).getByText('optional')).toBeInTheDocument();
    expect(screen.getByText('3 of 5 required steps done')).toBeInTheDocument();
  });

  it('opens the screen each step is done on', async () => {
    const { onGo } = draw();

    // UX6e: an agent's install and its accounts are on its page in the Agents place.
    await userEvent.click(within(step(/An agent/)).getByRole('button', { name: 'Open Agents' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'agents', agentPart: 'accounts' });
    await userEvent.click(within(step(/Ask Daoris's agent/)).getByRole('button', { name: "Open AI features" }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'ai' });
    await userEvent.click(within(step(/repositories/)).getByRole('button', { name: 'Add repository…' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'projects', drawer: 'add' });
    await userEvent.click(within(step(/repositories/)).getByRole('button', { name: 'Import a folder…' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'projects', drawer: 'import' });
    await userEvent.click(within(step(/driven/)).getByRole('button', { name: 'Open Repositories' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'projects' });
    // UX6g: how work lands for each repository that sets none is the workspace's default, on its page's Setup.
    await userEvent.click(within(step(/work lands/)).getByRole('button', { name: "Open the workspace's setup" }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'projects', workspaceSection: 'defaults' });
    // And what agents may do is on the page of the agent Daoris hands the rules file (D150 §3.1).
    await userEvent.click(within(step(/agents may do/)).getByRole('button', { name: 'Open what agents may do' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'agents', agentPart: 'rules' });
  });

  it('shows the command that does each step at a terminal, and copies it', async () => {
    const user = userEvent.setup();
    draw();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();

    const agent = step(/An agent/);
    expect(within(agent).getByText(code('daoris agent install <agent>'))).toBeInTheDocument();
    // The console's one code span, so a step's command breaks only between its words (LOOK5).
    const login = within(agent).getByText(code('daoris agent login <agent>'));
    expect([...login.children].map((word) => word.textContent)).toEqual(['daoris', 'agent', 'login', '<agent>']);
    for (const word of login.children) expect(word).toHaveClass('inline-block', 'max-w-full');
    expect(within(step(/repositories/)).getByText(code('daoris import <folder>'))).toBeInTheDocument();
    expect(within(step(/agents may do/)).getByText(code('daoris agent rules'))).toBeInTheDocument();

    await user.click(within(agent).getByRole('button', { name: 'copy daoris agent login <agent>' }));
    expect(writeText).toHaveBeenCalledWith('daoris agent login <agent>');
    expect(await within(agent).findByText('copied')).toBeInTheDocument();
  });

  it('hands Ask Daoris a first message naming the steps not yet done', async () => {
    const { onAsk } = draw({ steps: HALFWAY, helper: 'claude-code-acp' });

    await userEvent.click(screen.getByRole('button', { name: 'Set up with Ask Daoris' }));
    expect(onAsk).toHaveBeenCalledWith(
      'Walk me through setting up Daoris on this machine, one step at a time. Not done yet: '
      + '4. What is driven; 5. How work lands; 6. What agents may do (optional).');
  });

  it('says Ask Daoris needs an agent first, and offers no hand-off that could not run', () => {
    draw({ helper: null });

    expect(screen.queryByRole('button', { name: 'Set up with Ask Daoris' })).toBeNull();
    expect(screen.getByText(/once it has an agent to run on: step 2/)).toBeInTheDocument();
  });

  it('turns opening at start off and on, as the viewer\'s own choice', async () => {
    const { onAtStart } = draw({ atStart: true });

    const box = screen.getByRole('checkbox', { name: "Don't open at start" });
    expect(box).not.toBeChecked();
    await userEvent.click(box);
    expect(onAtStart).toHaveBeenCalledWith(false);
  });

  /** A browser may learn nothing of a machine (D47 §4): it shows what is registered, and says the rest is the desktop's. */
  it('shows a browser only the step it can know, and says the rest is the desktop\'s', () => {
    draw({ steps: BROWSER, helper: null, atStart: undefined, onAtStart: undefined, onAsk: undefined });

    const items = within(screen.getByRole('list', { name: 'setup steps' })).getAllByRole('listitem');
    expect(items.map((item) => item.getAttribute('aria-label'))).toEqual(['3. A workspace and its repositories']);
    expect(within(items[0]!).getByText('to do')).toBeInTheDocument();
    expect(within(items[0]!).queryByRole('button', { name: /Add repository/ })).toBeNull();
    expect(within(items[0]!).getByText(code('daoris connect'))).toBeInTheDocument();
    expect(screen.getByText(/the desktop's Setup shows them/)).toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Set up with Ask Daoris' })).toBeNull();
    expect(screen.queryByText(/required steps done/)).toBeNull();
  });

  /** A step drawn before the roster answered would say *to do* of a machine that has it done. */
  it('shows no step while the machine is still being read', () => {
    draw({ reading: true, helper: 'claude-code-acp' });

    expect(screen.getByText('Reading what this machine holds…')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'setup steps' })).toBeNull();
    expect(screen.queryByText(/required steps done/)).toBeNull();
    expect(screen.queryByRole('button', { name: 'Set up with Ask Daoris' })).toBeNull();
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    draw({ steps: HALFWAY, helper: 'claude-code-acp' });

    expect(screen.getByRole('listitem', { name: '1. 智能体' })).toBeInTheDocument();
    expect(screen.getByText('5 个必需步骤已完成 3 个')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '和问道衍一起配置' })).toBeInTheDocument();
  });
});
