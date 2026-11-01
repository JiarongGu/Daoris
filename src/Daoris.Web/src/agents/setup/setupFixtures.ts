import type { PlaceRow, PlacesAsk, PlacesRead } from '../../data/wiring';

const value = (text: string, source: 'here' | 'workspace' | 'machine' | 'default') => ({ value: text, source });

const VALUES = (own: number): NonNullable<PlaceRow['values']> => ({
  line: value('release', own > 0 ? 'here' : 'default'),
  landing: value('merge', own > 1 ? 'here' : 'workspace'),
  workflow: value('ship', 'machine'),
  landed: value('verify', 'default'),
  commits: value('plain', 'default'),
  opinion: value('off', 'default'),
  handOver: value('ask', 'default'),
  language: value('en', 'default'),
  access: value('own', 'default'),
});

const row = (repository: string, patch: Partial<PlaceRow> = {}): PlaceRow => ({
  workspace: 'aurora', repository, driven: true, held: false, trees: true, checkout: true, adopted: true, values: VALUES(2), works: [], needs: [],
  ...patch,
});

const SITE = row('site', { trees: false, checkout: false, values: VALUES(0), needs: [{ code: 'no-checkout' }] });
const ENGINE = row('engine');

const ARCHIVE = Array.from({ length: 41 }, (_, at) => ({ workspace: 'aurora', repository: `archive-${at + 1}`, checkout: true }));

/** An answer to PLACES as the driver gives it: the search and the filter narrow the driven ones, the fold pages the others two at a time. */
export function PLACES(ask: PlacesAsk): PlacesRead {
  const text = ask.search?.toLowerCase() ?? '';
  const driven = [ENGINE, SITE]
    .filter((each) => !text || each.repository!.includes(text))
    .filter((each) => !ask.needsLook || each.needs.length > 0);
  const after = ask.foldAfter ? ARCHIVE.findIndex((each) => `aurora/${each.repository}` === ask.foldAfter) + 1 : 0;
  const page = ARCHIVE.slice(after, after + 2);
  return {
    machine: { driven: false, held: false, trees: false, checkout: false, adopted: false, works: [], needs: [] },
    workspaces: [{
      place: { workspace: 'aurora', driven: false, held: false, trees: false, checkout: false, adopted: false, values: VALUES(1), works: [], needs: [] },
      driven: 2, notDriven: 41,
    }],
    repositories: driven,
    next: null,
    matching: driven.length,
    undriven: ask.fold ? { workspace: ask.fold, rows: page, next: after + 2 < ARCHIVE.length ? `aurora/${page.at(-1)!.repository}` : null, total: ARCHIVE.length } : null,
  };
}
