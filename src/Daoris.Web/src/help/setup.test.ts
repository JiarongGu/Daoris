import { describe, expect, it } from 'vitest';
import { readMachine } from './machine';
import { opensAtStart, setupProgress, setupSteps } from './setup';
import { starters } from './starters';
import type { ToolDoor } from '../agents/tools';

// SETUP1a (D97): one pure reading of the machine serves the setup guide and Ask Daoris's starters, so a
// step and a starter never disagree. Every machine is an argument: fresh, an agent signed in with nothing
// registered, everything done, and a browser, which may learn nothing of a machine (D47 §4).

const door = (over: Partial<ToolDoor> = {}): ToolDoor => ({
  harness: 'claude-code', present: true, product: 'Claude Code', ownLogin: 'in',
  profiles: [{ name: 'work', home: 'h', login: 'in' }], ...over,
});

/** What each machine's queries answer, as the page receives them. */
const MACHINES = {
  fresh: {
    attached: true,
    registry: [],
    driver: { drivable: [], helperAdapter: '', intakeAdapter: '' },
    harnesses: [door({ present: false, ownLogin: 'unknown' })],
    sessions: [],
    lines: [],
  },
  signedInNothingRegistered: {
    attached: true,
    registry: [],
    driver: { drivable: [], helperAdapter: '', intakeAdapter: '' },
    harnesses: [door()],
    sessions: [],
    lines: [],
  },
  everythingDone: {
    attached: true,
    registry: [{ repository: 'engine' }, { repository: 'game' }],
    driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp', intakeAdapter: 'claude-code-acp' },
    harnesses: [door()],
    sessions: [{ state: 'working' as const }],
    lines: [
      { repository: 'engine', workspace: 'aurora', branch: 'develop', source: 'workspace' as const },
      // Not driven, so its missing line is neither the setup's business nor a starter's (STARTERSCOPE1).
      { repository: 'game', workspace: 'aurora', source: 'none' as const },
    ],
  },
  // A browser is handed nothing of the machine by its queries; handed some anyway, it reads none of it.
  browser: {
    attached: false,
    registry: [{ repository: 'engine' }],
    driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp' },
    harnesses: [door()],
    sessions: [{ state: 'awaiting-person' as const }],
    lines: [{ repository: 'engine', workspace: 'aurora', source: 'none' as const }],
  },
};

const states = (machine: keyof typeof MACHINES) =>
  Object.fromEntries(setupSteps(readMachine(MACHINES[machine])).map((step) => [step.id, step.state]));

describe('the machine, read once for the guide and the starters', () => {
  it('reads a fresh machine as lacking everything, and an absent tool as no agent', () => {
    expect(readMachine(MACHINES.fresh)).toMatchObject({
      attached: true, repositories: [], drivable: [], waiting: 0, unnamedLines: [], helper: null, intake: null,
    });
    expect(states('fresh')).toEqual({
      agent: 'todo', helper: 'todo', repositories: 'todo', setUp: 'todo', rules: 'optional',
    });
  });

  it('reads an agent signed in with nothing registered as one step of four', () => {
    expect(states('signedInNothingRegistered')).toEqual({
      agent: 'done', helper: 'todo', repositories: 'todo', setUp: 'todo', rules: 'optional',
    });
    expect(setupProgress(setupSteps(readMachine(MACHINES.signedInNothingRegistered)))).toEqual({ done: 1, of: 4 });
  });

  it('reads a machine with everything set as done, the rules optional and never counted', () => {
    const machine = readMachine(MACHINES.everythingDone);
    expect(machine).toMatchObject({
      repositories: ['engine', 'game'], drivable: ['engine'], helper: 'claude-code-acp', intake: 'claude-code-acp',
      unnamedLines: [], waiting: 0,
    });
    expect(states('everythingDone')).toEqual({
      agent: 'done', helper: 'done', repositories: 'done', setUp: 'done', rules: 'optional',
    });
    expect(setupProgress(setupSteps(machine))).toEqual({ done: 4, of: 4 });
  });

  it('does not count a line not read yet (the pushed flag, PUSH1b2) among the lines that name none', () => {
    const base = MACHINES.everythingDone;
    const machine = readMachine({
      ...base,
      driver: { ...base.driver, drivable: ['engine', 'game'] },
      lines: [
        { repository: 'engine', workspace: 'default', source: 'none', notReadYet: true },
        { repository: 'game', workspace: 'default', source: 'none' },
      ],
    });
    expect(machine.unnamedLines).toEqual(['game']);
  });

  it('counts no line of a repository imported and never driven, so no starter sends the person to it (STARTERSCOPE1)', () => {
    const base = MACHINES.everythingDone;
    const machine = readMachine({
      ...base,
      registry: [...base.registry, { repository: 'imported' }],
      lines: [...base.lines, { repository: 'imported', workspace: 'aurora', source: 'none' as const }],
    });
    expect(machine.unnamedLines).toEqual([]);
    expect(starters(machine).map((starter) => starter.id)).not.toContain('no-line');

    // Driven, the same repository's missing line is a starter again.
    const driven = readMachine({
      ...base,
      driver: { ...base.driver, drivable: ['engine', 'imported'] },
      lines: [...base.lines, { repository: 'imported', workspace: 'aurora', source: 'none' as const }],
    });
    expect(driven.unnamedLines).toEqual(['imported']);
    expect(starters(driven).find((starter) => starter.id === 'no-line')?.values).toEqual({ count: 1, first: 'imported' });
  });

  it('reads a browser as knowing only what is registered, and the rest as the desktop\'s', () => {
    const machine = readMachine(MACHINES.browser);
    expect(machine).toMatchObject({
      attached: false, repositories: ['engine'], drivable: [], tools: [], waiting: 0, unnamedLines: [], helper: null,
    });
    expect(states('browser')).toEqual({
      agent: 'desktop', helper: 'desktop', repositories: 'done', setUp: 'desktop', rules: 'desktop',
    });
    // Unknowable, so no count and no opening at start.
    expect(setupProgress(setupSteps(machine))).toBeNull();
    expect(opensAtStart(setupSteps(machine))).toBe(false);
  });

  it('opens at start only while one of the first three steps is to do', () => {
    expect(opensAtStart(setupSteps(readMachine(MACHINES.fresh)))).toBe(true);
    expect(opensAtStart(setupSteps(readMachine(MACHINES.signedInNothingRegistered)))).toBe(true);
    expect(opensAtStart(setupSteps(readMachine(MACHINES.everythingDone)))).toBe(false);
    // Step four left is counted, and does not open the guide.
    const undriven = readMachine({ ...MACHINES.everythingDone, driver: { ...MACHINES.everythingDone.driver, drivable: [] } });
    expect(opensAtStart(setupSteps(undriven))).toBe(false);
    expect(setupProgress(setupSteps(undriven))).toEqual({ done: 3, of: 4 });
  });

  /**
   * ANSWER1e (D131's ANSWER1c note, answer-continues design §5): a park the person answered stays parked until the
   * driver's next look, and the same session goes on then, so Ask Daoris counts it among what waits on them no longer.
   */
  it('counts no park the person answered as waiting on them', () => {
    const machine = readMachine({
      ...MACHINES.everythingDone,
      sessions: [
        { state: 'awaiting-person', answer: 'the second one' },
        { state: 'awaiting-person', answer: null },
        { state: 'awaiting-person' },
        { state: 'working' },
      ],
    });

    expect(machine.waiting).toBe(2);
    expect(starters(machine).find((starter) => starter.id === 'waiting')?.values).toEqual({ count: 2 });
    expect(readMachine({ ...MACHINES.everythingDone, sessions: [{ state: 'awaiting-person', answer: 'go on' }] }).waiting).toBe(0);
  });

  /** The design's point (§2): the facts are the starters' facts, so the two never disagree. */
  it('never lets a step and a starter disagree', () => {
    for (const answers of Object.values(MACHINES).filter((machine) => machine.attached)) {
      const machine = readMachine(answers);
      const step = Object.fromEntries(setupSteps(machine).map((one) => [one.id, one.state]));
      const said = starters(machine).map((starter) => starter.id);

      expect(said.includes('nothing-registered')).toBe(step.repositories === 'todo');
      expect(said.includes('helper-off')).toBe(step.helper === 'todo');
      expect(said.includes('nothing-driven')).toBe(step.repositories === 'done' && step.setUp === 'todo');
      if (said.includes('agent-signed-out') && machine.tools.every((tool) => !tool.present || tool.accounts.every((account) => account.login === 'out'))) {
        expect(step.agent).toBe('todo');
      }
    }
  });
});

describe('each step: its state, its doors and its commands', () => {
  const byId = (answers: Parameters<typeof readMachine>[0]) =>
    Object.fromEntries(setupSteps(readMachine(answers)).map((step) => [step.id, step]));

  it('names the screen that does each step and the command that does the same (D50)', () => {
    const steps = byId(MACHINES.fresh);

    expect(steps.agent).toMatchObject({
      doors: [{ view: 'agents', agentPart: 'accounts' }],
      commands: ['daoris agent install <agent>', 'daoris agent login <agent>'],
    });
    // Ask Daoris and the intake are roles, set on Agents → Roles and by `daoris driver role`.
    expect(steps.helper).toMatchObject({
      doors: [{ view: 'agents', agentsMode: 'setup', roleSection: 'working' }],
      commands: ['daoris driver role'],
    });
    expect(steps.repositories).toMatchObject({
      doors: [{ view: 'projects', drawer: 'add' }, { view: 'projects', drawer: 'import' }],
      commands: ['daoris connect', 'daoris import <folder>'],
    });
    // One step for the place's Set up…, which chooses how its work lands and drives it.
    expect(steps.setUp).toMatchObject({ doors: [{ view: 'agents', agentsMode: 'setup' }], commands: ['daoris-driver place <repository> --plan'] });
    // UX6e: what agents may do is on the page of the agent Daoris hands the rules file (D150 §3.1).
    expect(steps.rules).toMatchObject({ doors: [{ view: 'agents', agentPart: 'rules' }], commands: ['daoris agent rules'] });
  });

  it('lists the steps in the order a setup goes', () => {
    expect(setupSteps(readMachine(MACHINES.fresh)).map((step) => step.id))
      .toEqual(['agent', 'helper', 'repositories', 'setUp', 'rules']);
  });

  it('counts only Daoris\'s accounts as signed in: an unknown one as in, a definite out or none as not (OWNSPACE1b-C)', () => {
    const profile = (login: 'in' | 'out' | 'unknown') => [{ name: 'work', home: 'h', login }];
    const state = (over: Partial<ToolDoor>) => byId({ ...MACHINES.signedInNothingRegistered, harnesses: [door(over)] }).agent.state;
    expect(state({ profiles: profile('unknown') })).toBe('done');
    expect(state({ profiles: profile('in') })).toBe('done');
    expect(state({ profiles: profile('out') })).toBe('todo');
    expect(state({ profiles: [] })).toBe('todo');
    // The tool's own sign-in is no account of Daoris's, signed in or not (D175).
    expect(state({ ownLogin: 'in', profiles: [] })).toBe('todo');
    expect(state({ ownLogin: 'out', profiles: profile('in') })).toBe('done');
  });

  it('offers a browser no door to a screen it does not have, and still says the commands', () => {
    const steps = byId(MACHINES.browser);
    for (const step of Object.values(steps)) expect(step.doors).toEqual([]);
    expect(steps.repositories.commands).toEqual(['daoris connect', 'daoris import <folder>']);
  });
});
