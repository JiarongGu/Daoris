import type { PlaceRow, PlacesAsk, PlacesRead, PlaceWork, RoleCatalogue, RoleStructure, StructurePlaceAsk } from '../../data/wiring';
import { STRUCTURE, WORKS } from '../roles/structureFixtures';

const value = (text: string, source: 'here' | 'workspace' | 'machine' | 'default') => ({ value: text, source });

const VALUES = (own: number): NonNullable<PlaceRow['values']> => ({
  follows: own > 0 ? { default: 'merge-after-accept', differs: ['accept `automatic`', 'method `squash`'] } : { default: 'integrated', differs: [] },
  lands: own > 0 ? { preset: 'merge-after-accept', own: ['accept `automatic`', 'method `squash`'] } : { preset: 'integrated', own: [] },
  line: value('release', own > 0 ? 'here' : 'default'),
  landing: value('merge', own > 1 ? 'here' : 'workspace'),
  workflow: value('current', 'machine'),
  landed: value('verify', 'default'),
  commits: value('plain', 'default'),
  opinion: value('none', 'default'),
  handOver: value('ask', 'default'),
  language: value('en', 'machine'),
  access: value('today', 'default'),
  helpers: value('ask', 'workspace'),
  look: value('off', 'default'),
});

/** The works a `ROLE_STRUCTURE` place holds, as `PLACES` answers them: the role's id, names and scope, and the rule's source. */
const worksAt = (workspace?: string, repository?: string): PlaceWork[] =>
  STRUCTURE.places.find((place) => place.workspace === workspace && place.repository === repository)!.works.map((each) => ({
    work: each.work,
    role: each.role?.id ?? null,
    held: each.held ?? null,
    consent: each.policy?.consent ?? null,
    source: (each.policy?.source ?? 'default') as PlaceWork['source'],
    roleName: each.role ? each.role.name : null,
    roleScope: each.role ? { kind: each.role.scope.kind as 'machine' | 'workspace' | 'repository', id: each.role.scope.id } : null,
  }));

const row = (repository: string, patch: Partial<PlaceRow> = {}): PlaceRow => ({
  workspace: 'aurora', repository, driven: true, held: false, trees: true, checkout: true, adopted: true, values: VALUES(2), works: worksAt('aurora', repository), needs: [],
  ...patch,
});

const SITE = row('site', { trees: false, checkout: false, values: VALUES(0), needs: [{ code: 'no-checkout' }] });
const ENGINE = row('engine');

const ARCHIVE = Array.from({ length: 41 }, (_, at) => ({ workspace: 'aurora', repository: `archive-${at + 1}`, checkout: true }));

/** An imported repository whole, as `PLACES` answers `detail` for one that is not driven. */
const importedRow = (repository: string): PlaceRow => ({
  workspace: 'aurora', repository, driven: false, held: false, trees: false, checkout: true, adopted: false, adoption: 'none', values: VALUES(0), works: worksAt('aurora', 'site'),
  needs: [],
});

/** An answer to PLACES as the driver gives it: the search and the filter narrow the driven ones, the fold pages the others two at a time. */
export function PLACES(ask: PlacesAsk): PlacesRead {
  const text = ask.search?.toLowerCase() ?? '';
  const driven = [ENGINE, SITE]
    .filter((each) => !text || each.repository!.includes(text))
    .filter((each) => !ask.needsLook || each.needs.length > 0);
  const after = ask.foldAfter ? ARCHIVE.findIndex((each) => `aurora/${each.repository}` === ask.foldAfter) + 1 : 0;
  const page = ARCHIVE.slice(after, after + 2);
  return {
    machine: { driven: false, held: false, trees: false, checkout: false, adopted: false, works: worksAt(), needs: [] },
    workspaces: [{
      place: { workspace: 'aurora', driven: false, held: false, trees: false, checkout: false, adopted: false, values: VALUES(1), works: worksAt('aurora'), needs: [] },
      driven: 2, notDriven: 41,
    }],
    repositories: driven,
    next: null,
    matching: driven.length,
    undriven: ask.fold ? { workspace: ask.fold, rows: page, next: after + 2 < ARCHIVE.length ? `aurora/${page.at(-1)!.repository}` : null, total: ARCHIVE.length } : null,
    detail: ask.detail?.repository ? importedRow(ask.detail.repository) : null,
    members: ask.members
      ? {
        workspace: ask.members,
        rows: [
          ...[ENGINE, SITE].map((each) => ({ repository: each.repository!, driven: true, checkout: each.checkout, adoption: 'adopted' as const })),
          ...ARCHIVE.map((each) => ({ repository: each.repository, driven: false, checkout: each.checkout, adoption: 'none' as const })),
        ],
      }
      : null,
  };
}

/** A place's works; a repository the structure holds none for (an imported one) takes `site`'s, which takes this machine's roles. */
const at = (workspace?: string, repository?: string) => STRUCTURE.places.find((place) => place.workspace === workspace && place.repository === repository)
  ?? { ...STRUCTURE.places.find((place) => place.workspace === workspace && place.repository === 'site')!, repository };

/** The roles' structure for a chosen place's chain, nearest first, as `ROLE_STRUCTURE` answers it (AGENTSPLACE1a): the place, its workspace, this machine. */
export function CHAIN(ask: StructurePlaceAsk): RoleStructure {
  const chain = [...(ask.repository ? [at(ask.workspace, ask.repository)] : []), ...(ask.workspace ? [at(ask.workspace)] : []), at()];
  return { ...STRUCTURE, places: chain };
}

/** The catalogue those roles are from: this machine's seeds and the engine's own building role. */
export const CATALOGUE: RoleCatalogue = { roles: STRUCTURE.roles, works: WORKS, kept: true, suggested: [] };

/** The agents this machine has, as the editor names them. */
export const AGENTS = STRUCTURE.agents.filter((each) => each.installed).map((each) => ({ value: each.agent, label: each.agent }));
