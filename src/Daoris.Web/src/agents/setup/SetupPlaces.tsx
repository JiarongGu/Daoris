import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePlaces } from '../../bridge/shell';
import type { PlaceRow, PlacesAsk, PlacesRead, StructurePlaceAsk } from '../../data/wiring';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { MACHINE, repositoryId, workspaceId } from '../modes';
import type { SetUpPlace } from './placePlan';
import { type PlaceDriving, PlacePageView } from './PlacePage';
import type { SourceDoor } from './PlaceRoles';
import {
  firstSection, foldedLines, headOf, needOpening, type PlaceAt, type PlaceKind, type PlaceSection, type PlaceShell, sectionsOf,
} from './placeShell';
import { placeIdOf, type SetupState } from './setupTreeModel';

/** A page of driven repositories, and the step "Show more" takes. */
const PAGE = 50;

export { MACHINE, repositoryId, workspaceId };

/**
 * The Setup mode's places as the view holds them: the search, the filter, the open fold and the page asked, and the driver's answer.
 * The last answer stays while the next is on its way, so a press does not blank the list.
 */
export function useSetupPlaces(enabled: boolean): SetupState {
  const [search, setSearch] = useState('');
  const [needsLook, setNeedsLook] = useState(false);
  const [fold, setFold] = useState<string | null>(null);
  const [foldAfter, setFoldAfter] = useState<string | null>(null);
  const [limit, setLimit] = useState(PAGE);
  const text = search.trim();
  const ask: PlacesAsk = {
    limit,
    ...(text ? { search: text, foldSearch: text } : {}),
    ...(needsLook ? { needsLook: true } : {}),
    ...(fold ? { fold, ...(foldAfter ? { foldAfter } : {}) } : {}),
  };
  const places = usePlaces(ask, { enabled });
  const last = useRef<PlacesRead | null>(null);
  if (places.data) last.current = places.data;
  const read = places.data ?? last.current;
  const status = read ? 'ok' : places.isError || (places.isSuccess && !places.data) ? 'failed' : 'loading';
  return {
    search,
    onSearch: setSearch,
    needsLook,
    onNeedsLook: setNeedsLook,
    fold,
    onFold: (workspace) => { setFold(workspace); setFoldAfter(null); },
    foldAfter,
    onFoldAfter: setFoldAfter,
    onMore: () => setLimit((now) => now + PAGE),
    read,
    status,
  };
}

type Found =
  | { kind: 'machine'; row: PlaceRow }
  | { kind: 'workspace'; row: PlaceRow; driven: number; notDriven: number }
  | { kind: 'repository'; row: PlaceRow }
  | { kind: 'imported'; workspace: string; repository: string; checkout: boolean };

/** The place a chosen id names, from the answer in hand; none where the answer no longer holds it. */
export function findPlace(read: PlacesRead | null, id: string | null): Found | null {
  if (!read || !id) return null;
  if (id === MACHINE) return { kind: 'machine', row: read.machine };
  for (const each of read.workspaces) {
    if (id === workspaceId(each.place.workspace ?? '')) return { kind: 'workspace', row: each.place, driven: each.driven, notDriven: each.notDriven };
  }
  const driven = read.repositories.find((row) => placeIdOf(row) === id);
  if (driven) return { kind: 'repository', row: driven };
  const imported = read.undriven?.rows.find((row) => repositoryId(row.workspace, row.repository) === id);
  return imported ? { kind: 'imported', ...imported } : null;
}

/** The row an imported repository is read as until its detail arrives: not driven, with what the registry says of its checkout. */
const importedRow = (found: Extract<Found, { kind: 'imported' }>): PlaceRow => ({
  workspace: found.workspace, repository: found.repository, driven: false, held: false, trees: false, checkout: found.checkout, adopted: false,
  works: [], needs: [],
});

/** The `PLACES` row of the place a chosen id names: its own, or for an imported repository its detail once read. */
export const rowOfFound = (found: Found | null, detail: PlaceRow | null): PlaceRow | null =>
  !found ? null : found.kind === 'imported' ? detail ?? importedRow(found) : found.row;

const SECTION_TITLE: Record<PlaceSection, string> = {
  workflow: 'agents.setup.workflow.title', roles: 'agents.setup.roles.title', sessions: 'agents.setup.sessions.title',
  allRoles: 'agents.setup.allRoles.title',
};

/**
 * **A place's page**: its head and three sections folded to the line that names their values, from
 * the place's `PLACES` row alone. The page's state (the sections open, the row a door asked for, the way back) is the application's
 * (`PlaceShell`); the sections' bodies are handed in, and a folded section mounts none, so it reads nothing.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
export function SetupMain({ found, state, id, row, counts, members, shell, bodies, notes, drive, setUp, onGo }: {
  found: Found | null;
  state: SetupState['status'];
  /** The chosen place's id, which the shell's state is held under. */
  id: string | null;
  /** The place's `PLACES` row; for an imported repository, its detail once read. */
  row: PlaceRow | null;
  /** A workspace's driven and not driven repositories, and those of its repositories in hand. */
  counts?: { driven: number; notDriven: number };
  members?: readonly PlaceRow[];
  shell: PlaceShell;
  /** Each section's body, an organism of the application's: Workflow and Sessions press its hooks, Roles reads the driver. */
  bodies: Partial<Record<PlaceSection, ReactNode>>;
  /** What waits on the person between the head and the sections: This machine's suggested choices and found changes. */
  notes?: ReactNode;
  /** A repository's driving for its head: the organism holds the hooks, and renders the page with what it made. */
  drive?: (row: PlaceRow, render: (driving: PlaceDriving) => ReactNode) => ReactNode;
  /**
   * The place's *Set up…*, pressed from its head: open, the page replaces the place's sections in the main area, and the
   * panel is the application's, which reads the plan and presses Apply. None for *This machine*, which has no set-up.
   */
  setUp?: { open: boolean; onOpen: () => void; panel: ReactNode; /** Returns to a place's set-up left through a refusal's door. */ onResume: (place: string) => void };
  /** A door to another place at a section and row; the page names itself as the way back. */
  onGo: (target: string, at: PlaceAt) => void;
}) {
  const { t, i18n } = useTranslation();
  if (!found || !id || !row) {
    return (
      <ViewMain state={state === 'loading' ? 'loading' : 'none'} none={{ headline: t('agents.setup.none.headline'), body: t('agents.setup.none.body') }}>
        {null}
      </ViewMain>
    );
  }
  const kind: PlaceKind = found.kind === 'imported' ? 'repository' : found.kind;
  const name = kind === 'machine' ? t('agents.setup.machine') : kind === 'workspace' ? row.workspace ?? '' : row.repository ?? '';
  const offersSetUp = setUp !== undefined && kind !== 'machine';
  if (offersSetUp && setUp.open) {
    return <ViewMain header={<PageHead title={name} line={kind === 'repository' ? row.workspace ?? undefined : undefined} />}>{setUp.panel}</ViewMain>;
  }

  const first = firstSection(kind);
  const need = needOpening(row);
  const lines = foldedLines(row, kind, t, i18n.language);
  const facts = headOf({ kind, row, counts, members }, t);
  const back = shell.backTo(id);
  const from = (section: PlaceSection) => ({ place: id, name, section });
  const sections = sectionsOf(kind).map((section) => ({
    id: section,
    title: t(SECTION_TITLE[section]),
    summary: lines[section],
    open: shell.isOpen(id, section, section === first || need?.section === section),
    onOpenChange: (open: boolean) => shell.setOpen(id, section, open),
    body: bodies[section] ?? null,
  }));
  const workspace = row.workspace;
  const page = (driving: PlaceDriving | null) => (
    <PlacePageView
      head={{
        title: name,
        pill: facts.pill,
        facts: [...facts.facts, ...row.needs.map((each) => `⚠ ${t(`agents.setup.need.${each.code}`, { defaultValue: each.code })}${each.detail ? `: ${each.detail}` : ''}`)],
        parent: kind === 'repository' && workspace ? { name: workspace, onOpen: () => onGo(workspaceId(workspace), { section: firstSection('workspace'), back: from(first) }) } : undefined,
        setUp: offersSetUp ? { onOpen: setUp.onOpen } : undefined,
      }}
      sections={sections}
      notes={notes}
      driving={driving}
      back={back
        ? {
          place: back.name,
          section: back.setUp ? t('agents.setup.set.open') : back.section ? t(SECTION_TITLE[back.section]) : '',
          onBack: () => {
            shell.clearBack();
            if (back.setUp) setUp?.onResume(back.place);
            onGo(back.place, { section: back.section ?? undefined });
          },
        }
        : null}
      row={shell.rowOf(id) ?? need?.row ?? null}
      onRowBrought={() => shell.rowBrought(id)}
      focusAsked={shell.focusAsked}
    />
  );
  return <ViewMain>{drive && kind === 'repository' ? drive(row, page) : page(null)}</ViewMain>;
}

/** A door from a role's source mark: the place that sets it, at the work's row of Roles, with the way back to this page. */
export function sourceDoor(
  from: { id: string; name: string }, workspace: string | null | undefined, onGo: (target: string, at: PlaceAt) => void,
): SourceDoor {
  return (scope, work) => onGo(scope.kind === 'workspace' ? workspaceId(scope.id ?? workspace ?? '') : MACHINE, {
    section: 'roles', row: work, back: { place: from.id, name: from.name, section: 'roles' },
  });
}

/** The place a *Set up…* is for, from the chosen id and the answer in hand; none for *This machine*, which has no set-up. */
export function setUpPlaceOf(found: Found | null, id: string | null, read: PlacesRead | null): SetUpPlace | null {
  if (!found || !id || found.kind === 'machine') return null;
  if (found.kind === 'workspace') {
    const workspace = found.row.workspace ?? '';
    return { id, kind: 'workspace', name: workspace, workspace, driven: (read?.repositories ?? []).filter((row) => row.workspace === workspace).map((row) => row.repository ?? '') };
  }
  return found.kind === 'repository'
    ? { id, kind: 'repository', name: found.row.repository ?? '', workspace: found.row.workspace ?? '', driven: [] }
    : { id, kind: 'repository', name: found.repository, workspace: found.workspace, driven: [] };
}

/** The place whose roles are asked: this machine's (neither), a workspace's, or a driven repository's with the workspace it sits in. */
export const placeAsk = (found: Exclude<Found, { kind: 'imported' }>): StructurePlaceAsk =>
  found.kind === 'machine' ? {}
    : found.kind === 'workspace' ? { workspace: found.row.workspace ?? '' }
      : { ...(found.row.workspace ? { workspace: found.row.workspace } : {}), repository: found.row.repository ?? '' };
