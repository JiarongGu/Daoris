import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import { FULL, NOTHING_SET, PLUGIN_UNREADY, WORK, WORKSPACE } from './fixtures';
import { WorkflowTab } from './WorkflowTab';

// WORKFLOW1b (D157 point 12; the workflow design §6.1, §6.2, §6.4): the Workflow tab draws Current read-only, a vertical
// list drawn as a graph. Each step says what it is, the person's part in it and who acts, what it is set to, where that
// was set with a door there, how much of it this build runs and its limit whole; each edge says what moves work on. A
// molecule: every state is reached by its props, and every press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** The step rows, in order, as a reader meets them. */
const steps = () => screen.getAllByRole('listitem').filter((item) => item.hasAttribute('data-step'));

describe("a repository's Workflow tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('heads with Current, its version, the person part composed from the steps, and the terminal twin', () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);

    expect(screen.getByRole('heading', { name: 'Current workflow' })).toBeInTheDocument();
    expect(screen.getByText('version 4740fb017006')).toBeInTheDocument();
    expect(screen.getByText(/How work in engine moves, read from its rules as they stand at each gate/)).toBeInTheDocument();
    const part = screen.getByText(/^Your part:/);
    expect(part).toHaveTextContent('Your part: you look at it in dev and say it is reviewed; then you merge its pull request on the platform.');
    expect(screen.getByText(code('daoris driver workflow show --repository engine'))).toBeInTheDocument();
  });

  it('draws each step in order, the landing the hinge between before and after it lands', () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);

    const before = screen.getByRole('list', { name: 'Before it lands' });
    const after = screen.getByRole('list', { name: 'After it lands' });
    expect(within(before).getAllByRole('heading', { level: 4 }).map((head) => head.textContent))
      .toEqual(['The work', 'Second opinion', 'Your look in dev', 'Landing on a branch']);
    expect(within(after).getAllByRole('heading', { level: 4 }).map((head) => head.textContent)).toEqual(['Pull request']);
    expect(screen.getByText('Finished')).toBeInTheDocument();
    // The plugins that may hold a start are drawn above the work, never as a step.
    expect(screen.getByText(/Before the work starts,/)).toHaveTextContent('Before the work starts, example.hold-by-title may hold it.');
    expect(steps()).toHaveLength(5);
  });

  it("says each step's part, who acts, what it is set to, where it was set and how much of it runs", () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    const [work, opinion, look, landing, pullRequest] = steps();

    expect(work).toHaveTextContent('agent alone');
    expect(work).toHaveTextContent('by an agent');
    expect(work).toHaveTextContent('a done that departs from your words or lacks its evidence waits for your yes');
    // The standing answer is the person's words, shown as written.
    expect(work).toHaveTextContent('Your standing answer, handed to each session here: “dev writes allowed; prod only on a yes”');
    expect(work).toHaveTextContent('Always part of the work here.');
    expect(work).toHaveTextContent('works today');

    expect(opinion).toHaveTextContent('agent alone');
    expect(opinion).toHaveTextContent('by codex-acp, else dsh');
    expect(opinion).toHaveTextContent('Read before it lands and before each next step; not required; commits that answer it not read again.');
    expect(opinion).toHaveTextContent("Set by the workspace aurora's second-opinion rule.");
    // In part since XAGENT1f's gate: the landing waits for it, and its limit says what is not built.
    expect(opinion).toHaveTextContent('works in part');
    expect(opinion).toHaveTextContent("The work here lands only once another agent's reading of it is settled");

    expect(look).toHaveTextContent('agent + you');
    expect(look).toHaveTextContent("Set by this repository's review rule.");
    expect(look).toHaveTextContent('works in part');

    expect(landing).toHaveTextContent('automatic');
    expect(landing).toHaveTextContent('by Daoris');
    expect(landing).toHaveTextContent('On a branch named work/{quest}-{slug}. Accepted automatically once its quest is done. '
      + 'example.pull-request pushes it and opens a pull request. Its tree and branch go once it lands.');

    expect(pullRequest).toHaveTextContent('you');
    expect(pullRequest).toHaveTextContent('by example.pull-request');
    expect(pullRequest).toHaveTextContent('Its state is asked of its plugin only when branches are cleaned up');
  });

  it('says on each edge what moves work on: your press, or automatically', () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    const edges = steps().map((step) => step.querySelector('[data-edge]')?.textContent);

    expect(edges).toEqual([
      'then automatically',
      'then automatically',
      'then once you say it is reviewed',
      'then automatically',
      'then once you merge it on the platform',
    ]);
  });

  it("says Daoris's default where nothing is set, and draws nothing after the landing where nothing follows it", () => {
    render(<WorkflowTab page="repository" name="engine" current={NOTHING_SET} />);
    const [, landing] = steps();

    expect(landing).toHaveTextContent('Landing by a merge');
    expect(landing).toHaveTextContent('Merged into its line. Waits for you to accept it.');
    expect(landing).toHaveTextContent("Daoris's default: no landing rule is set.");
    expect(landing.querySelector('[data-edge]')).toHaveTextContent('then once you accept it');
    expect(screen.queryByRole('list', { name: 'After it lands' })).toBeNull();
    expect(screen.getByText(/^Your part:/)).toHaveTextContent('Your part: you accept it.');
  });

  it('says a limit whole on every step that has one, never folded away', () => {
    render(<WorkflowTab page="repository" name="engine" current={PLUGIN_UNREADY} />);
    const unready = 'Its plugin cannot land work on this machine now: it is not installed, is switched off, contributes nothing, '
      + 'or speaks on no work/land point. daoris plugin list says which.';

    expect(steps()[1]).toHaveTextContent(unready);
    expect(steps()[2]).toHaveTextContent(unready);
  });

  it("opens the Setup that sets a step: this repository's, or its workspace's where the workspace set it", async () => {
    const setup = vi.fn();
    const workspaceSetup = vi.fn();
    render(<WorkflowTab page="repository" name="engine" current={FULL} doors={{ setup, workspaceSetup }} />);
    const [work, opinion, look] = steps();

    await userEvent.click(within(look).getByRole('button', { name: 'Change in Setup' }));
    expect(setup).toHaveBeenLastCalledWith('work');
    await userEvent.click(within(opinion).getByRole('button', { name: 'Open workspace Setup' }));
    expect(workspaceSetup).toHaveBeenCalledOnce();
    // The standing answer is set on Setup's Sessions.
    await userEvent.click(within(work).getByRole('button', { name: 'Change in Setup' }));
    expect(setup).toHaveBeenLastCalledWith('sessions');
  });

  it('offers no door where nothing is handed one', () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('moves between steps with ↑ and ↓', async () => {
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    const rows = steps();

    rows[0]!.focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(rows[1]).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}');
    expect(rows[4]).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(rows[4]).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}');
    expect(rows[3]).toHaveFocus();
  });

  it('says the registry does not hold a repository it was asked for, and which rules then reach it', () => {
    render(<WorkflowTab page="repository" name="stray" current={{ ...NOTHING_SET, repository: 'stray', registered: false }} />);
    expect(screen.getByText(/The registry does not hold it/)).toHaveTextContent('the default workspace\'s rules reach it');
  });

  it('draws a kind and a limit it has no words for in the driver\'s, marked as such, never dropped', () => {
    const odd = {
      ...NOTHING_SET,
      steps: [{ ...WORK(), limit: 'a-new-limit' }, { ...NOTHING_SET.steps[1]!, kind: 'stage', id: 'stage' }],
      limits: { 'a-new-limit': 'A sentence a newer driver says.' },
    };
    render(<WorkflowTab page="repository" name="engine" current={odd} />);

    expect(steps()[0]).toHaveTextContent('A sentence a newer driver says.');
    expect(steps()[0]).toHaveTextContent('shown as recorded');
    expect(steps()[1]).toHaveTextContent('A step this page has no words for: stage');
  });

  it('says it is reading while the answer is on its way, and a refusal in the driver\'s words where it was refused', () => {
    const { rerender } = render(<WorkflowTab page="repository" name="engine" reading />);
    expect(screen.getByText('Reading how work moves here…')).toBeInTheDocument();

    rerender(<Tooltip.Provider><WorkflowTab page="repository" name="engine" refusal="the driver is still coming up — try again in a moment." /></Tooltip.Provider>);
    expect(screen.getByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);

    expect(screen.getByRole('heading', { name: '现行工作流' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: '落地之前' })).toBeInTheDocument();
    const [work, , look, landing] = steps();
    expect(work).toHaveTextContent('智能体自行');
    expect(look).toHaveTextContent('智能体与你');
    expect(landing).toHaveTextContent('自动');
    // The person's words stay as written.
    expect(work).toHaveTextContent('dev writes allowed; prod only on a yes');
  });
});

describe("a workspace's Workflow tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("draws the workspace's Current, for each repository there that sets no rules of its own", () => {
    render(<WorkflowTab page="workspace" name="aurora" current={WORKSPACE} />);

    expect(screen.getByText(/How work moves in each repository of aurora that sets no rules of its own/)).toBeInTheDocument();
    expect(screen.getByText(code('daoris driver workflow show --workspace aurora'))).toBeInTheDocument();
    expect(steps()[1]).toHaveTextContent("Set by this workspace's second-opinion rule.");
    expect(steps()[3]).toHaveTextContent('No plugin pushes it: the branch is for you to push.');
  });

  it("opens its own Setup from each rule, and a repository that sets its own rules at its own workflow", async () => {
    const setup = vi.fn();
    const repository = vi.fn();
    render(<WorkflowTab page="workspace" name="aurora" current={WORKSPACE} doors={{ setup, repository }} />);

    await userEvent.click(within(steps()[2]!).getByRole('button', { name: 'Change in Setup' }));
    expect(setup).toHaveBeenLastCalledWith('defaults');

    const here = screen.getByRole('list', { name: 'Repositories here' });
    const [engine, game] = within(here).getAllByRole('listitem');
    expect(engine).toHaveTextContent('engineits own rules');
    expect(game).toHaveTextContent('gamefollows this');
    expect(within(game!).queryByRole('button')).toBeNull();
    await userEvent.click(within(engine!).getByRole('button', { name: "Open engine's workflow" }));
    expect(repository).toHaveBeenCalledWith('engine');
  });

  it('says so where the registry was not read, and where the workspace holds no repository', () => {
    const { rerender } = render(<WorkflowTab page="workspace" name="aurora" current={{ ...WORKSPACE, repositories: null }} />);
    expect(screen.getByText(/which repositories here set their own rules is not known/)).toBeInTheDocument();

    rerender(<Tooltip.Provider><WorkflowTab page="workspace" name="aurora" current={{ ...WORKSPACE, repositories: [] }} /></Tooltip.Provider>);
    expect(screen.getByText('No repository is in this workspace yet.')).toBeInTheDocument();
  });
});
