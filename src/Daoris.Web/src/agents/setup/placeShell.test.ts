import { beforeAll, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import type { PlaceRow, PlaceWork } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { firstSection, foldedLines, headOf, isPlaceSection, needOpening, roleNameOf, sectionsOf, usePlaceShell } from './placeShell';

// SETUPUX1h (the setup-UX design §2.1): a place page's three folds say their values from PLACES alone, each with its source, and the first
// section by kind of place, and a section that needs the person, open themselves.

const value = (text: string, source: 'here' | 'workspace' | 'machine' | 'default') => ({ value: text, source });

const work = (name: string, patch: Partial<PlaceWork> = {}): PlaceWork => ({
  work: name, role: `r-${name}`, source: 'default', roleName: { en: `${name} role`, zh: `${name}角色` }, roleScope: { kind: 'machine' }, ...patch,
});

const VALUES = (patch: Partial<NonNullable<PlaceRow['values']>> = {}): NonNullable<PlaceRow['values']> => ({
  line: value('main', 'here'),
  landing: value('merge', 'workspace'),
  workflow: value('current', 'default'),
  landed: value('current', 'default'),
  commits: value('plain', 'default'),
  opinion: value('none', 'default'),
  handOver: value('ask', 'default'),
  language: value('en', 'machine'),
  access: value('today', 'default'),
  lands: { preset: 'merge-after-accept', own: ['accept `automatic`', 'method `squash`'] },
  helpers: value('ask', 'workspace'),
  look: value('off', 'default'),
  ...patch,
});

const ENGINE: PlaceRow = {
  workspace: 'aurora', repository: 'engine', driven: true, held: false, trees: true, checkout: true, adopted: true, values: VALUES(),
  works: [
    work('build', { roleScope: { kind: 'repository', id: 'engine' } }),
    work('review', { roleScope: { kind: 'workspace', id: 'aurora' } }),
    work('design', { roleScope: { kind: 'machine' } }),
    work('chat', { roleScope: { kind: 'machine' } }),
  ],
  needs: [],
};

const t = i18n.getFixedT('en');
const tz = i18n.getFixedT('zh');

beforeAll(async () => { await i18n.changeLanguage('en'); });

describe('the first section by kind of place', () => {
  it('opens Workflow first on a repository and a workspace, and Roles first on this machine', () => {
    expect(firstSection('repository')).toBe('workflow');
    expect(firstSection('workspace')).toBe('workflow');
    expect(firstSection('machine')).toBe('roles');
    expect(sectionsOf('repository')).toEqual(['workflow', 'roles', 'sessions']);
    expect(sectionsOf('machine')[0]).toBe('roles');
  });

  // SETUPUX1m: this machine alone draws All roles beside its Roles, and its fold says what is behind it from no read.
  it("gives this machine All roles after its Roles, and no other place", () => {
    expect(sectionsOf('machine')).toEqual(['roles', 'allRoles']);
    expect(sectionsOf('workspace')).not.toContain('allRoles');
    expect(isPlaceSection('allRoles')).toBe(true);
    expect(foldedLines(ENGINE, 'machine', t, 'en').allRoles).toBe('Every role once, by where it is placed · as a list or a diagram');
    expect(foldedLines(ENGINE, 'machine', tz, 'zh').allRoles).toBe('每个角色一次，按所在位置 · 列表或角色图');
  });
});

describe('a folded value names its source, from PLACES alone', () => {
  it('leaves a value set here unmarked and says where every other stands', () => {
    const lines = foldedLines(ENGINE, 'repository', t, 'en');
    // The landing the workflow follows comes from the workspace's rule; the line is this repository's own.
    expect(lines.workflow).toBe('Merge after you accept, 2 options of its own (from aurora) · Line main');
    // Sessions: the language from this machine, helpers from the workspace, the access the default.
    expect(lines.sessions).toBe('Pauses as today (default) · English (from this machine) · helpers ask you (from aurora)');
  });

  it('says the roles set here, and how many more stand from above', () => {
    const lines = foldedLines(ENGINE, 'repository', t, 'en');
    expect(lines.roles).toBe('Building: build role · 3 more as aurora has');
    expect(foldedLines(ENGINE, 'repository', tz, 'zh').roles).toBe('开发：build角色 · 另有 3 项沿用 aurora');
  });

  it('names the role of a place that sets none of its own as the place above has it', () => {
    const bare: PlaceRow = { ...ENGINE, works: ENGINE.works.map((each) => ({ ...each, roleScope: { kind: 'workspace', id: 'aurora' } })) };
    expect(foldedLines(bare, 'repository', t, 'en').roles).toBe('All 4 works as aurora has');
  });

  it('says a start held first, and reads nothing it was not given', () => {
    const held: PlaceRow = {
      ...ENGINE,
      works: [work('review', { held: 'no role', role: null, roleName: null, roleScope: null }), ...ENGINE.works.filter((each) => each.work !== 'review')],
    };
    expect(foldedLines(held, 'repository', t, 'en').roles).toMatch(/^Second opinions: start held · /);
    const bare: PlaceRow = { driven: false, held: false, trees: false, checkout: true, adopted: false, works: [], needs: [], workspace: 'aurora', repository: 'archive-1' };
    const lines = foldedLines(bare, 'repository', t, 'en');
    expect(lines.workflow).toBe('Not read yet');
    expect(lines.sessions).toBe('Not read yet');
  });

  it('marks a value from this machine, and a workspace values from the machine', () => {
    const workspace: PlaceRow = { ...ENGINE, repository: null, values: VALUES({ line: value('release', 'machine'), landing: value('merge', 'machine') }) };
    expect(foldedLines(workspace, 'workspace', t, 'en').workflow).toContain('Line release (from this machine)');
  });
});

describe('the need that opens a section', () => {
  it('opens Roles at the work whose start is held, and no other section', () => {
    const held: PlaceRow = { ...ENGINE, works: [work('chat', { held: 'no role' }), ...ENGINE.works.filter((each) => each.work !== 'chat')] };
    expect(needOpening(held)).toEqual({ section: 'roles', row: 'chat' });
  });

  it('opens nothing where nothing is held', () => {
    expect(needOpening(ENGINE)).toBeNull();
    expect(needOpening({ ...ENGINE, needs: [{ code: 'no-checkout' }] })).toBeNull();
  });

  // SETUPUX1o: a landing rule that reads as none opens Workflow at its Landing fold.
  it('opens Workflow at Landing on a landing that could not be read, after a held start', () => {
    const unread: PlaceRow = { ...ENGINE, needs: [{ code: 'landing-unread', detail: 'no form' }] };
    expect(needOpening(unread)).toEqual({ section: 'workflow', row: 'landing' });

    const both: PlaceRow = { ...unread, works: [work('chat', { held: 'no role' })] };
    expect(needOpening(both)).toEqual({ section: 'roles', row: 'chat' });
  });
});

// SETUPUX1o: the route the person chose to read, and the step whose cell shows, are held above the mounts for each place.
describe('the route and step a place shows', () => {
  const held = () => renderHook(() => usePlaceShell());

  it('are the default work and no step until chosen, and are kept for each place apart', () => {
    const { result } = held();
    expect(result.current.viewOf('p1')).toEqual({ route: 'default', step: null });

    act(() => result.current.choose('p1', { route: 'kind:docs' }));
    act(() => result.current.choose('p1', { step: 'landing' }));
    expect(result.current.viewOf('p1')).toEqual({ route: 'kind:docs', step: 'landing' });
    expect(result.current.viewOf('p2')).toEqual({ route: 'default', step: null });
  });

  it('forget the step when another route is chosen, whose steps are its own', () => {
    const { result } = held();
    act(() => result.current.choose('p1', { route: 'landed', step: 'review' }));
    act(() => result.current.choose('p1', { route: 'default' }));
    expect(result.current.viewOf('p1')).toEqual({ route: 'default', step: null });
  });
});

// FLOWCHART2g: a draft card's door asks a place's Workflow editor to open on a draft, once, for that place alone.
describe('the editor a door asked a place to open', () => {
  const held = () => renderHook(() => usePlaceShell());
  const edit = { draft: 'g1-0123456789abcdef', workflow: 'release', base: 2 };

  it('is the draft the door named, held until the editor has opened, and for that place alone', () => {
    const { result } = held();
    expect(result.current.editOf('p1')).toBeNull();

    act(() => result.current.arrive('p1', { section: 'workflow', edit }));
    expect(result.current.editOf('p1')).toEqual(edit);
    expect(result.current.editOf('p2')).toBeNull();
    expect(result.current.isOpen('p1', 'workflow', false)).toBe(true);

    act(() => result.current.editOpened('p1'));
    expect(result.current.editOf('p1')).toBeNull();
  });

  it('is not asked by a door that names none', () => {
    const { result } = held();
    act(() => result.current.arrive('p1', { section: 'workflow', row: 'defaults' }));
    expect(result.current.editOf('p1')).toBeNull();
  });
});

// REVIEW2 P5: the step editor's draft outlives the section that drew it, for its place alone, until the editor is closed.
describe('the draft the step editor keeps above the mount', () => {
  const kept = { route: 'default', editor: null };

  it('is held for a place, and let go when the editor closes', () => {
    const { result } = renderHook(() => usePlaceShell());
    expect(result.current.holdOf('p1')).toBeNull();

    act(() => result.current.hold('p1', kept));
    expect(result.current.holdOf('p1')).toEqual(kept);
    expect(result.current.holdOf('p2')).toBeNull();

    act(() => result.current.hold('p1', null));
    expect(result.current.holdOf('p1')).toBeNull();
  });
});

describe('the head: its pill and the facts under the title', () => {
  const NOT_DRIVEN: PlaceRow = { ...ENGINE, repository: 'site', driven: false, trees: false, adopted: false, adoption: 'none', works: [] };

  it('says a repository driven, held, and what it lands by', () => {
    expect(headOf({ kind: 'repository', row: ENGINE }, t)).toEqual({
      pill: { tone: 'done', label: 'Driven' },
      facts: ['Merge after you accept, 2 options of its own', 'its own tree per session'],
    });
    expect(headOf({ kind: 'repository', row: { ...ENGINE, held: true } }, t).pill).toEqual({ tone: 'taken', label: 'Held' });
  });

  it('says a repository not driven by its checkout or by its adoption running', () => {
    expect(headOf({ kind: 'repository', row: NOT_DRIVEN }, t).pill).toEqual({ tone: 'neutral', label: 'Not driven · checkout here' });
    expect(headOf({ kind: 'repository', row: { ...NOT_DRIVEN, adoption: 'running' } }, t).pill).toEqual({ tone: 'open', label: 'Not driven · adoption running' });
    expect(headOf({ kind: 'repository', row: { ...NOT_DRIVEN, checkout: false } }, t).pill).toEqual({ tone: 'neutral', label: 'Not driven' });
  });

  it('counts a workspace\'s driven repositories and how they land, and this machine\'s works and roles', () => {
    const members = [ENGINE, { ...ENGINE, repository: 'api' }, { ...ENGINE, repository: 'site', values: VALUES({ lands: { preset: 'pull-request', own: [] } }) }];
    expect(headOf({ kind: 'workspace', row: { ...ENGINE, repository: null }, counts: { driven: 3, notDriven: 17 }, members }, t)).toEqual({
      pill: null,
      facts: ['3 of 20 driven', 'Merge after you accept 2 · A pull request 1'],
    });
    const machine: PlaceRow = { ...ENGINE, workspace: null, repository: null, works: [work('build'), work('review'), work('chat', { role: 'r-build' })] };
    expect(headOf({ kind: 'machine', row: machine }, t).facts).toEqual(['3 works · 2 roles']);
  });
});

describe('a role name', () => {
  it('reads in the viewer\'s language, then the other, then nothing', () => {
    expect(roleNameOf({ en: 'Reader', zh: '阅读者' }, 'en')).toBe('Reader');
    expect(roleNameOf({ en: 'Reader', zh: '阅读者' }, 'zh')).toBe('阅读者');
    expect(roleNameOf({ zh: '阅读者' }, 'en')).toBe('阅读者');
    expect(roleNameOf('Lecteur', 'zh')).toBe('Lecteur');
    expect(roleNameOf(null, 'en')).toBe('');
  });
});
