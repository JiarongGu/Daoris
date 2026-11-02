import type { TFunction } from 'i18next';
import { useCallback, useMemo, useState } from 'react';
import type { PlaceRow, PlaceWork } from '../../data/wiring';
import { type Authored, authoredText } from '../../locales/authored';
import type { EditorHold } from '../../workflow/edit/WorkflowEditor';

// A place page's shell (SETUPUX1h, the setup-UX design §2.1): its three sections, the one that opens on arrival and the one a need opens, the
// one-line summary each folds to, and the state the application holds above the page so a door out and back finds it as left.

export const SECTIONS = ['workflow', 'roles', 'sessions'] as const;
/** *All roles* is this machine's alone: every role once, as a list or a diagram, with what Daoris suggests and the changes found. */
export type PlaceSection = (typeof SECTIONS)[number] | 'allRoles';
export type PlaceKind = 'machine' | 'workspace' | 'repository';

export const isPlaceSection = (value: string | null | undefined): value is PlaceSection => value === 'allRoles' || SECTIONS.some((each) => each === value);

/** The sections a kind of place draws, in order. *This machine*'s Sessions are Settings' own, so its page draws Roles and All roles. */
export const sectionsOf = (kind: PlaceKind): readonly PlaceSection[] => (kind === 'machine' ? ['roles', 'allRoles'] : SECTIONS);

/** The section that opens on arrival: Workflow on a repository and a workspace, Roles on this machine. */
export const firstSection = (kind: PlaceKind): PlaceSection => sectionsOf(kind)[0]!;

/** A role's name as one text, as authored words are shown (D222); none where the driver named none. */
export function roleNameOf(name: Authored | null | undefined, language: string): string {
  return authoredText(name, language);
}

/**
 * Where a section opens itself for something that needs the person, and at which row: a held start opens Roles at that work, and a landing
 * that could not be read opens Workflow at Landing. A declaration owed acceptance opens Gates from the answer Workflow reads, not from here:
 * a folded section reads nothing.
 */
export function needOpening(row: PlaceRow): { section: PlaceSection; row: string } | null {
  const held = row.works.find((each) => each.held);
  if (held) return { section: 'roles', row: held.work };
  return row.needs.some((each) => each.code === 'landing-unread') ? { section: 'workflow', row: 'landing' } : null;
}

const capital = (text: string) => (text ? text[0]!.toLocaleUpperCase() + text.slice(1) : text);

/** A delivery preset's name from the page's catalogue by its id (SETUPUX1a); *Not driven* is chrome; an id the catalogue lacks reads as itself. */
const deliveryNameOf = (t: TFunction, preset: string): string =>
  preset === 'not-driven' ? t('agents.setup.set.notDriven') : t(`workflow.shipped.delivery.${preset}.name`, { defaultValue: preset });

const sourceMark = (t: TFunction, source: string, workspace?: string | null): string | null =>
  source === 'workspace'
    ? (workspace ? t('agents.setup.fold.from', { name: workspace }) : t('agents.setup.fold.fromWorkspace'))
    : source === 'machine' ? t('agents.setup.fold.fromMachine')
      : source === 'default' ? t('agents.setup.fold.default')
        : null;

/** A value with where it stands: unmarked where it is set here. */
const marked = (t: TFunction, text: string, source: string, workspace?: string | null) => {
  const mark = sourceMark(t, source, workspace);
  return mark ? t('agents.setup.fold.marked', { value: text, mark }) : text;
};

type Value = { value?: string | null; source: string } | undefined;

const languageName = (code: string, language: string) => {
  try {
    return new Intl.DisplayNames([language], { type: 'language' }).of(code) ?? code;
  } catch {
    return code;
  }
};

/** Where a work's role stands at this place: here, or from the workspace or the machine above it. */
function roleSource(work: PlaceWork, row: PlaceRow, kind: PlaceKind): 'here' | 'workspace' | 'machine' {
  const scope = work.roleScope;
  if (!scope) return 'machine';
  if (scope.kind === 'machine') return kind === 'machine' ? 'here' : 'machine';
  if (scope.kind === 'workspace') return kind === 'workspace' && (scope.id ?? row.workspace) === row.workspace ? 'here' : 'workspace';
  return kind === 'repository' && scope.id === row.repository ? 'here' : 'workspace';
}

/**
 * The one line each section folds to, from the place's `PLACES` row alone: every value says where it stands (*from aurora*, *from this
 * machine*, *default*), and a value set here says nothing. A place whose values were not read says so.
 */
export function foldedLines(row: PlaceRow, kind: PlaceKind, t: TFunction, language: string): Record<PlaceSection, string> {
  const join = t('agents.setup.fold.join');
  const workspace = row.workspace;
  const values = row.values;
  const notRead = t('agents.setup.fold.notRead');
  const say = (segments: (string | null)[]) => segments.filter((each): each is string => each !== null).join(join) || notRead;
  const one = (v: Value, text: (value: string) => string): string | null =>
    v?.value ? marked(t, text(v.value), v.source, workspace) : null;

  let workflow = notRead;
  let sessions = notRead;
  if (values) {
    const lands = values.lands;
    const landing: Value = values.landing;
    const how = lands?.preset ? deliveryNameOf(t, lands.preset) : null;
    workflow = say([
      lands
        ? marked(t, how ? (lands.own.length > 0 ? t('agents.setup.fold.landsOwn', { how, count: lands.own.length }) : how) : t('agents.setup.fold.landsOwnWay'), landing?.source ?? 'default', workspace)
        : null,
      values.workflow.value && values.workflow.value !== 'current' ? marked(t, t('agents.setup.fold.saved'), values.workflow.source, workspace) : null,
      one(values.line, (branch) => t('agents.setup.fold.line', { branch })),
      values.look?.value === 'on' ? marked(t, t('agents.setup.fold.lookOn'), values.look.source, workspace) : null,
      values.opinion.value && values.opinion.value !== 'none' ? marked(t, t('agents.setup.fold.opinion', { who: values.opinion.value }), values.opinion.source, workspace) : null,
    ]);
    sessions = say([
      one(values.access, (mode) => t(`agents.setup.fold.access.${mode}`, { defaultValue: mode })),
      one(values.language, (code) => languageName(code, language)),
      one(values.helpers, (mode) => t(`agents.setup.fold.helpers.${mode}`, { defaultValue: mode })),
      values.answer?.value ? marked(t, t('agents.setup.fold.answer'), values.answer.source, workspace) : null,
    ]);
  }

  return { workflow, roles: rolesLine(row, kind, t, language, say), sessions, allRoles: t('agents.setup.allRoles.fold') };
}

function rolesLine(row: PlaceRow, kind: PlaceKind, t: TFunction, language: string, say: (segments: (string | null)[]) => string): string {
  if (row.works.length === 0) return say([]);
  const label = (work: string) => capital(t(`settings.ai.roles.work.${work}`, { defaultValue: work }));
  const held = row.works.find((each) => each.held);
  const own = row.works.filter((each) => each !== held && each.role && roleSource(each, row, kind) === 'here');
  const shown = own.slice(0, 2);
  const remaining = row.works.length - shown.length - (held ? 1 : 0);
  const machine = t('agents.setup.fold.machineName');
  const parent = kind === 'repository' ? row.workspace || machine : machine;
  const rest = shown.length === 0 && !held
    ? t('agents.setup.fold.allAs', { count: remaining, parent })
    : remaining > 0
      ? (kind === 'machine' ? t('agents.setup.fold.more', { count: remaining }) : t('agents.setup.fold.moreAs', { count: remaining, parent }))
      : null;
  return say([
    held ? t('agents.setup.fold.held', { work: label(held.work) }) : null,
    ...shown.map((each) => t('agents.setup.fold.role', { work: label(each.work), role: roleNameOf(each.roleName, language) || each.role })),
    rest,
  ]);
}

export type HeadFacts = { pill: { tone: 'done' | 'taken' | 'open' | 'neutral'; label: string } | null; facts: string[] };

const landsSaid = (t: TFunction, lands: NonNullable<NonNullable<PlaceRow['values']>['lands']>): string | null => {
  if (!lands.preset) return null;
  const how = deliveryNameOf(t, lands.preset);
  return lands.own.length > 0 ? t('agents.setup.fold.landsOwn', { how, count: lands.own.length }) : how;
};

/**
 * The head's pill and facts, from `PLACES` alone (§2.1): a repository's state and how it lands, a workspace's driven count and how its
 * driven repositories land, and this machine's works and roles. `members` are the repositories in hand.
 */
export function headOf(
  input: { kind: PlaceKind; row: PlaceRow; counts?: { driven: number; notDriven: number }; members?: readonly PlaceRow[] }, t: TFunction,
): HeadFacts {
  const { kind, row } = input;
  if (kind === 'machine') {
    const roles = new Set(row.works.map((each) => each.role).filter((each): each is string => Boolean(each)));
    return { pill: null, facts: [t('agents.setup.page.machineCounts', { works: row.works.length, roles: roles.size })] };
  }
  if (kind === 'workspace') {
    const counts = input.counts;
    const landing = new Map<string, number>();
    for (const member of input.members ?? []) {
      const preset = member.values?.lands?.preset;
      if (preset) landing.set(preset, (landing.get(preset) ?? 0) + 1);
    }
    return {
      pill: null,
      facts: [
        ...(counts ? [t('agents.setup.page.drivenOf', { driven: counts.driven, total: counts.driven + counts.notDriven })] : []),
        ...(landing.size > 0
          ? [[...landing].map(([preset, count]) => t('agents.setup.page.landsCount', { how: deliveryNameOf(t, preset), count })).join(t('agents.setup.fold.join'))]
          : []),
      ],
    };
  }
  const lands = row.values?.lands ? landsSaid(t, row.values.lands) : null;
  const pill: HeadFacts['pill'] = row.driven
    ? row.held ? { tone: 'taken', label: t('agents.setup.page.pill.held') } : { tone: 'done', label: t('agents.setup.page.pill.driven') }
    : row.adoption === 'running' ? { tone: 'open', label: t('agents.setup.page.pill.adoption') }
      : { tone: 'neutral', label: t(row.checkout ? 'agents.setup.page.pill.checkout' : 'agents.setup.page.pill.notDriven') };
  return { pill, facts: [...(lands ? [lands] : []), ...(row.driven && row.trees ? [t('agents.setup.page.trees')] : [])] };
}

/** Where a door leaves the person: a section of a place, and a row of it; and the editor of its Workflow asked on a draft (FLOWCHART2g). */
export type PlaceAt = { section?: PlaceSection; row?: string | null; back?: PlaceBack | null; edit?: PlaceEdit };

/**
 * The editor of a place's Workflow asked on a draft (FLOWCHART2g): the fingerprint of the draft a conversation was told, which the card
 * of its proposal is against, with the saved workflow and version it came from where it came from one.
 */
export type PlaceEdit = { draft: string | null; workflow?: string; name?: string; base?: number };

/** The page a door came from, to return to: its place and section, or the place's *Set up…* (SETUPUX1j), which keeps its draft. */
export type PlaceBack = { place: string; name: string; section: PlaceSection | null; setUp?: boolean };

/**
 * The step editor open on a place's Workflow, kept above the mounts so a fold, *Set up…* or a change of layout that unmounts the section
 * leaves the draft as it was: the route it opened on, the steps it began from and the editor's own history and target.
 */
export type PlaceFlowHold = { route: string; from?: { graph: unknown; id?: string; name?: string; base?: number | null; told?: boolean }; editor: EditorHold | null };

type Folds = Partial<Record<PlaceSection, boolean>>;

/** What a place's Workflow draws: the route whose chart it is (`default`, `kind:<name>` or `landed`), and the step whose cell shows. */
export type PlaceView = { route: string; step: string | null };

const DEFAULT_VIEW: PlaceView = { route: 'default', step: null };

/**
 * What the application holds for each place's page, so a door out and back finds it as left: the sections the person opened or closed, the
 * row a door asked for, the way back, and a request to bring the focus to the head. Never kept across a restart.
 */
export type PlaceShell = {
  /** Whether a section is open: as the person left it, else as the page opens it (`itself`). */
  isOpen: (place: string, section: PlaceSection, itself: boolean) => boolean;
  setOpen: (place: string, section: PlaceSection, open: boolean) => void;
  /** The route and the step a place's Workflow shows, as the person left them. */
  viewOf: (place: string) => PlaceView;
  /** Chooses a route (its step is forgotten, since a route's steps are its own) or a step of the one shown. */
  choose: (place: string, choice: Partial<PlaceView>) => void;
  /** The row a door asked for at the place, until it is brought into view. */
  rowOf: (place: string) => string | null;
  rowBrought: (place: string) => void;
  /** The draft a door asked the place's Workflow editor to open on, until the editor has opened (FLOWCHART2g). */
  editOf: (place: string) => PlaceEdit | null;
  editOpened: (place: string) => void;
  /** The step editor's draft at the place, kept while the section is unmounted; null once the editor is closed (REVIEW2 P5). */
  holdOf: (place: string) => PlaceFlowHold | null;
  hold: (place: string, held: PlaceFlowHold | null) => void;
  /** A door to a place: its section opens, its row is asked for, and the way back is kept once. */
  arrive: (place: string, at: PlaceAt) => void;
  /** The way back, where the chosen place was reached from another. */
  backTo: (place: string) => PlaceBack | null;
  clearBack: () => void;
  /** A count, raised when a choice in the list asks the head to take the focus. */
  focusAsked: number;
  askFocus: () => void;
};

export function usePlaceShell(): PlaceShell {
  const [folds, setFolds] = useState<Record<string, Folds>>({});
  const [views, setViews] = useState<Record<string, PlaceView>>({});
  const [rows, setRows] = useState<Record<string, string>>({});
  const [edits, setEdits] = useState<Record<string, PlaceEdit>>({});
  const [holds, setHolds] = useState<Record<string, PlaceFlowHold>>({});
  const [back, setBack] = useState<{ place: string; back: PlaceBack } | null>(null);
  const [focusAsked, setFocusAsked] = useState(0);

  const isOpen = useCallback((place: string, section: PlaceSection, itself: boolean) => folds[place]?.[section] ?? itself, [folds]);
  const setOpen = useCallback((place: string, section: PlaceSection, open: boolean) =>
    setFolds((was) => ({ ...was, [place]: { ...was[place], [section]: open } })), []);
  const viewOf = useCallback((place: string) => views[place] ?? DEFAULT_VIEW, [views]);
  const choose = useCallback((place: string, choice: Partial<PlaceView>) => setViews((was) => {
    const now = was[place] ?? DEFAULT_VIEW;
    const route = choice.route ?? now.route;
    return { ...was, [place]: { route, step: route === now.route ? choice.step === undefined ? now.step : choice.step : choice.step ?? null } };
  }), []);
  const rowOf = useCallback((place: string) => rows[place] ?? null, [rows]);
  const rowBrought = useCallback((place: string) => setRows(({ [place]: _brought, ...rest }) => rest), []);
  const arrive = useCallback((place: string, at: PlaceAt) => {
    if (at.section) setFolds((was) => ({ ...was, [place]: { ...was[place], [at.section!]: true } }));
    const row = at.row;
    if (row) setRows((was) => ({ ...was, [place]: row }));
    const edit = at.edit;
    if (edit) setEdits((was) => ({ ...was, [place]: edit }));
    setBack(at.back ? { place, back: at.back } : null);
  }, []);
  const editOf = useCallback((place: string) => edits[place] ?? null, [edits]);
  const editOpened = useCallback((place: string) => setEdits(({ [place]: _opened, ...rest }) => rest), []);
  const holdOf = useCallback((place: string) => holds[place] ?? null, [holds]);
  const hold = useCallback((place: string, held: PlaceFlowHold | null) =>
    setHolds(({ [place]: _was, ...rest }) => (held === null ? rest : { ...rest, [place]: held })), []);
  const backTo = useCallback((place: string) => (back?.place === place ? back.back : null), [back]);
  const clearBack = useCallback(() => setBack(null), []);
  const askFocus = useCallback(() => setFocusAsked((was) => was + 1), []);

  return useMemo(
    () => ({ isOpen, setOpen, viewOf, choose, rowOf, rowBrought, editOf, editOpened, holdOf, hold, arrive, backTo, clearBack, focusAsked, askFocus }),
    [isOpen, setOpen, viewOf, choose, rowOf, rowBrought, editOf, editOpened, holdOf, hold, arrive, backTo, clearBack, focusAsked, askFocus],
  );
}
