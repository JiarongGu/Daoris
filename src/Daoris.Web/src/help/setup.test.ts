import { describe, expect, it } from 'vitest';
import { readMachine } from './machine';
import { opensAtStart, setupProgress, setupSteps } from './setup';
import { starters } from './starters';
import type { ToolDoor } from '../tools';

// SETUP1a (D97): one pure reading of the machine serves the setup guide and Ask Daoris's starters, so a
// step and a starter never disagree. Every machine is an argument: fresh, an agent signed in with nothing
// registered, everything done, and a browser, which may learn nothing of a machine (D47 §4).

const door = (over: Partial<ToolDoor> = {}): ToolDoor => ({
  harness: 'claude-code', present: true, product: 'Claude Code', ownLogin: 'in', profiles: [], ...over,
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
    landings: [],
  },
  signedInNothingRegistered: {
    attached: true,
    registry: [],
    driver: { drivable: [], helperAdapter: '', intakeAdapter: '' },
    harnesses: [door()],
    sessions: [],
    lines: [],
    landings: [],
  },
  everythingDone: {
    attached: true,
    registry: [{ repository: 'engine' }, { repository: 'game' }],
    driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp', intakeAdapter: 'claude-code-acp' },
    harnesses: [door()],
    sessions: [{ state: 'working' }],
    lines: [
      { repository: 'engine', workspace: 'aurora', branch: 'develop', source: 'workspace' as const },
      // Not driven, so its missing line is not the setup's business — only a starter's.
      { repository: 'game', workspace: 'aurora', source: 'none' as const },
    ],
    landings: [
      { repository: 'engine', workspace: 'aurora', source: 'workspace' as const, form: 'merge' },
      { repository: 'game', workspace: 'aurora', source: 'default' as const, form: 'merge' },
    ],
  },
  // A browser is handed nothing of the machine by its queries; handed some anyway, it reads none of it.
  browser: {
    attached: false,
    registry: [{ repository: 'engine' }],
    driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp' },
    harnesses: [door()],
    sessions: [{ state: 'awaiting-person' }],
    lines: [{ repository: 'engine', workspace: 'aurora', source: 'none' as const }],
    landings: [],
  },
};

const states = (machine: keyof typeof MACHINES) =>
  Object.fromEntries(setupSteps(readMachine(MACHINES[machine])).map((step) => [step.id, step.state]));

describe('the machine, read once for the guide and the starters', () => {
  it('reads a fresh machine as lacking everything, and an absent tool as no agent', () => {
    expect(readMachine(MACHINES.fresh)).toMatchObject({
      attached: true, repositories: [], drivable: [], waiting: 0, unnamedLines: [], helper: null, intake: null, unlanded: [],
    });
    expect(states('fresh')).toEqual({
      agent: 'todo', helper: 'todo', repositories: 'todo', driven: 'todo', landing: 'todo', rules: 'optional',
    });
  });

  it('reads an agent signed in with nothing registered as one step of five', () => {
    expect(states('signedInNothingRegistered')).toEqual({
      agent: 'done', helper: 'todo', repositories: 'todo', driven: 'todo', landing: 'todo', rules: 'optional',
    });
    expect(setupProgress(setupSteps(readMachine(MACHINES.signedInNothingRegistered)))).toEqual({ done: 1, of: 5 });
  });

  it('reads a machine with everything set as done, the rules optional and never counted', () => {
    const machine = readMachine(MACHINES.everythingDone);
    expect(machine).toMatchObject({
      repositories: ['engine', 'game'], drivable: ['engine'], helper: 'claude-code-acp', intake: 'claude-code-acp',
      unnamedLines: ['game'], unlanded: [], waiting: 0,
    });
    expect(states('everythingDone')).toEqual({
      agent: 'done', helper: 'done', repositories: 'done', driven: 'done', landing: 'done', rules: 'optional',
    });
    expect(setupProgress(setupSteps(machine))).toEqual({ done: 5, of: 5 });
  });

  it('reads a browser as knowing only what is registered, and the rest as the desktop\'s', () => {
    const machine = readMachine(MACHINES.browser);
    expect(machine).toMatchObject({
      attached: false, repositories: ['engine'], drivable: [], tools: [], waiting: 0, unnamedLines: [], helper: null,
    });
    expect(states('browser')).toEqual({
      agent: 'desktop', helper: 'desktop', repositories: 'done', driven: 'desktop', landing: 'desktop', rules: 'desktop',
    });
    // Unknowable, so no count and no opening at start.
    expect(setupProgress(setupSteps(machine))).toBeNull();
    expect(opensAtStart(setupSteps(machine))).toBe(false);
  });

  it('opens at start only while one of the first three steps is to do', () => {
    expect(opensAtStart(setupSteps(readMachine(MACHINES.fresh)))).toBe(true);
    expect(opensAtStart(setupSteps(readMachine(MACHINES.signedInNothingRegistered)))).toBe(true);
    expect(opensAtStart(setupSteps(readMachine(MACHINES.everythingDone)))).toBe(false);
    // Steps four and five left are counted, and do not open the guide.
    const undriven = readMachine({ ...MACHINES.everythingDone, driver: { ...MACHINES.everythingDone.driver, drivable: [] } });
    expect(opensAtStart(setupSteps(undriven))).toBe(false);
    expect(setupProgress(setupSteps(undriven))).toEqual({ done: 3, of: 5 });
  });

  /** The design's point (§2): the facts are the starters' facts, so the two never disagree. */
  it('never lets a step and a starter disagree', () => {
    for (const answers of Object.values(MACHINES).filter((machine) => machine.attached)) {
      const machine = readMachine(answers);
      const step = Object.fromEntries(setupSteps(machine).map((one) => [one.id, one.state]));
      const said = starters(machine).map((starter) => starter.id);

      expect(said.includes('nothing-registered')).toBe(step.repositories === 'todo');
      expect(said.includes('helper-off')).toBe(step.helper === 'todo');
      expect(said.includes('nothing-driven')).toBe(step.repositories === 'done' && step.driven === 'todo');
      if (said.includes('agent-signed-out') && machine.tools.every((tool) => !tool.present || tool.ownLogin === 'out')) {
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
      doors: [{ view: 'settings', section: 'agents' }],
      commands: ['daoris agent install <agent>', 'daoris agent login <agent>'],
    });
    expect(steps.helper).toMatchObject({
      doors: [{ view: 'settings', section: 'ai' }],
      commands: ['daoris driver helper <agent>', 'daoris driver intake <agent>'],
    });
    expect(steps.repositories).toMatchObject({
      doors: [{ view: 'projects', drawer: 'add' }, { view: 'projects', drawer: 'import' }],
      commands: ['daoris connect', 'daoris import <folder>'],
    });
    expect(steps.driven).toMatchObject({ doors: [{ view: 'projects' }], commands: ['daoris driver drive <repository>'] });
    expect(steps.landing).toMatchObject({
      doors: [{ view: 'settings', section: 'workspace', anchor: 'landing' }],
      commands: ['daoris driver line <repository> <branch>', 'daoris driver landing <repository> merge|branch <pattern>'],
    });
    expect(steps.rules).toMatchObject({ doors: [{ view: 'settings', section: 'permissions' }], commands: ['daoris agent rules'] });
  });

  it('lists the steps in the order a setup goes', () => {
    expect(setupSteps(readMachine(MACHINES.fresh)).map((step) => step.id))
      .toEqual(['agent', 'helper', 'repositories', 'driven', 'landing', 'rules']);
  });

  it('counts an unknown sign-in as signed in, the roster\'s rule, and a definite out as not', () => {
    expect(byId({ ...MACHINES.signedInNothingRegistered, harnesses: [door({ ownLogin: 'unknown' })] }).agent.state).toBe('done');
    expect(byId({ ...MACHINES.signedInNothingRegistered, harnesses: [door({ ownLogin: 'out' })] }).agent.state).toBe('todo');
    // A named account signed in is an account, whatever the tool's own home says.
    expect(byId({
      ...MACHINES.signedInNothingRegistered,
      harnesses: [door({ ownLogin: 'out', profiles: [{ name: 'work', home: 'h', login: 'in' }] })],
    }).agent.state).toBe('done');
  });

  it('leaves how work lands to do while a driven repository lands by the default, or has no line', () => {
    const done = MACHINES.everythingDone;
    const defaulted = byId({ ...done, landings: [{ ...done.landings[0]!, source: 'default' as const }, done.landings[1]!] });
    expect(defaulted.landing).toMatchObject({ state: 'todo', doors: [{ view: 'settings', section: 'workspace', anchor: 'landing' }] });

    // A missing line comes first, so the door opens on the lines, above the landing.
    const lineless = byId({ ...done, lines: [{ repository: 'engine', workspace: 'aurora', source: 'none' as const }, done.lines[1]!] });
    expect(lineless.landing).toMatchObject({ state: 'todo', doors: [{ view: 'settings', section: 'workspace', anchor: 'lines' }] });
  });

  it('offers a browser no door to a screen it does not have, and still says the commands', () => {
    const steps = byId(MACHINES.browser);
    for (const step of Object.values(steps)) expect(step.doors).toEqual([]);
    expect(steps.repositories.commands).toEqual(['daoris connect', 'daoris import <folder>']);
  });
});
