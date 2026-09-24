import type { Meta, StoryObj } from '@storybook/react-vite';
import { AgentRules, type AgentRulesState } from './AgentRules';

// What agents may do (PERM1, D72): Claude Code's own permission rules in Daoris's scopes — the shapes
// the driver's RULES answer carries. The machine's scope has no name on the wire (the bridge leaves a
// null out), and a file read cleanly carries no problem.

const DEFAULTS: AgentRulesState['defaults'] = [
  {
    id: 'connector', list: 'allow', on: true,
    rules: ['mcp__daoris-knowledge__quest_list', 'mcp__daoris-knowledge__quest_respond', 'mcp__daoris-knowledge__quest_publish'],
    why: "A session takes and closes its own quest, and publishes what it finds for others, through Daoris's connector — and anything it would have to ask for is refused.",
  },
  {
    id: 'commit', list: 'allow', on: true,
    rules: ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)'],
    why: 'A session commits its own work in its own tree, which D37 makes automatic — the push is still refused.',
  },
  {
    id: 'no-push', list: 'deny', on: true,
    rules: ['Bash(git push)', 'Bash(git push:*)'],
    why: "A push leaves this machine, and that stays the person's (D37).",
  },
  // A hook, not a rule (PERM3): the driver answers the tools it judges, and no rules.
  {
    id: 'tree-guard', list: 'deny', on: true, rules: [], hook: 'Edit|Write|MultiEdit|NotebookEdit',
    why: 'A session writes files only inside its own tree: an edit or a write anywhere else is refused, through links as well (D51). A change needed elsewhere is a quest.',
  },
];

const PATH = 'C:/somewhere/data/permissions.json';

const base: AgentRulesState = {
  path: PATH,
  defaults: DEFAULTS,
  scopes: [{ scope: 'machine', allow: [], ask: [], deny: [] }],
};

const meta = {
  title: 'Settings/AgentRules',
  component: AgentRules,
  args: {
    rules: base,
    circles: ['default', 'team'],
    repositories: ['engine', 'game'],
    onSwitchDefault: () => {},
    onRemove: () => {},
    onAdd: () => {},
  },
} satisfies Meta<typeof AgentRules>;

export default meta;
type Story = StoryObj<typeof meta>;

/** A machine that wrote nothing: the defaults alone, each with its reason and its switch. */
export const DefaultsOnly: Story = {};

/** Rules in every scope — the machine's, a circle's, a repository's. */
export const EveryScope: Story = {
  args: {
    rules: {
      ...base,
      scopes: [
        { scope: 'machine', allow: ['Bash(npm run test:*)'], ask: [], deny: [] },
        { scope: 'workspace', name: 'default', allow: [], ask: ['WebFetch'], deny: ['Bash(rm -rf:*)'] },
        { scope: 'repository', name: 'engine', allow: ['Bash(make:*)', 'Edit(/docs/**)'], ask: [], deny: [] },
      ],
    },
  },
};

/** A default switched off reads as off, and still says what it would do. */
export const ADefaultOff: Story = {
  args: { rules: { ...base, defaults: [DEFAULTS[0]!, { ...DEFAULTS[1]!, on: false }] } },
};

/** A file that could not be read: said, in the driver's words, and the defaults still hold. */
export const Unreadable: Story = {
  args: {
    rules: { ...base, problem: 'permissions.json could not be read (unexpected token), so no rule of it applies. The defaults still do.' },
  },
};
