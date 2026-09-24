import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { AgentRules, type AgentRulesState, type RuleProposal } from './AgentRules';

// PERM1 (D72): what an agent Daoris starts may do — Claude Code's own rules in Daoris's scopes, the
// screen's half of `daoris agent rules` (D50). Every value here is the driver's RULES answer.

const DEFAULTS: AgentRulesState['defaults'] = [
  {
    id: 'connector', list: 'allow', on: true,
    rules: ['mcp__daoris-knowledge__quest_respond'],
    why: "A session takes and closes its own quest through Daoris's connector.",
  },
  {
    id: 'commit', list: 'allow', on: true,
    rules: ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)'],
    why: 'A session commits its own work in its own tree.',
  },
  { id: 'no-push', list: 'deny', on: false, rules: ['Bash(git push:*)'], why: "A push stays the person's (D37)." },
  // A hook, not a rule (PERM3): no rules, and the tools it judges named instead.
  {
    id: 'tree-guard', list: 'deny', on: true, rules: [], hook: 'Edit|Write|MultiEdit|NotebookEdit',
    why: 'A session writes files only inside its own tree.',
  },
];

const RULES: AgentRulesState = {
  path: 'C:/somewhere/data/permissions.json',
  defaults: DEFAULTS,
  scopes: [
    // The machine's scope carries no name: the bridge leaves a null out.
    { scope: 'machine', allow: ['Bash(npm run test:*)'], ask: [], deny: [] },
    { scope: 'repository', name: 'engine', allow: [], ask: [], deny: ['Bash(rm -rf:*)'] },
  ],
};

const show = (extra: Partial<Parameters<typeof AgentRules>[0]> = {}) => {
  const props = {
    rules: RULES, circles: ['default'], repositories: ['engine', 'game'],
    onSwitchDefault: vi.fn(), onRemove: vi.fn(), onAdd: vi.fn(), onAnswer: vi.fn(), ...extra,
  };
  render(<Tooltip.Provider><AgentRules {...props} /></Tooltip.Provider>);
  return props;
};

describe('What agents may do', () => {
  it('names the file the terminal edits, and says the rules are Claude Code\'s own', () => {
    show();
    expect(screen.getByText('C:/somewhere/data/permissions.json')).toBeTruthy();
    expect(screen.getByText(/daoris agent rules/)).toBeTruthy();
  });

  /** A default is Daoris's, with its reason — switched on or off, and removable by nothing else. */
  it('shows each default with its reason and its rules, and switches one off by id', async () => {
    const props = show();
    const connector = screen.getByRole('listitem', { name: 'connector' });
    expect(within(connector).getByText(/closes its own quest/)).toBeTruthy();
    expect(within(connector).getByText('mcp__daoris-knowledge__quest_respond')).toBeTruthy();

    await userEvent.click(within(connector).getByRole('checkbox'));
    expect(props.onSwitchDefault).toHaveBeenCalledWith('connector', false);

    const push = screen.getByRole('listitem', { name: 'no-push' });
    expect(within(push).getByRole('checkbox').getAttribute('aria-checked')).toBe('false');
  });

  /** The tree guard adds no rule, so its row names the tools it judges instead of an empty rule line. */
  it('names what a hook default judges, and switches it by id like the others', async () => {
    const props = show();
    const guard = screen.getByRole('listitem', { name: 'tree-guard' });
    expect(within(guard).getByText('a hook on Edit|Write|MultiEdit|NotebookEdit')).toBeTruthy();

    await userEvent.click(within(guard).getByRole('checkbox'));
    expect(props.onSwitchDefault).toHaveBeenCalledWith('tree-guard', false);

    const commit = screen.getByRole('listitem', { name: 'commit' });
    expect(within(commit).getByText('Bash(git commit:*)')).toBeTruthy();
  });

  it('lists each scope\'s rules under its name, and removes one from its scope', async () => {
    const props = show();
    // Not "This machine": that is the section this card sits under.
    expect(screen.getByRole('heading', { name: 'Every session on this machine' })).toBeTruthy();
    const engine = screen.getByRole('list', { name: 'repository engine' });
    expect(within(engine).getByText('Bash(rm -rf:*)')).toBeTruthy();

    await userEvent.click(within(engine).getByRole('button', { name: 'remove Bash(rm -rf:*)' }));
    expect(props.onRemove).toHaveBeenCalledWith({ scope: 'repository', name: 'engine', rule: 'Bash(rm -rf:*)' });
  });

  /** A rarely-used form is one press away, not open on every visit (platform-ux §4). */
  it('adds a rule from a form one press away, to the machine unless a scope is chosen', async () => {
    const props = show();
    expect(screen.queryByRole('textbox', { name: 'the rule' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Add a rule' }));
    const add = screen.getByRole('button', { name: 'add' });
    expect(add).toHaveProperty('disabled', true);

    await userEvent.type(screen.getByRole('textbox', { name: 'the rule' }), 'Bash(make:*)');
    await userEvent.click(add);
    expect(props.onAdd).toHaveBeenCalledWith({ list: 'allow', rule: 'Bash(make:*)', scope: 'machine', name: undefined });
  });

  it('says a file that could not be read, in the driver\'s words', () => {
    show({ rules: { ...RULES, problem: 'permissions.json could not be read, so no rule of it applies. The defaults still do.' } });
    expect(screen.getByText(/could not be read/)).toBeTruthy();
  });

  it('holds its controls while a change is in flight', () => {
    show({ busy: true });
    for (const checkbox of screen.getAllByRole('checkbox')) expect(checkbox).toHaveProperty('disabled', true);
  });
});

// PERM2 (D74): what agents proposed about these rules. A narrowing applied itself at the driver's tick;
// a widening waits here for the person, who alone may let an agent do more.
const PROPOSALS: RuleProposal[] = [
  {
    id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
    why: 'The docs it needs are on the web.', session: 'i9n8t7k6', ask: 'a1b2c3', proposed: '2026-09-24T11:00:00Z',
    settledBy: 'the driver', note: 'it widens what agents may do, so it waits for the person.',
  },
  // A session the driver did not start carries no session: the bridge leaves a null out.
  {
    id: 'p0000001', state: 'applied', action: 'remove', scope: 'repository', name: 'engine', rule: 'Bash(make:*)',
    why: 'Make rebuilds everything.', proposed: '2026-09-24T10:00:00Z', settledBy: 'the driver',
  },
  {
    id: 'p0000000', state: 'declined', action: 'default', scope: 'machine', default: 'tree-guard', on: false,
    why: 'I need to write outside.', session: 's1a2b3c4', proposed: '2026-09-24T09:00:00Z',
    settledBy: 'the person', note: 'Not from a session.',
  },
];

describe('what agents proposed', () => {
  it('shows a waiting widening with what it changes, who proposed it and why, and answers it by id', async () => {
    const onAnswer = vi.fn();
    show({ rules: { ...RULES, proposals: PROPOSALS }, onAnswer });

    const waiting = screen.getByRole('listitem', { name: 'proposal #p0000002' });
    expect(within(waiting).getByText('allow WebFetch for every session on this machine')).toBeTruthy();
    expect(within(waiting).getByText(/session i9n8t7k6 \(ask #a1b2c3\)/)).toBeTruthy();
    expect(within(waiting).getByText('The docs it needs are on the web.')).toBeTruthy();
    // Said where the person decides: why this one is theirs.
    expect(within(waiting).getByText(/widens what agents may do/)).toBeTruthy();

    await userEvent.click(within(waiting).getByRole('button', { name: 'accept' }));
    expect(onAnswer).toHaveBeenCalledWith('p0000002', true);
    await userEvent.click(within(waiting).getByRole('button', { name: 'decline' }));
    expect(onAnswer).toHaveBeenCalledWith('p0000002', false);
  });

  /** History is rarely read, so it is one press away (platform-ux §4) — and it says who settled each. */
  it('keeps the settled ones one press away, each with who settled it and why', async () => {
    show({ rules: { ...RULES, proposals: PROPOSALS }, onAnswer: vi.fn() });
    expect(screen.queryByRole('listitem', { name: 'proposal #p0000001' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Earlier proposals (2)' }));

    const applied = screen.getByRole('listitem', { name: 'proposal #p0000001' });
    expect(within(applied).getByText('remove Bash(make:*) from repository engine')).toBeTruthy();
    expect(within(applied).getByText(/a session the driver did not start/)).toBeTruthy();
    expect(within(applied).getByText('applied')).toBeTruthy();
    expect(within(applied).queryByRole('button', { name: 'accept' })).toBeNull();

    const declined = screen.getByRole('listitem', { name: 'proposal #p0000000' });
    expect(within(declined).getByText('switch the default tree-guard off')).toBeTruthy();
    expect(within(declined).getByText(/Not from a session\./)).toBeTruthy();
  });

  it('is absent when no agent has proposed anything, and from an older shell that never sends proposals', () => {
    show({ onAnswer: vi.fn() });
    expect(screen.queryByRole('heading', { name: 'Proposed by agents' })).toBeNull();
  });

  it('holds its answers while a change is in flight', () => {
    show({ rules: { ...RULES, proposals: PROPOSALS }, onAnswer: vi.fn(), busy: true });
    const waiting = screen.getByRole('listitem', { name: 'proposal #p0000002' });
    expect(within(waiting).getByRole('button', { name: 'accept' })).toHaveProperty('disabled', true);
  });
});
