import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS } from './structureFixtures';
import {
  buildStructure, heldMarks, litOf, NO_FILTERS, nodeOfRow, placesToAsk, sectionOf, type StructureFilters, usedByAgent, workflowReadOf,
} from './roleStructure';

// COMPANYVIEW1d (the company-view design §3.3 to §3.5, §4): the rows of Work, Accounts and By repository, built once over the driver's
// answer, with one id each. A row says the driver's answer; it resolves nothing itself.

const build = (filters: Partial<StructureFilters> = {}, workflows = WORKFLOWS) => buildStructure({
  structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
  driver: DRIVER, workflows, filters: { ...NO_FILTERS, ...filters },
});

describe('buildStructure', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('asks this machine, each workspace and each driven repository once, and no imported one that is not driven', () => {
    expect(placesToAsk(REGISTRY, DRIVER.drivable)).toEqual([
      {}, { workspace: 'aurora' }, { workspace: 'default' },
      { workspace: 'aurora', repository: 'engine' }, { workspace: 'aurora', repository: 'site' },
    ]);
    expect(placesToAsk(REGISTRY, [])).toEqual([{}, { workspace: 'aurora' }, { workspace: 'default' }]);
  });

  describe('Work', () => {
    it('makes a row per work with one id each, and the role taking it here', () => {
      const { works } = build();
      expect(works.map((row) => row.id)).toEqual(STRUCTURE.works.map((work) => `work:${work}`));
      expect(works.find((row) => row.work === 'build')?.role?.id).toBe('build');
    });

    it('counts the places that have their own, and for a workspace only its repositories', () => {
      // Machine: engine (its own role) and tools (no role) differ from build's answer; site takes it.
      expect(build().works.find((row) => row.work === 'build')?.own).toBe(2);
      expect(build({ workspace: 'aurora' }).works.find((row) => row.work === 'build')?.own).toBe(1);
      expect(build().works.find((row) => row.work === 'chat')?.own).toBe(0);
    });

    it('says design takes building\'s role, and says no role takes a work only when the driver said none', () => {
      const design = build().works.find((row) => row.work === 'design')!;
      expect(design.takesBuilding).toBe(true);
      expect(design.reasons).toEqual([]);
      expect(build().works.find((row) => row.work === 'build')?.takesBuilding).toBe(false);
    });

    it('reads the rule as the driver resolved it, with where it was set, and partnering as not a rule yet', () => {
      const { works } = build();
      expect(works.find((row) => row.work === 'help')?.rule).toEqual({
        kind: 'rule', consent: 'ask', agents: ['claude-code'], concurrency: 2, minutes: 10, source: 'workspace',
      });
      expect(works.find((row) => row.work === 'partner')?.rule).toEqual({ kind: 'not-a-rule' });
      expect(works.find((row) => row.work === 'chat')?.rule).toMatchObject({ kind: 'rule', source: 'default' });
    });

    it('names a fenced agent no choice names, which runs at account default', () => {
      expect(build().works.find((row) => row.work === 'brainstorm')?.atDefault).toEqual(['dsh']);
      expect(build().works.find((row) => row.work === 'help')?.atDefault).toEqual([]);
    });

    it('holds a work whose every choice cannot run, by the last reading', () => {
      const review = build().works.find((row) => row.work === 'review')!;
      expect(review.reasons).toEqual(['cannot-run']);
      // The build has one that is ready and one signed out: a start goes on the first.
      expect(build().works.find((row) => row.work === 'build')?.reasons).toEqual([]);
    });

    it('says a held start in the driver\'s sentence at its place', () => {
      const tools = buildStructure({
        structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: null, plugins: null, lines: null, driver: null, workflows: {}, filters: NO_FILTERS,
      });
      expect(tools.works.some((row) => row.held)).toBe(false);
      const row = build().repositories.flatMap((group) => group.repositories).find((each) => each.repository === 'tools')!;
      expect(row.works.find((each) => each.work === 'build')?.held).toMatch(/^No role takes `build` here/);
    });

    it('says a catalogue that does not read on every work, never an empty list', () => {
      const failed = buildStructure({
        structure: { ...STRUCTURE, roles: [], catalogue: { why: 'roles.json does not read' } }, registry: REGISTRY, tools: TOOLS, accounts: null,
        plugins: null, lines: null, driver: null, workflows: {}, filters: NO_FILTERS,
      });
      expect(failed.catalogueFailed).toBe('roles.json does not read');
      expect(failed.works.every((row) => row.reasons.includes('catalogue'))).toBe(true);
    });

    it('keeps a part\'s failure on its row', () => {
      const places = STRUCTURE.places.map((place) => ({
        ...place,
        works: place.works.map((each) => (each.work === 'help' ? { ...each, policyFailed: 'unreadable', choicesFailed: 'unreadable too' } : each)),
      }));
      const row = buildStructure({
        structure: { ...STRUCTURE, places }, registry: REGISTRY, tools: TOOLS, accounts: null, plugins: null, lines: null, driver: null, workflows: {}, filters: NO_FILTERS,
      }).works.find((each) => each.work === 'help')!;
      expect(row.rule).toEqual({ kind: 'failed', why: 'unreadable' });
      expect(row.reasons).toEqual(expect.arrayContaining(['policy-failed', 'choices-failed']));
    });
  });

  describe('Accounts', () => {
    it('makes a row per agent a choice names, folded to the roster\'s tool, with one id each', () => {
      const { accounts } = build();
      expect(accounts.map((row) => row.id)).toEqual(['agent:claude-code', 'agent:codex', 'agent:dsh', 'agent:ghost-agent'].filter((id) => accounts.some((row) => row.id === id)));
      expect(accounts.map((row) => row.id)).toContain('agent:codex');
      expect(accounts.find((row) => row.agent === 'codex')?.product).toBe('Codex');
    });

    it('says who uses an agent at which position, the roles once each', () => {
      const claude = build().accounts.find((row) => row.agent === 'claude-code')!;
      const build_ = claude.usedBy.find((used) => used.role.id === 'build' && used.from === '1')!;
      expect(build_.works).toEqual(['build', 'design']);
      expect(claude.usedBy.find((used) => used.role.id === 'chat')?.from).toBe('before');
      const codex = build().accounts.find((row) => row.agent === 'codex')!;
      expect(codex.usedBy.find((used) => used.role.id === 'build')).toMatchObject({ from: '2', state: { code: 'signed-out' } });
    });

    it('says a named agent not installed, one this build does not know, and a choice that cannot run', () => {
      const { accounts } = build();
      expect(accounts.find((row) => row.agent === 'dsh')?.reasons).toContain('not-installed');
      expect(accounts.find((row) => row.agent === 'ghost-agent')?.reasons).toContain('not-known');
      expect(accounts.find((row) => row.agent === 'codex')?.reasons).toContain('cannot-run');
      expect(accounts.find((row) => row.agent === 'claude-code')?.reasons).toEqual([]);
    });

    it('says the plugin that declared it, from the roster\'s door', () => {
      const declared = buildStructure({
        structure: { ...STRUCTURE, agents: [...STRUCTURE.agents, { agent: 'acme-agent', known: true, installed: true }] },
        registry: REGISTRY, tools: TOOLS, accounts: null, plugins: PLUGINS, lines: null, driver: null, workflows: {}, filters: NO_FILTERS,
      });
      expect(declared.accounts.find((row) => row.agent === 'acme-agent')?.plugin).toBe('acme.gate');
    });

    it('carries the Agents list\'s phrase and when the accounts were read', () => {
      const claude = build().accounts.find((row) => row.agent === 'claude-code')!;
      expect(claude.phrase).toMatch(/3 accounts/);
      expect(claude.read).not.toBeNull();
    });
  });

  describe('By repository', () => {
    const rows = () => build().repositories;

    it('groups repositories by workspace, a repository in none in default', () => {
      expect(rows().map((group) => group.workspace)).toEqual(['aurora', 'default']);
      expect(rows()[0]!.repositories.map((row) => row.id)).toEqual(['place:aurora/engine', 'place:aurora/site']);
    });

    it('counts the works that differ from this machine\'s roles, none for one that takes them', () => {
      const [aurora] = rows();
      expect(aurora!.repositories.find((row) => row.repository === 'engine')?.differs).toBe(1);
      expect(aurora!.repositories.find((row) => row.repository === 'site')?.differs).toBe(0);
      expect(aurora!.differs).toBe(1);
    });

    it('says line, landing, driving and workflow from the reads', () => {
      const engine = rows()[0]!.repositories[0]!;
      expect(engine.line).toEqual({ branch: 'main', source: 'repository' });
      expect(engine.landing).toMatchObject({ form: 'branch', plugin: 'github-pull-request' });
      expect(engine.driving).toBe('driven');
      expect(engine.workflow).toEqual(WORKFLOWS.engine);
      const tools = rows()[1]!.repositories[0]!;
      expect(tools.line).toBeNull();
      expect(tools.driving).toBe('not-driven');
      expect(tools.workflow).toEqual({ read: 'failed' });
    });

    it('names the plugins whose landing it uses, and a proposed integration declaration needs a look', () => {
      const engine = rows()[0]!.repositories[0]!;
      expect(engine.plugins).toEqual([{ id: 'github-pull-request', name: 'GitHub pull request', via: ['landing'] }]);
      expect(engine.reasons).toContain('integration');
      expect(rows()[0]!.repositories[1]!.reasons).not.toContain('integration');
    });

    it('says a read not made as none, never as a state', () => {
      const none = build({}, {}).repositories[0]!.repositories[0]!;
      expect(none.workflow).toBeNull();
    });
  });

  describe('the shared filters', () => {
    it('narrows Work and Accounts to a workspace\'s scope and By repository to its group', () => {
      const aurora = build({ workspace: 'aurora' });
      expect(aurora.scope).toEqual({ workspace: 'aurora' });
      expect(aurora.repositories.map((group) => group.workspace)).toEqual(['aurora']);
    });

    it('narrows to one work in each section', () => {
      const one = build({ work: 'review' });
      expect(one.works.map((row) => row.work)).toEqual(['review']);
      expect(one.accounts.map((row) => row.agent)).toEqual(['codex']);
      expect(one.accounts[0]!.usedBy.every((used) => used.works.includes('review'))).toBe(true);
      expect(one.repositories.flatMap((group) => group.repositories).every((row) => row.works.some((each) => each.work === 'review'))).toBe(true);
    });

    it('narrows to one agent, by a door or by the tool', () => {
      expect(build({ agent: 'codex' }).accounts.map((row) => row.agent)).toEqual(['codex']);
      expect(build({ agent: 'codex' }).works.map((row) => row.work)).toEqual(expect.arrayContaining(['build', 'review']));
      expect(build({ agent: 'codex' }).works.map((row) => row.work)).not.toContain('chat');
    });

    it('narrows to a name across roles, works, agents and repositories, ignoring case', () => {
      expect(build({ name: 'ENGINE' }).repositories.flatMap((group) => group.repositories).map((row) => row.repository)).toEqual(['engine']);
      expect(build({ name: '开发' }).works.map((row) => row.work)).toEqual(expect.arrayContaining(['build']));
    });

    it('keeps only what needs a look, and it is this builder\'s alone', () => {
      const look = build({ needsLook: true });
      expect(look.works.map((row) => row.work)).toEqual(['review']);
      expect(look.accounts.map((row) => row.agent).sort()).toEqual(['codex', 'dsh', 'ghost-agent']);
      expect(look.repositories.flatMap((group) => group.repositories).map((row) => row.repository)).toEqual(['engine', 'site', 'tools']);
      // The second opinion cools at every place that takes this machine's roles, so each place needs a look.
    });
  });

  it('reads the options of the filters from the registry and the answer', () => {
    const { options } = build();
    expect(options.workspaces).toEqual(['aurora', 'default']);
    expect(options.works).toEqual(STRUCTURE.works);
  });
});

// COMPANYVIEW1f (the company-view design §4): the diagram's nodes and edges, built by the same function over the same answer, each with
// the id its row has. Three columns: places, roles and agents; an edge only from a record.
describe('the diagram', () => {
  const APPOINTED = {
    stewardOffered: true, supervisorOffered: false,
    appointments: [{ role: 'supervisor', scope: 'workspace:aurora', workspace: 'aurora', repository: '', dormant: true }],
  };
  const diagramOf = (filters: Partial<StructureFilters> = {}, patch: Partial<Parameters<typeof buildStructure>[0]> = {}) => buildStructure({
    structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
    driver: DRIVER, workflows: WORKFLOWS, filters: { ...NO_FILTERS, ...filters }, ...patch,
  }).diagram;
  const ids = (kind: string, diagram = diagramOf()) => diagram.nodes.filter((node) => node.kind === kind).map((node) => node.id);
  const staffed = (role: string, diagram = diagramOf()) => diagram.edges
    .flatMap((edge) => (edge.kind === 'staffed' && edge.from === role ? [[edge.to, edge.position, edge.dashed]] : []));

  it("draws This Daoris and each place that sets something of its own, and counts the rest in its workspace's node", () => {
    expect(ids('place')).toEqual(['place:machine', 'place:aurora', 'place:aurora/engine', 'place:default/tools']);
    const node = (id: string) => diagramOf().nodes.find((each) => each.id === id);
    // Aurora sets nothing of its own and holds site, which takes this machine's roles; default's one repository is drawn itself.
    expect(node('place:aurora')).toMatchObject({ own: false, counted: 1, group: 'aurora' });
    expect(node('place:aurora/engine')).toMatchObject({ own: true, ownRoles: true, counted: 0, group: 'aurora' });
    // A start held is the place's own answer.
    expect(node('place:default/tools')).toMatchObject({ own: true, group: 'default' });
    expect(node('place:machine')).toMatchObject({ group: null });
  });

  it('draws each working role once, placed from the scope its record names', () => {
    expect(ids('role')).toEqual(['role:build', 'role:engine-build', 'role:chat', 'role:ask-daoris', 'role:intake', 'role:help', 'role:partner', 'role:review', 'role:brainstorm']);
    const placed = diagramOf().edges.filter((edge) => edge.kind === 'placed').map((edge) => `${edge.from} > ${edge.to}`);
    expect(placed).toContain('place:machine > role:build');
    expect(placed).toContain('place:aurora/engine > role:engine-build');
    expect(placed).not.toContain('place:machine > role:engine-build');
    expect(placed).toHaveLength(9);
  });

  it('draws the appointed roles where offered or appointed, each appointment an edge, dashed while dormant', () => {
    expect(ids('appointed')).toEqual([]);
    const diagram = diagramOf({}, { appointed: APPOINTED });
    expect(ids('appointed', diagram)).toEqual(['appointed:steward', 'appointed:supervisor']);
    expect(diagram.edges.filter((edge) => edge.kind === 'appointed')).toEqual([
      { id: 'appointed:place:aurora>appointed:supervisor', kind: 'appointed', from: 'place:aurora', to: 'appointed:supervisor', dashed: true },
    ]);
    // An appointment is a place's own, though its roles are still this machine's.
    expect(diagram.nodes.find((node) => node.id === 'place:aurora')).toMatchObject({ own: true, ownRoles: false });
  });

  it('staffs a role choice by choice, numbered, dashed where the last reading says that choice cannot run; a rule is never drawn', () => {
    expect(staffed('role:build')).toEqual([['agent:claude-code', '1', false], ['agent:codex', '2', true]]);
    expect(staffed('role:engine-build')).toEqual([['agent:claude-code', '1', true]]);
    expect(staffed('role:review')).toEqual([['agent:codex', '1', true]]);
    expect(staffed('role:chat')).toEqual([['agent:claude-code', 'before', false]]);
    // dsh runs brainstorms at account default by its rule; no choice names it.
    expect(staffed('role:brainstorm')).toEqual([['agent:claude-code', '1', false]]);
  });

  it('draws each agent a choice names with its accounts, and one the roster lacks as written, never invented', () => {
    expect(ids('agent')).toEqual(['agent:claude-code', 'agent:codex']);
    const claude = diagramOf().nodes.find((node) => node.id === 'agent:claude-code');
    expect(claude).toMatchObject({ kind: 'agent', product: 'Claude Code', known: true });
    const ghostly = {
      ...STRUCTURE,
      places: STRUCTURE.places.map((place) => ({
        ...place, works: place.works.map((work) => (work.work === 'help' ? { ...work, choices: [{ ...work.choices[0]!, agent: 'ghost-agent' }] } : work)),
      })),
    };
    const ghost = diagramOf({}, { structure: ghostly }).nodes.find((node) => node.id === 'agent:ghost-agent');
    expect(ghost).toMatchObject({ kind: 'agent', product: 'ghost-agent', known: false, phrase: null });
  });

  it("lights a place's path: per work the role the driver answered there, where that role is placed, and its staffing", () => {
    const diagram = diagramOf();
    const lit = litOf(diagram, 'place:aurora/engine');
    expect([...lit.nodes]).toEqual(expect.arrayContaining(['place:aurora/engine', 'role:engine-build', 'role:chat', 'place:machine', 'agent:claude-code']));
    expect([...lit.edges]).toContain('placed:place:machine>role:chat');
    expect([...lit.edges]).toContain('staffed:role:engine-build>agent:claude-code:1');
    // Its design still takes this machine's Build, so Build is lit too; site's node is not.
    expect(lit.nodes.has('role:build')).toBe(true);
    expect(lit.nodes.has('place:aurora')).toBe(false);
  });

  it('lights every role leaning on an agent, and what a role is placed in and staffed by', () => {
    const diagram = diagramOf();
    const codex = litOf(diagram, 'agent:codex');
    expect([...codex.nodes].sort()).toEqual(['agent:codex', 'role:build', 'role:review']);
    expect([...codex.edges].sort()).toEqual(['staffed:role:build>agent:codex:2', 'staffed:role:review>agent:codex:1']);
    const engine = litOf(diagram, 'role:engine-build');
    expect([...engine.nodes].sort()).toEqual(['agent:claude-code', 'place:aurora/engine', 'role:engine-build']);
    expect(litOf(diagram, null).nodes.size).toBe(0);
  });

  it('keeps to the filters: a workspace, a work, an agent, a name, and what needs a look', () => {
    expect(ids('place', diagramOf({ workspace: 'aurora' }))).toEqual(['place:machine', 'place:aurora', 'place:aurora/engine']);
    const codex = diagramOf({ agent: 'codex' });
    expect(ids('role', codex)).toEqual(['role:build', 'role:review']);
    expect(ids('agent', codex)).toEqual(['agent:codex']);
    expect(ids('role', diagramOf({ work: 'build' }))).toEqual(['role:build', 'role:engine-build']);
    expect(ids('place', diagramOf({ work: 'build' }))).toEqual(['place:machine', 'place:aurora/engine']);
    expect(ids('role', diagramOf({ name: 'engine' }))).toEqual(['role:engine-build']);
    expect(ids('role', diagramOf({ needsLook: true }))).toEqual(['role:engine-build', 'role:review']);
    // Every edge kept has both its ends kept.
    for (const diagram of [codex, diagramOf({ workspace: 'aurora' }), diagramOf({ needsLook: true })]) {
      const kept = new Set(diagram.nodes.map((node) => node.id));
      expect(diagram.edges.every((edge) => kept.has(edge.from) && kept.has(edge.to))).toBe(true);
    }
  });

  it("finds a row's node, a place that sets nothing of its own in its workspace's, and the section a node's row is in", () => {
    const structure = build();
    expect(nodeOfRow(structure, 'work:build')).toBe('role:build');
    expect(nodeOfRow(structure, 'agent:codex')).toBe('agent:codex');
    expect(nodeOfRow(structure, 'place:aurora/site')).toBe('place:aurora');
    expect(nodeOfRow(structure, 'place:aurora/engine')).toBe('place:aurora/engine');
    expect(nodeOfRow(structure, 'role:chat')).toBe('role:chat');
    expect(nodeOfRow(structure, 'place:default')).toBeNull();
    expect(['place:machine', 'place:aurora', 'place:aurora/engine', 'role:build', 'appointed:steward', 'agent:codex'].map(sectionOf))
      .toEqual(['work', 'repository', 'repository', 'working', 'appointed', 'accounts']);
  });
});

describe('workflowReadOf', () => {
  const current = (integration: unknown) => ({ steps: [{}, {}], start: integration === undefined ? undefined : { integration } }) as never;

  it('reads the integration declaration as none, proposed or accepted, and leaves it unsaid on a driver that does not send it', () => {
    expect(workflowReadOf(current(undefined))).toEqual({ read: 'ok', steps: 2, integration: null });
    expect(workflowReadOf(current(null))).toMatchObject({ integration: 'none' });
    expect(workflowReadOf(current({ gates: 'abc', state: 'accepted' }))).toMatchObject({ integration: 'accepted' });
    expect(workflowReadOf(current({ gates: 'none', state: 'accepted' }))).toMatchObject({ integration: 'none' });
    expect(workflowReadOf(current({ gates: null, state: 'unaccepted' }))).toMatchObject({ integration: 'proposed' });
    expect(workflowReadOf(current({ gates: 'abc', state: 'changed' }))).toMatchObject({ integration: 'proposed' });
    expect(workflowReadOf(current({ gates: null, state: 'unread' }))).toMatchObject({ integration: 'unread' });
  });
});

describe('heldMarks', () => {
  it('marks Work on a held start at this machine or a catalogue unread, and By repository on one at a place', () => {
    expect(heldMarks(STRUCTURE)).toEqual({ work: false, repository: true });
    expect(heldMarks({ ...STRUCTURE, catalogue: { why: 'x' } })).toEqual({ work: true, repository: true });
  });
});

describe('usedByAgent', () => {
  it('lists the roles that lean on an agent at this machine, for its page', () => {
    const claude = TOOLS.find((tool) => tool.name === 'claude-code')!;
    const used = usedByAgent(STRUCTURE, claude);
    expect(used.map((each) => each.role.id)).toEqual(expect.arrayContaining(['build', 'chat', 'ask-daoris']));
    expect(usedByAgent(STRUCTURE, TOOLS.find((tool) => tool.name === 'codex')!).map((each) => each.role.id)).toEqual(['build', 'review']);
  });
});
