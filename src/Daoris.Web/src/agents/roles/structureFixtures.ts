import type { RoleStructure, StructureChoice, StructureWork } from '../../data/wiring';
import type { RepositoryLanding } from '../../settings/repository/Landings';
import type { RepositoryLine } from '../../settings/repository/Lines';
import { TOOLS } from '../agentsFixtures';
import type { PluginRef, WorkflowRead } from './structure';

// The roles' structure as the stories and tests hand it (COMPANYVIEW1d): this machine with its nine works, a workspace taking them, a
// repository with a role of its own whose first choice is signed out, one taking this machine's, and a repository in no workspace whose
// build has no role and whose helping rule does not read. Neutral names, as every fixture's are.

export const READ_AT = '2026-10-31T08:00:00.000Z';

export const WORKS = ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'];

const roleOf = (id: string, nameEn: string, nameZh: string, scope: { kind: string; id?: string } = { kind: 'machine' }, revision = 0) =>
  ({ id, nameEn, nameZh, scope, revision });

const BUILD = roleOf('build', 'Build', '开发');
const ENGINE = roleOf('engine-build', 'Engine build', '引擎开发', { kind: 'repository', id: 'engine' }, 3);

export const choice = (agent: string, code = 'ready', patch: Partial<StructureChoice> = {}): StructureChoice => ({
  agent, model: 'opus', effort: 'high', from: '1', state: { code, at: READ_AT }, ...patch,
});

const work = (name: string, role: StructureWork['role'] | null, choices: StructureChoice[], patch: Partial<StructureWork> = {}): StructureWork => ({
  work: name, ...(role ? { role } : {}), choices, ...patch,
});

const HAND_OVER = { consent: 'on', agents: [], concurrency: 3, source: 'default', requirement: { kind: 'hand-over', consent: 'ask', source: 'default' } };

/** This machine's nine works. */
export const MACHINE_WORKS: StructureWork[] = [
  work('build', BUILD, [choice('claude-code'), choice('codex-acp', 'signed-out', { from: '2', model: undefined, effort: undefined })], { policy: HAND_OVER }),
  work('design', BUILD, [choice('claude-code')], { policy: HAND_OVER }),
  work('chat', roleOf('chat', 'Chat', '对话'), [choice('claude-code', 'ready', { from: 'before', model: undefined, effort: undefined })], { policy: { ...HAND_OVER, concurrency: undefined } }),
  work('ask-daoris', roleOf('ask-daoris', 'Ask Daoris', '问道衍'), [choice('claude-code')], { policy: { consent: 'on', agents: [], concurrency: 1, source: 'machine' } }),
  work('intake', roleOf('intake', 'Intake', '受理'), [choice('claude-code', 'not-read')], { policy: { consent: 'off', agents: [], concurrency: 1, source: 'default' } }),
  work('help', roleOf('help', 'Helping', '协助'), [choice('claude-code')], {
    policy: { consent: 'ask', agents: ['claude-code'], concurrency: 2, minutes: 10, source: 'workspace', requirement: { kind: 'help', setUp: true } },
  }),
  work('partner', roleOf('partner', 'Partnering', '搭档'), [choice('claude-code')], { policy: { consent: 'not-a-rule', agents: [], source: 'default', requirement: { kind: 'partner', measured: true } } }),
  work('review', roleOf('review', 'Second opinion', '第二意见'), [choice('codex-acp', 'cooling', { state: { code: 'cooling', at: READ_AT, until: '2026-10-31T11:00:00.000Z' } })], {
    policy: { consent: 'on', agents: ['codex-acp'], source: 'repository', minutes: 15 },
  }),
  work('brainstorm', roleOf('brainstorm', 'Brainstorming', '头脑风暴'), [choice('claude-code'), choice('dsh', 'unavailable', { from: 'rule-default', model: undefined, effort: undefined })], {
    policy: { consent: 'on', agents: ['claude-code', 'dsh'], source: 'machine' },
  }),
];

/** The same, a place taking them. */
const taken = (): StructureWork[] => MACHINE_WORKS.map((each) => ({ ...each }));

const withWork = (works: StructureWork[], name: string, patch: StructureWork): StructureWork[] => works.map((each) => (each.work === name ? patch : each));

const ENGINE_WORKS = withWork(taken(), 'build', work('build', ENGINE, [choice('claude-code', 'signed-out', { model: 'sonnet' })], { policy: HAND_OVER }));

const TOOLS_WORKS = withWork(
  withWork(taken(), 'build', {
    work: 'build', held: 'No role takes `build` here, so its starts hold until one is given it: Agents → Roles, or `daoris driver role`.', choices: [],
  }),
  'help', { work: 'help', role: roleOf('help', 'Helping', '协助'), choices: [choice('claude-code')], policyFailed: 'driver.json does not read: line 4.' },
);

export const STRUCTURE: RoleStructure = {
  works: WORKS,
  roles: [
    { id: 'build', state: 'seed', seeded: true, nameEn: 'Build', nameZh: '开发', takes: ['build'], scopeKind: 'machine', revision: 0 },
    { id: 'engine-build', state: 'set', seeded: false, nameEn: 'Engine build', nameZh: '引擎开发', takes: ['build'], scopeKind: 'repository', scopeId: 'engine', revision: 3 },
    { id: 'chat', state: 'seed', seeded: true, nameEn: 'Chat', nameZh: '对话', takes: ['chat'], scopeKind: 'machine', revision: 0 },
  ],
  places: [
    { works: MACHINE_WORKS },
    { workspace: 'aurora', works: taken() },
    { workspace: 'default', works: taken() },
    { workspace: 'aurora', repository: 'engine', works: ENGINE_WORKS },
    { workspace: 'aurora', repository: 'site', works: taken() },
    { workspace: 'default', repository: 'tools', works: TOOLS_WORKS },
  ],
  agents: [
    { agent: 'claude-code', known: true, installed: true, version: '2.1.4' },
    { agent: 'codex-acp', known: true, installed: true, version: '0.159.3' },
    { agent: 'dsh', known: true, installed: false },
    { agent: 'ghost-agent', known: false },
  ],
};

export const REGISTRY = [
  { repository: 'engine', workspace: 'aurora' },
  { repository: 'site', workspace: 'aurora' },
  { repository: 'tools' },
];

export const PLUGINS: PluginRef[] = [
  { id: 'acme.gate', name: 'Acme gate', harnesses: ['acme-agent'] },
  { id: 'github-pull-request', name: 'GitHub pull request', harnesses: [] },
];

export const LINES: { lines: RepositoryLine[]; landings: RepositoryLanding[] } = {
  lines: [
    { repository: 'engine', workspace: 'aurora', branch: 'main', source: 'repository' },
    { repository: 'site', workspace: 'aurora', branch: 'main', source: 'workspace' },
    { repository: 'tools', workspace: 'default', source: 'none', notReadYet: true },
  ],
  landings: [
    { repository: 'engine', workspace: 'aurora', form: 'branch', plugin: 'github-pull-request', source: 'repository' },
    { repository: 'site', workspace: 'aurora', form: 'merge', source: 'default' },
  ],
};

export const DRIVER = { drivable: ['engine', 'site'], holds: [] as string[] };

/**
 * A structure past 24 nodes (COMPANYVIEW1f): aurora's eight repositories and borealis's six each with a building role of its own, two
 * more of aurora's taking this machine's, so each workspace folds to one node.
 */
export function largeStructure(): { structure: RoleStructure; registry: { repository: string; workspace?: string }[] } {
  const own = [...Array.from({ length: 8 }, (_, n) => ['aurora', `aurora-${n + 1}`]), ...Array.from({ length: 6 }, (_, n) => ['borealis', `borealis-${n + 1}`])];
  const taking = [['aurora', 'aurora-docs'], ['aurora', 'aurora-site']];
  const roleFor = (repository: string) => roleOf(`${repository}-build`, `${repository} build`, `${repository} 开发`, { kind: 'repository', id: repository }, 1);
  const buildAt = (role: ReturnType<typeof roleOf>) => [work('build', role, [choice('claude-code', 'ready', { model: 'sonnet' })])];
  return {
    structure: {
      works: ['build'],
      roles: [
        { id: 'build', state: 'seed', seeded: true, nameEn: 'Build', nameZh: '开发', takes: ['build'], scopeKind: 'machine', revision: 0 },
        ...own.map(([, repository]) => ({
          id: `${repository}-build`, state: 'set' as const, seeded: false, nameEn: `${repository} build`, nameZh: `${repository} 开发`, takes: ['build'],
          scopeKind: 'repository', scopeId: repository, revision: 1,
        })),
      ],
      places: [
        { works: buildAt(BUILD) },
        { workspace: 'aurora', works: buildAt(BUILD) },
        { workspace: 'borealis', works: buildAt(BUILD) },
        ...own.map(([workspace, repository]) => ({ workspace, repository, works: buildAt(roleFor(repository!)) })),
        ...taking.map(([workspace, repository]) => ({ workspace, repository, works: buildAt(BUILD) })),
      ],
      agents: [{ agent: 'claude-code', known: true, installed: true, version: '2.1.4' }],
    },
    registry: [...own, ...taking].map(([workspace, repository]) => ({ repository: repository!, workspace })),
  };
}

export const WORKFLOWS: Record<string, WorkflowRead> = {
  engine: { read: 'ok', steps: 5, integration: 'proposed' },
  site: { read: 'ok', steps: 4, integration: 'none' },
  tools: { read: 'failed' },
};

export { TOOLS };
