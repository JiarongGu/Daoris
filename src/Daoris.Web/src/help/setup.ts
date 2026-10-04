import type { Machine } from './machine';
import type { StarterDoor } from './starters';

/** The setup guide's steps (D97), in the order a setup goes. */
export type SetupStepId = 'agent' | 'helper' | 'repositories' | 'driven' | 'landing' | 'rules';

/**
 * Where a step stands: done, to do, optional (it never blocks and has no done), or the desktop's — a
 * fact of this machine, which a browser may not learn (D47 §4).
 */
export type SetupState = 'done' | 'todo' | 'optional' | 'desktop';

/** One step: where it stands, the screens that do it, and the commands that do the same (D50). */
export type SetupStep = { id: SetupStepId; state: SetupState; doors: StarterDoor[]; commands: string[] };

/** How many steps are required: all but the last, which is optional. */
const REQUIRED = 5;

/** The steps a machine missing any of opens on the guide at start (D97 §2): an agent, Daoris's own, a repository. */
const FIRST: readonly SetupStepId[] = ['agent', 'helper', 'repositories'];

/**
 * An agent this machine can run a session on: installed, and not definitely signed out. The roster's
 * rule (SES3), which the starters use too: an `unknown` sign-in is not a lack anyone can act on.
 */
const signedIn = (machine: Machine) => machine.tools.some((tool) =>
  tool.present && (tool.ownLogin !== 'out' || tool.accounts.some((account) => account.login === 'in')));

/**
 * The setup guide's steps (SETUP1a, D97): each with its state read off the machine, the screens that
 * do it and the terminal commands that do the same. Pure, so every machine is an argument.
 *
 * @remarks
 * **The facts are the starters' facts** (`readMachine`), so a step and a starter never disagree: no
 * repository registered is both step 3 and a starter, no agent named for Ask Daoris is both step 2 and
 * a starter. The guide adds the order, the done state and the optional last step.
 *
 * **How work lands is done only once something is driven** and every driven repository has a line and
 * a landing rule of its own or its workspace's: with nothing driven there is nothing for it to say yet.
 * **What agents may do is optional and never done**: nothing on the machine records the rules being
 * looked at, and the step never blocks.
 *
 * **In a browser** only what is registered is known; every other step is the desktop's, with no door,
 * since none of their screens is a browser's (D47 §4). The commands still stand: they are the same
 * everywhere.
 */
export function setupSteps(machine: Machine): SetupStep[] {
  const known = machine.attached;
  const machineState = (done: boolean): SetupState => (!known ? 'desktop' : done ? 'done' : 'todo');
  const doors = (...list: StarterDoor[]) => (known ? list : []);
  const unnamedDriven = machine.drivable.filter((repository) => machine.unnamedLines.includes(repository));

  return [
    {
      id: 'agent',
      state: machineState(signedIn(machine)),
      // The Agents place (UX6e, D150 §5): an agent's install and its accounts are on its page.
      doors: doors({ view: 'agents', agentPart: 'accounts' }),
      commands: ['daoris agent install <agent>', 'daoris agent login <agent>'],
    },
    {
      id: 'helper',
      state: machineState(machine.helper !== null),
      doors: doors({ view: 'settings', section: 'ai' }),
      commands: ['daoris driver helper <agent>', 'daoris driver intake <agent>'],
    },
    {
      id: 'repositories',
      // The one step a browser can read: the registry is every browser's.
      state: machine.repositories.length > 0 ? 'done' : 'todo',
      // The Workspace menu's two drawers, which a browser is not offered.
      doors: doors({ view: 'projects', drawer: 'add' }, { view: 'projects', drawer: 'import' }),
      commands: ['daoris connect', 'daoris import <folder>'],
    },
    {
      id: 'driven',
      state: machineState(machine.drivable.length > 0),
      doors: doors({ view: 'projects' }),
      commands: ['daoris driver drive <repository>'],
    },
    {
      id: 'landing',
      state: machineState(machine.drivable.length > 0 && unnamedDriven.length === 0 && machine.unlanded.length === 0),
      // A missing line is the first thing to set, on the first such repository's Setup (UX6f); else the workspace's
      // defaults, which set how work lands for each repository there that sets none (UX6g, D150 §4.3).
      doors: doors(unnamedDriven.length > 0
        ? { view: 'projects', item: unnamedDriven[0]!, tab: 'setup' }
        : { view: 'projects', workspaceSection: 'defaults' }),
      commands: ['daoris driver line <repository> <branch>', 'daoris driver landing <repository> merge|branch <pattern>'],
    },
    {
      id: 'rules',
      state: known ? 'optional' : 'desktop',
      // What it may do is on the page of the agent Daoris hands the rules file (UX6e, D150 §3.1).
      doors: doors({ view: 'agents', agentPart: 'rules' }),
      commands: ['daoris agent rules'],
    },
  ];
}

/**
 * How many required steps are done, for the status bar's *setup: n of 5* — or null where that cannot be
 * known, which is a browser.
 */
export function setupProgress(steps: readonly SetupStep[]): { done: number; of: number } | null {
  if (steps.some((step) => step.state === 'desktop')) return null;
  return { done: steps.filter((step) => step.state === 'done').length, of: REQUIRED };
}

/** Whether the guide opens at start (D97 §2): one of the first three steps is to do on this machine. */
export function opensAtStart(steps: readonly SetupStep[]): boolean {
  return steps.some((step) => FIRST.includes(step.id) && step.state === 'todo')
    && !steps.some((step) => step.state === 'desktop');
}
