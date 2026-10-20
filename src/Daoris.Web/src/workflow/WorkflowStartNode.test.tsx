import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { GATES_CHANGED, NO_VERIFICATION, ROUTED, START, WORKSPACE } from './fixtures';
import { WorkflowTab } from './WorkflowTab';

// FABRIC-PIPELINE1a (D192 point 2, the pipeline design §1.3): a Workflow tab draws its start node first, each route (the default,
// each kind by its label and paths, the work that lands) wired to the workflow it names and saying what chose it, laid out by D186's
// Flow; a route naming nothing says what follows then. The landed route says where its gates stand against the line's tip, and the
// person's *Accept the gates* goes out where they wait to be accepted. A molecule: every state is reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const start = () => screen.getByRole('list', { name: 'Where work here goes' });
const node = (name: string) => within(start()).getByRole('listitem', { name });

describe("a Workflow tab's start node", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the start first, each route wired to the workflow it names, before the steps', () => {
    render(<WorkflowTab page="repository" name="engine" current={ROUTED} />);

    expect(screen.getByRole('heading', { name: 'Where work here goes' })).toBeInTheDocument();
    for (const name of ['Start', 'By default', 'Documentation', 'Landed work', 'Current', 'Documentation to a pull request', 'Verify the line']) {
      expect(node(name)).toBeInTheDocument();
    }
    expect(node('Documentation')).toHaveTextContent('docs/**');
    expect(node('Documentation')).toHaveTextContent('**/*.md');
    // Each wire says what chose the workflow, as the run will.
    expect(start()).toHaveTextContent('chosen by the workspace aurora for Documentation');
    expect(start()).toHaveTextContent('since nothing names a workflow');
    expect(start()).toHaveTextContent('as engine chooses');
    // Drawn before the steps.
    const steps = screen.getByRole('list', { name: 'Before it lands' });
    expect(start().compareDocumentPosition(steps) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('says the gates the work that lands runs unasked, as accepted at the line\'s tip, and that this build runs none yet', () => {
    render(<WorkflowTab page="repository" name="engine" current={ROUTED} />);

    const landed = node('Landed work');
    expect(landed).toHaveTextContent('Its gates run unasked as you accepted them, at blob 3b18e51 on main\'s tip aa11bb2.');
    expect(landed).toHaveTextContent('This build keeps the route and runs none of its checks yet.');
    expect(within(landed).queryByRole('button', { name: 'Accept the gates' })).not.toBeInTheDocument();
  });

  it("holds a changed declaration for the person's Accept the gates, showing what they accept, and the press goes out", async () => {
    const onAcceptGates = vi.fn();
    render(<WorkflowTab page="repository" name="engine" current={{ ...ROUTED, start: GATES_CHANGED }} onAcceptGates={onAcceptGates} />);

    const landed = node('Landed work');
    expect(landed).toHaveTextContent('The gates changed since you accepted them: nothing runs until you accept them again.');
    expect(within(landed).getByLabelText("daoris.gates.json at main's tip aa11bb2")).toHaveTextContent('"run": "npm test"');
    await userEvent.click(within(landed).getByRole('button', { name: 'Accept the gates' }));
    expect(onAcceptGates).toHaveBeenCalledOnce();
  });

  it('offers no Accept where no door is handed, and says why the tip was not read in the driver\'s words', () => {
    render(<WorkflowTab page="repository" name="engine" current={{
      ...ROUTED, start: { ...START, landed: { ...START.landed, state: 'unread', tip: null, problem: '`engine` has no checkout on this machine to read.' } },
    }} />);

    expect(node('Landed work')).toHaveTextContent('`engine` has no checkout on this machine to read.'.replaceAll('`', ''));
    expect(screen.queryByRole('button', { name: 'Accept the gates' })).not.toBeInTheDocument();
  });

  it('says what follows where a route names nothing: Current, or no verification here', () => {
    render(<WorkflowTab page="repository" name="engine" current={{ ...ROUTED, start: NO_VERIFICATION }} />);

    expect(node('No verification here')).toBeInTheDocument();
    expect(start()).toHaveTextContent('since nothing names a route for it');
  });

  it("says on a workspace's page that its route's gates are accepted repository by repository", () => {
    render(<WorkflowTab page="workspace" name="aurora" current={{
      ...WORKSPACE, start: { ...START, landed: { ...START.landed, level: 'workspace-landed', gates: null, state: 'unread', tip: null } },
    }} />);

    expect(node('Landed work')).toHaveTextContent("Its gates are accepted repository by repository, at each line's tip, on each repository's Workflow tab.");
    expect(start()).toHaveTextContent('as the workspace aurora chooses');
  });

  it('draws nothing of a start where a driver before it answers none', () => {
    render(<WorkflowTab page="repository" name="engine" current={{ ...ROUTED, start: undefined }} />);

    expect(screen.queryByRole('list', { name: 'Where work here goes' })).not.toBeInTheDocument();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowTab page="repository" name="engine" current={{ ...ROUTED, start: GATES_CHANGED }} onAcceptGates={() => {}} />);

    expect(screen.getByRole('heading', { name: '工作在这里如何分流' })).toBeInTheDocument();
    const list = screen.getByRole('list', { name: '工作在这里如何分流' });
    expect(within(list).getByRole('listitem', { name: '起点' })).toBeInTheDocument();
    expect(within(list).getByRole('listitem', { name: '落地的工作' })).toHaveTextContent('这些检查在你采纳之后变了：在你再次采纳之前，什么都不会运行。');
    expect(screen.getByRole('button', { name: '采纳这些检查' })).toBeInTheDocument();
  });
});
