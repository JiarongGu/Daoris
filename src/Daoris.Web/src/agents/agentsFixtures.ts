import { byTool, type Tool, type ToolDoor } from '../tools';
import { type AgentAccounts, USE_DEFAULTS } from '../settings/accounts';
import type { AgentRulesState } from '../settings/AgentRules';
import { scopeOf } from '../settings/accountsFixtures';

// The Agents place as its stories and molecules' tests hand it (UX6e, D150 §5): the design's machine, three Claude Code
// accounts of which two read signed out, one cooling, the tool's own sign-in never read; Codex with one account; DeepSeek
// Harness (`dsh`) not installed; and an agent a plugin declares. Names are neutral, as every fixture's are.

const minutesFrom = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString();

/** When the accounts were read: an hour and a quarter ago, today. */
export const READ = minutesFrom(-75);

const CLAUDE: ToolDoor = {
  harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4 (Claude Code)',
  wire: 'pipe', structured: true, takesRules: true, signsIn: true, takesKey: true, pinnable: true, updates: 'tool',
  machineDefault: 'account-1', ownLogin: 'unknown', ownRead: null, ownAccount: null,
  workspaceDefaults: [{ workspace: 'forge', profile: 'account-1' }],
  settingsChoices: { models: ['default', 'sonnet', 'opus'], efforts: ['low', 'medium', 'high'] },
  profiles: [
    { name: 'account-1', home: 'C:/somewhere/data/harnesses/claude-code/account-1', login: 'out', account: 'you@work.example', read: READ,
      settings: { model: null, effort: null, perModel: [], problem: null } },
    { name: 'account-2', home: 'C:/somewhere/data/harnesses/claude-code/account-2', login: 'in', account: 'you@home.example', read: READ,
      settings: { model: 'opus', effort: 'high', perModel: [], problem: null } },
    { name: 'account-3', home: 'C:/somewhere/data/harnesses/claude-code/account-3', login: 'out', account: 'spare@example.invalid', read: READ,
      settings: { model: null, effort: null, perModel: [], problem: null } },
  ],
};

const CLAUDE_ACP: ToolDoor = {
  ...CLAUDE, harness: 'claude-code-acp', accountOf: 'claude-code', wire: 'acp', version: '0.9.1', updates: 'pin', takesKey: false,
};

const CODEX: ToolDoor = {
  harness: 'codex-acp', accountOf: 'codex', product: 'Codex', maker: 'OpenAI', present: true, version: '0.159.3', wire: 'acp',
  signsIn: true, pinnable: true, machineDefault: null, ownLogin: 'unknown', settingsChoices: null, workspaceDefaults: [],
  profiles: [{ name: 'account-1', home: 'C:/somewhere/data/harnesses/codex/account-1', login: 'in', account: 'codex@example.invalid', read: READ }],
};

const DSH: ToolDoor = {
  harness: 'dsh', product: 'DeepSeek Harness', maker: 'DeepSeek', present: false, version: null, wire: 'acp', pinnable: true,
  problem: '`dsh` is not on this machine\'s PATH — `daoris agent install dsh` installs it', profiles: [], workspaceDefaults: [],
};

const DECLARED: ToolDoor = {
  harness: 'acme-agent', product: 'Acme agent', maker: 'Acme', present: true, version: '(not asked)', wire: 'acp',
  plugin: 'acme.gate', pinnable: false, signsIn: false, profiles: [], workspaceDefaults: [],
};

/** The roster as the place reads it, a door a row, and the agents a person has, each once. */
export const DOORS: ToolDoor[] = [CLAUDE, CLAUDE_ACP, CODEX, DSH, DECLARED];
export const TOOLS: Tool[] = byTool(DOORS);
export const CLAUDE_TOOL = TOOLS.find((tool) => tool.name === 'claude-code')!;
export const CODEX_TOOL = TOOLS.find((tool) => tool.name === 'codex')!;
export const DSH_TOOL = TOOLS.find((tool) => tool.name === 'dsh')!;

/** How Claude Code's accounts are used: this machine's list of three, `work` on its own two, account-2 cooling. */
export const CLAUDE_USE: AgentAccounts = {
  agent: 'claude-code',
  speaks: true,
  own: {},
  accounts: [
    { name: 'account-1', running: 0 },
    {
      name: 'account-2', running: 1,
      cooling: { until: minutesFrom(60 * 40), stated: true, window: 'weekly', seen: READ, assumedZone: false, notBelieved: false },
      said: {
        seen: READ,
        windows: [{ window: 'weekly', used: 1, reset: minutesFrom(60 * 40), standing: 'refused', credits: false, seen: READ }],
      },
    },
    { name: 'account-3', running: 0 },
  ],
  scopes: [
    scopeOf({ default: 'account-1', list: ['account-1', 'account-2', 'account-3'], begins: 'account-1', use: USE_DEFAULTS }),
    scopeOf({ workspace: 'work', list: ['account-2', 'account-1'], begins: 'account-2', use: USE_DEFAULTS }),
  ],
};

export const CODEX_USE: AgentAccounts = {
  agent: 'codex', speaks: false, own: {}, accounts: [{ name: 'account-1', running: 0 }], scopes: [scopeOf()],
};

/**
 * The install's shape, as the UX7 design §4.7 draws it (UX7b): account-1 signed in with a session on it, first in work's
 * list; account-2 a list holds, unknown and never read; *personal*, named by the person (ACCT2), cooling; a new account
 * (`acct-…`, ACCT2's fresh id) signed in and in no list (ACCT1); your own sign-in never read, which forge starts on since
 * neither it nor this machine names an account.
 */
const INSTALL_DOOR: ToolDoor = {
  ...CLAUDE, machineDefault: null, workspaceDefaults: [], version: '2.1.288 (Claude Code)',
  profiles: [
    { name: 'account-1', home: 'C:/somewhere/data/harnesses/claude-code/account-1', login: 'in', account: 'you@work.example', read: READ,
      places: [{ workspace: 'work', list: true, default: false }], nowhere: false },
    { name: 'account-2', home: 'C:/somewhere/data/harnesses/claude-code/account-2', login: 'unknown', read: null,
      places: [{ workspace: 'work', list: true, default: false }], nowhere: false },
    { name: 'account-4', displayName: 'personal', home: 'C:/somewhere/data/harnesses/claude-code/account-4', login: 'in',
      account: 'you@home.example', read: READ, places: [{ workspace: 'work', list: true, default: false }], nowhere: false },
    { name: 'acct-5e1f0a2b', home: 'C:/somewhere/data/harnesses/claude-code/acct-5e1f0a2b', login: 'in', account: 'spare@example.invalid',
      read: READ, places: [], nowhere: true },
  ],
};

export const INSTALL_TOOL: Tool = byTool([
  INSTALL_DOOR, { ...CLAUDE_ACP, version: '0.84.0', profiles: INSTALL_DOOR.profiles, machineDefault: null, workspaceDefaults: [] },
])[0]!;

export const INSTALL_USE: AgentAccounts = {
  agent: 'claude-code',
  speaks: true,
  own: {},
  accounts: [
    { name: 'account-1', running: 1 },
    { name: 'account-2', running: 0 },
    {
      name: 'account-4', running: 0,
      cooling: { until: minutesFrom(60 * 20), stated: true, window: 'weekly', seen: READ, assumedZone: false, notBelieved: false },
    },
    { name: 'acct-5e1f0a2b', running: 0 },
  ],
  scopes: [
    scopeOf(),
    scopeOf({ workspace: 'work', list: ['account-1', 'account-2', 'account-4'], begins: 'account-1', use: USE_DEFAULTS }),
  ],
};

/** The install's two workspaces. */
export const INSTALL_WORKSPACES = ['forge', 'work'];

/** Daoris's four defaults on, one rule of the person's for every session, one proposal waiting. */
export const RULES: AgentRulesState = {
  path: 'C:/somewhere/data/permissions.json',
  defaults: [
    { id: 'tree-guard', list: 'deny', rules: [], why: 'keeps every write inside the session\'s own tree', on: true, hook: 'Edit, Write' },
    { id: 'no-push', list: 'deny', rules: ['Bash(git push:*)'], why: 'a push is the person\'s', on: true },
    { id: 'no-force', list: 'deny', rules: ['Bash(git reset --hard:*)'], why: 'a hard reset loses work', on: true },
    { id: 'connector', list: 'allow', rules: ['mcp__daoris__*'], why: 'the connector is how an agent reaches Daoris', on: true },
  ],
  scopes: [{ scope: 'machine', allow: ['Bash(npm test:*)'], ask: [], deny: [] }],
  proposals: [{
    id: 'p1', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'Bash(dotnet test:*)',
    why: 'the tests run on every change', session: 's1a2b3c4', proposed: minutesFrom(-30),
  }],
};

/** What each Claude Code account has carried (TOOL3). */
export const USAGE = [
  { harness: 'claude-code', profile: 'account-2', sessions: 4, used: 412_000 },
  { harness: 'claude-code-acp', profile: 'account-1', sessions: 2, used: 96_000 },
];

export const WORKSPACES = ['forge', 'work'];
