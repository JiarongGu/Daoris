import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS } from './structureFixtures';
import { buildStructure, heldMarks, NO_FILTERS, placesToAsk, type StructureFilters, usedByAgent } from './structure';

// COMPANYVIEW1d (the company-view design §3.3 to §3.5, §4): the rows of Work, Accounts and By repository, built once over the driver's
// answer, with one id each. A row says the driver's answer; it resolves nothing itself.

const build = (filters: Partial<StructureFilters> = {}, workflows = WORKFLOWS) => buildStructure({
  structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
  driver: DRIVER, workflows, filters: { ...NO_FILTERS, ...filters },
});

describe('buildStructure', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('asks this machine, each workspace and each repository once', () => {
    expect(placesToAsk(REGISTRY)).toEqual([
      {}, { workspace: 'aurora' }, { workspace: 'default' },
      { workspace: 'aurora', repository: 'engine' }, { workspace: 'aurora', repository: 'site' }, { workspace: 'default', repository: 'tools' },
    ]);
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
