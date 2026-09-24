import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { AgentRules, type AgentRulesState } from './AgentRules';

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
    onSwitchDefault: vi.fn(), onRemove: vi.fn(), onAdd: vi.fn(), ...extra,
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
