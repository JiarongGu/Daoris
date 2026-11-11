import { useCallback, useState } from 'react';
import type { PresetWorkflow } from '../../workflow/edit/answers';

// A place's quick set-up as the driver answers it (`PLACE_PLAN` and `PLACE_APPLY`): the shapes
// the molecule draws, the ask the draft makes, and the draft the application holds for each place. The bridge leaves a null out.

/**
 * The deliveries a set-up offers, from the announced presets (`WORKFLOW_LIST`: `setUp`, `order` and `delivery`), in the data's order. The
 * page words each by its id from its catalogue.
 */
export const deliveriesOf = (presets: readonly PresetWorkflow[]): PresetWorkflow[] =>
  presets.filter((each) => each.setUp === true && each.delivery)
    .sort((a, b) => (a.order ?? Number.MAX_SAFE_INTEGER) - (b.order ?? Number.MAX_SAFE_INTEGER));

/** What a repository builds (`SetupBuilds.All`): the one thing the adoption quest's request is shaped by. */
export const BUILDS = ['library', 'service', 'desktop', 'tooling'] as const;

export const NOT_DRIVEN = 'not-driven';

/** The ask that is no delivery (`SetupPresets.Keep`): it writes no landing and no workflow choice, and allows driving, the ticks and adoption. */
export const KEEP = 'keep';

export type PlaceAsk = {
  repositories: string[];
  preset: string;
  opinion: boolean;
  look: boolean;
  adoption: boolean;
  builds?: string;
  drive: boolean;
};

export type PlaceChange = { key: string; part: string; said: string; before: string; after: string };

export type PlaceRefusal = { repository?: string | null; part: string; code: string; sentence: string };

export type PlaceAcceptance = { purpose: 'landed' | 'integration'; standing: string; said: string };

export type PlaceDeclaration = { line: string; commit: string; blob: string; text?: string | null; acceptances: PlaceAcceptance[] };

export type PlaceSide = {
  driven: boolean;
  held: boolean;
  trees: boolean;
  match: { preset?: string | null; own: string[] };
  /** The values the side reads, where the driver gives them: the workflow its work follows (`current` or a saved one). */
  values?: { workflow?: { value?: string | null; source: string } | null } | null;
};

export type PlaceWhyCode = 'kept' | 'integration' | 'plugin' | 'default';

export type PlaceRepositoryPlan = {
  repository: string;
  workspace: string;
  before: PlaceSide;
  after: PlaceSide;
  changes: PlaceChange[];
  /** What the plan lowers of the person's part there, in the driver's words. */
  lowers: string[];
  /** §17.7's reach: a row with `said` is a sentence the driver says of it. */
  reach: { repository: string; cannots: string[]; noGates: boolean; said?: string | null }[];
  /** The queued work enabling driving would release, by title. */
  released: { quest: string; title: string }[];
  declaration?: PlaceDeclaration | null;
  /** Why the line's tip could not be read. */
  unread?: string | null;
  /** `why` is a code the page words (`kept`, `integration`, `plugin`, `default`) with the driver's sentence beside it; a driver before it leaves it out. */
  suggested: { preset: string; adoption: boolean; why?: { code: PlaceWhyCode; said: string } };
};

export type PlacePlanned = {
  asked: PlaceAsk;
  repositories: PlaceRepositoryPlan[];
  /** Each change once, with every repository it is made at. */
  changes: { change: PlaceChange; repositories: string[] }[];
  refusals: PlaceRefusal[];
  /** `daoris place … --plan`. */
  terminal: string;
  applies: boolean;
  digest: string;
};

export type PlaceAccept = { repository: string; purpose: string };

/** An acceptance ticked, bound to the declaration it is ticked at: the line and blob the plan read. The route is sent only the {@link PlaceAccept}. */
export type PlaceTick = PlaceAccept & { line: string; blob: string };

export type PlaceOperation = { id: string; kind: string; repository?: string | null; said: string; outcome: string; note?: string | null; quest?: string | null };

/** What an apply came to: `refused` is a code (`moved` where the plan no longer holds), and a refusal wrote nothing. */
export type PlaceApplied = { done: boolean; refused?: string | null; message: string; digest: string; operations: PlaceOperation[] };

/** The apply's payload: the plan's own ask, its digest and the acceptances chosen. */
export type PlaceApplyAsk = PlaceAsk & { digest: string; accept: PlaceAccept[] };

/** What the person has set on a place's set-up, held by the application so a door out and back finds it as left. */
export type SetUpDraft = {
  /** The repositories the plan covers: a repository's own, or the ones ticked on a workspace's page. */
  repositories: string[];
  /** The delivery; none until the plan's suggestion arrives. */
  preset: string | null;
  opinion: boolean;
  look: boolean;
  /** Whether adoption is asked; none until the plan's suggestion arrives. */
  adoption: boolean | null;
  /** What it builds, one of {@link BUILDS}, or none said. */
  builds: string;
  drive: boolean;
  /** The acceptances ticked, each at the declaration it is read at; none is, until the person ticks it. */
  accept: PlaceTick[];
  /** The ticks the page cleared because the declaration changed since, kept to say so until the next tick. */
  cleared: PlaceTick[];
  /** What the last Apply came to, kept until the plan changes. */
  result: PlaceApplied | null;
  /** Where each repository stands once the last Apply is done (what it started), kept with its result. */
  ended: { state: DrivingState; repositories: string[] }[] | null;
  /** Whether the set-up is on its review rather than its choices. */
  review: boolean;
};

export const freshDraft = (repositories: string[]): SetUpDraft => ({
  repositories, preset: null, opinion: false, look: false, adoption: null, builds: '', drive: false, accept: [], cleared: [], result: null, ended: null, review: false,
});

/**
 * The ask a draft makes. Until the suggestion arrives the plan is read as *Not driven* with nothing else asked, which changes nothing and
 * says what to preselect; *Not driven* takes no second opinion, look or driving, so none is sent with it.
 */
export function askOf(draft: SetUpDraft): PlaceAsk {
  const preset = draft.preset ?? NOT_DRIVEN;
  const delivers = preset !== NOT_DRIVEN;
  const adoption = draft.adoption ?? false;
  return {
    repositories: draft.repositories,
    preset,
    opinion: delivers && preset !== KEEP && draft.opinion,
    look: delivers && preset !== KEEP && draft.look,
    adoption,
    ...(adoption && draft.builds ? { builds: draft.builds } : {}),
    drive: delivers && draft.drive,
  };
}

export type SuggestionWhy = { code: PlaceWhyCode | 'saved'; said: string };

/** What a plan suggests for the draft: the one choice every repository agrees on, or none where they differ, and the ones each choice suits. */
export type Suggestions = {
  /** A delivery's id, {@link KEEP}, or null where the repositories differ: nothing is preselected then. */
  preset: string | null;
  adoption: boolean;
  /** Why the preselected choice is suggested; none where nothing is. */
  why: SuggestionWhy | null;
  mixed: boolean;
  /** For each choice any repository suggests, the repositories it suits. */
  suits: Record<string, string[]>;
  /** The workflow a single repository follows, where it is a saved one. */
  following: string | null;
};

/** The saved workflow a place follows by a choice made for it or its workspace; the machine's default workflow is no choice of the place's. */
const savedWorkflow = (row: PlaceRepositoryPlan): string | null => {
  const workflow = row.before.values?.workflow;
  return workflow?.value && workflow.value !== 'current' && workflow.source !== 'machine' && workflow.source !== 'default' ? workflow.value : null;
};

/**
 * What one repository suggests: {@link KEEP} where its landing is chosen (`why.code` `kept`) or its work follows a saved workflow chosen for it,
 * which a delivery would replace with a copy of Daoris's; else the driver's delivery with its why.
 */
function suggestionOf(row: PlaceRepositoryPlan): { preset: string; why: SuggestionWhy | null } {
  const why = row.suggested.why ?? null;
  if (why?.code === 'kept') return { preset: KEEP, why };
  if (savedWorkflow(row)) return { preset: KEEP, why: { code: 'saved', said: '' } };
  return { preset: row.suggested.preset, why };
}

export function suggestionsOf(plan: PlacePlanned | null): Suggestions | null {
  if (!plan || plan.repositories.length === 0) return null;
  const suits: Record<string, string[]> = {};
  const each = plan.repositories.map((row) => ({ row, ...suggestionOf(row) }));
  for (const one of each) (suits[one.preset] ??= []).push(one.row.repository);
  const presets = Object.keys(suits);
  const mixed = presets.length > 1;
  const first = each[0]!;
  return {
    preset: mixed ? null : first.preset,
    adoption: !mixed && each.every((one) => one.row.suggested.adoption),
    why: mixed ? null : first.why,
    mixed,
    suits,
    following: each.length === 1 ? savedWorkflow(first.row) : null,
  };
}

export type DrivingState = 'driven' | 'starts' | 'stops' | 'adoptionOnly' | 'notDriven';

const DRIVING_ORDER: readonly DrivingState[] = ['driven', 'starts', 'stops', 'adoptionOnly', 'notDriven'];

/** Each repository's driving by the plan's own sides, grouped: where it is driven now and after, and what runs where it is not. */
export function drivingOf(plan: PlacePlanned): { state: DrivingState; repositories: string[] }[] {
  const groups = new Map<DrivingState, string[]>();
  for (const row of plan.repositories) {
    const state: DrivingState = row.before.driven
      ? (row.after.driven ? 'driven' : 'stops')
      : row.after.driven ? 'starts' : plan.asked.adoption ? 'adoptionOnly' : 'notDriven';
    groups.set(state, [...(groups.get(state) ?? []), row.repository]);
  }
  return DRIVING_ORDER.filter((state) => groups.has(state)).map((state) => ({ state, repositories: groups.get(state)! }));
}

/** The parts a plan's refusals are about, in the order the page draws them. */
export const PARTS = ['delivery', 'adoption', 'driving'] as const;
export type PlacePart = (typeof PARTS)[number];

const partOf = (refusal: PlaceRefusal): PlacePart => PARTS.find((part) => part === refusal.part) ?? 'delivery';

/** The refusals about one part. */
export const refusalsOf = (plan: PlacePlanned | null, part: PlacePart): PlaceRefusal[] =>
  (plan?.refusals ?? []).filter((each) => partOf(each) === part);

/**
 * Where a refusal's cause is put right, by its code: the line's landing rows (a second opinion's reviewers, a review environment, the line), or
 * the workflow's own steps, where a tick would add one to the workflow the place keeps.
 */
export const doorOf = (code: string): 'landing' | 'steps' | null =>
  code === 'no-reviewers' || code === 'no-review-environment' || code === 'no-line' ? 'landing' : code === 'keeps-workflow' ? 'steps' : null;

/** The acceptances the plan offers, each repository's declaration standing for a purpose that has not been accepted at its blob, with the line and blob read. */
export const offered = (plan: PlacePlanned | null): PlaceTick[] =>
  (plan?.repositories ?? []).flatMap((row) =>
    (row.declaration?.acceptances ?? [])
      .filter((each) => each.standing === 'changed' || each.standing === 'unaccepted')
      .map((each) => ({ repository: row.repository, purpose: each.purpose, line: row.declaration!.line, blob: row.declaration!.blob })));

export const isChosen = (accept: readonly PlaceAccept[], one: PlaceAccept) =>
  accept.some((each) => each.repository === one.repository && each.purpose === one.purpose);

/**
 * The ticks that still hold against the plan read, and the ones cleared: a tick for an acceptance the plan offers at another line or blob than it
 * the tick's own is cleared, since it agrees to a text other than the one read. A tick for what the plan does not offer stands, inert.
 */
export function reconcileTicks(accept: readonly PlaceTick[], plan: PlacePlanned): { accept: PlaceTick[]; cleared: PlaceTick[] } {
  const now = offered(plan);
  const cleared: PlaceTick[] = [];
  const kept = accept.filter((tick) => {
    const read = now.find((each) => each.repository === tick.repository && each.purpose === tick.purpose);
    if (!read || (read.line === tick.line && read.blob === tick.blob)) return true;
    cleared.push(read);
    return false;
  });
  return { accept: kept, cleared };
}

/** What Apply sends: the ticks still offered by the plan at the line and blob they were ticked at, as the route reads them. */
export const acceptOf = (accept: readonly PlaceTick[], plan: PlacePlanned | null): PlaceAccept[] =>
  offered(plan)
    .filter((read) => accept.some((tick) => tick.repository === read.repository && tick.purpose === read.purpose && tick.line === read.line && tick.blob === read.blob))
    .map(({ repository, purpose }) => ({ repository, purpose }));

/** The place a set-up is for, as its page names it: the id the list chose, and for a workspace the repositories it drives now. */
export type SetUpPlace = { id: string; kind: 'repository' | 'workspace'; name: string; workspace: string; driven: string[] };

/** What the application holds for the places whose set-up is open (the Roles page's way, `useRolesPage`): a draft by place id. */
/** A refusal's door, built by the page the set-up is in: where its cause is put right, which returns to the set-up; none where it has no door. */
export type SetUpDoor = (refusal: PlaceRefusal) => (() => void) | null;

export type SetUpPage = {
  /** The draft of an open place; none where its set-up is closed. */
  draftOf: (place: string) => SetUpDraft | null;
  /** Whether the set-up is in front: a draft left through a door is kept but not shown. */
  shown: (place: string) => boolean;
  /** Opens a place's set-up, on the draft the place held where it holds one. */
  open: (place: string, repositories: string[]) => void;
  /** Leaves the set-up through a door, the draft kept as it is. */
  leave: (place: string) => void;
  /** Returns to a set-up left through a door. */
  resume: (place: string) => void;
  close: (place: string) => void;
  /** A change to the draft; any change but the result's own drops the last result, since the plan it came of is gone. */
  change: (place: string, patch: Partial<SetUpDraft>) => void;
};

export function useSetUpPage(): SetUpPage {
  const [drafts, setDrafts] = useState<Record<string, SetUpDraft>>({});
  const [away, setAway] = useState<ReadonlySet<string>>(new Set());
  const draftOf = useCallback((place: string) => drafts[place] ?? null, [drafts]);
  const shown = useCallback((place: string) => drafts[place] !== undefined && !away.has(place), [drafts, away]);
  const back = useCallback((place: string) => setAway((was) => (was.has(place) ? new Set([...was].filter((each) => each !== place)) : was)), []);
  const open = useCallback((place: string, repositories: string[]) => {
    back(place);
    setDrafts((was) => (was[place] ? was : { ...was, [place]: freshDraft(repositories) }));
  }, [back]);
  const leave = useCallback((place: string) => setAway((was) => new Set([...was, place])), []);
  const close = useCallback((place: string) => {
    back(place);
    setDrafts(({ [place]: _closed, ...rest }) => rest);
  }, [back]);
  const change = useCallback((place: string, patch: Partial<SetUpDraft>) =>
    setDrafts((was) => (was[place] ? { ...was, [place]: { ...was[place], ...('result' in patch ? {} : { result: null, ended: null }), ...patch } } : was)), []);
  return { draftOf, shown, open, leave, resume: back, close, change };
}
