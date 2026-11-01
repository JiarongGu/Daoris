import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePlaces } from '../../bridge/shell';
import type { PlaceNeed, PlaceRow, PlacesAsk, PlacesRead, StructurePlaceAsk } from '../../data/wiring';
import { Button, CheckField, Icon } from '../../design/ui';
import { cn } from '../../lib/cn';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { MACHINE, repositoryId, workspaceId } from '../modes';
import type { SetUpPlace } from './placePlan';

/** A page of driven repositories, and the step "Show more" takes. */
const PAGE = 50;

export { MACHINE, repositoryId, workspaceId };

/** The place a row stands for, as the list's chosen item names it. */
export const placeIdOf = (row: PlaceRow): string =>
  row.repository ? repositoryId(row.workspace, row.repository) : row.workspace ? workspaceId(row.workspace) : MACHINE;

export type SetupState = {
  search: string;
  onSearch: (search: string) => void;
  needsLook: boolean;
  onNeedsLook: (on: boolean) => void;
  /** The workspace whose not-driven repositories are open, and the page of them. */
  fold: string | null;
  onFold: (workspace: string | null) => void;
  /** The cursor of the open fold's page; none on its first. */
  foldAfter: string | null;
  onFoldAfter: (after: string | null) => void;
  onMore: () => void;
  read: PlacesRead | null;
  /** `loading` until the first answer; `failed` where the shell does not offer places or could not read them. */
  status: 'loading' | 'failed' | 'ok';
};

/**
 * The Setup mode's places as the view holds them: the search, the filter, the open fold and the page asked, and the driver's answer
 * (AGENTSPLACE1a). The last answer stays while the next is on its way, so a press does not blank the list.
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

const needsOf = (needs: readonly PlaceNeed[]) => needs.map((need) => need.code);

function PlaceButton({ chosen, onPress, depth = 0, children }: { chosen: boolean; onPress: () => void; depth?: 0 | 1; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-current={chosen || undefined}
      onClick={onPress}
      className={cn(
        'block w-full min-w-0 border-l-[3px] py-1.5 pr-2.5 text-left transition-colors duration-(--speed)',
        depth ? 'pl-6' : 'pl-2.5',
        'hover:bg-accent-soft/50',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      {children}
    </button>
  );
}

/**
 * **The Setup mode's list** (AGENTSPLACE1e, the Agents-place design §3): *This machine*, then each workspace with its driven
 * repositories, and the rest of a workspace's imported ones folded into one row that opens a paged read. A search and *Needs a look*
 * narrow the driven ones; the driver judges the reasons from its configuration alone.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
export function SetupList({ state, chosen, onChoose }: { state: SetupState; chosen: string | null; onChoose: (id: string) => void }) {
  const { t } = useTranslation();
  const { read } = state;
  const line = 'm-0 px-3 py-1.5 text-small text-ink-faint';
  const reasons = (needs: readonly PlaceNeed[]) => needsOf(needs).map((code) => t(`agents.setup.need.${code}`, { defaultValue: code })).join(' · ');
  return (
    <div>
      <div className="sticky top-0 z-10 grid gap-1.5 border-b border-line bg-page px-2 pb-2 pt-1.5">
        <div className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={state.search}
            aria-label={t('agents.setup.search')}
            placeholder={t('agents.setup.search')}
            onChange={(event) => state.onSearch(event.target.value)}
            className="min-h-6 min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </div>
        <CheckField checked={state.needsLook} onChange={state.onNeedsLook} label={t('agents.setup.needsLook')} className="px-1 text-small" />
      </div>
      {state.status === 'loading' && <p className={line}>{t('agents.setup.loading')}</p>}
      {state.status === 'failed' && <p className={line}>{t('agents.setup.failed')}</p>}
      {read && (
        <ul className="m-0 list-none p-0" aria-label={t('agents.setup.places')}>
          <li data-list-row="">
            <PlaceButton chosen={chosen === MACHINE} onPress={() => onChoose(MACHINE)}>
              <span className="block truncate text-body font-medium text-ink">{t('agents.setup.machine')}</span>
              {read.machine.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(read.machine.needs)}</span>}
            </PlaceButton>
          </li>
          {read.workspaces.map(({ place, driven, notDriven }) => {
            const workspace = place.workspace ?? '';
            const own = read.repositories.filter((row) => row.workspace === workspace);
            const open = state.fold === workspace;
            return (
              <li key={workspace} role="presentation" className="m-0 p-0">
                <ul className="m-0 list-none p-0" aria-label={workspace}>
                  <li data-list-row="">
                    <PlaceButton chosen={chosen === workspaceId(workspace)} onPress={() => onChoose(workspaceId(workspace))}>
                      <span className="block truncate text-body font-medium text-ink">{workspace}</span>
                      <span className="block truncate text-small text-ink-soft">{t('agents.setup.drivenCount', { count: driven })}</span>
                      {place.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(place.needs)}</span>}
                    </PlaceButton>
                  </li>
                  {own.map((row) => (
                    <li key={row.repository} data-list-row="">
                      <PlaceButton depth={1} chosen={chosen === placeIdOf(row)} onPress={() => onChoose(placeIdOf(row))}>
                        <span className="block truncate text-body text-ink">{row.repository}</span>
                        {row.needs.length > 0 && <span className="block truncate text-small text-warn">{reasons(row.needs)}</span>}
                      </PlaceButton>
                    </li>
                  ))}
                  {notDriven > 0 && (
                    <li data-list-row="">
                      <button
                        type="button"
                        aria-expanded={open}
                        onClick={() => state.onFold(open ? null : workspace)}
                        className="block w-full min-w-0 py-1.5 pl-6 pr-2.5 text-left text-small text-ink-soft hover:bg-accent-soft/50"
                      >
                        {t('agents.setup.imported', { count: notDriven })}
                      </button>
                    </li>
                  )}
                  {open && read.undriven && read.undriven.workspace === workspace && (
                    <>
                      {read.undriven.rows.map((row) => (
                        <li key={row.repository} data-list-row="">
                          <PlaceButton depth={1} chosen={chosen === repositoryId(row.workspace, row.repository)} onPress={() => onChoose(repositoryId(row.workspace, row.repository))}>
                            <span className="block truncate text-body text-ink-soft">{row.repository}</span>
                          </PlaceButton>
                        </li>
                      ))}
                      <li className="flex items-center gap-1.5 py-1 pl-6 pr-2.5">
                        <span className="min-w-0 flex-1 truncate text-small text-ink-faint">
                          {t('agents.setup.page', { shown: read.undriven.rows.length, total: read.undriven.total })}
                        </span>
                        {state.foldAfter && (
                          <Button variant="ghost" onClick={() => state.onFoldAfter(null)}>{t('agents.setup.first')}</Button>
                        )}
                        {read.undriven.next && (
                          <Button variant="ghost" onClick={() => state.onFoldAfter(read.undriven!.next ?? null)}>{t('agents.setup.next')}</Button>
                        )}
                      </li>
                    </>
                  )}
                </ul>
              </li>
            );
          })}
        </ul>
      )}
      {read && read.repositories.length === 0 && (state.search || state.needsLook) && <p className={line}>{t('agents.setup.nothing')}</p>}
      {read?.next && (
        <div className="px-2 py-1.5">
          <Button variant="ghost" onClick={state.onMore}>{t('agents.setup.more')}</Button>
        </div>
      )}
    </div>
  );
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

/** How many of a place's values were set at the place itself. */
const ownChanges = (row: PlaceRow) => {
  const { follows: _follows, ...values } = row.values ?? {};
  return Object.values(values).filter((value) => value.source === 'here').length;
};

function Fact({ name, children }: { name: string; children: ReactNode }) {
  return (
    <div className="flex flex-wrap gap-x-3 gap-y-0.5 py-1">
      <dt className="w-32 shrink-0 text-small text-ink-faint">{name}</dt>
      <dd className="m-0 min-w-0 flex-1 text-body text-ink">{children}</dd>
    </div>
  );
}

/**
 * **A place's page** (AGENTSPLACE1e): its head, driving, held, trees, and *n changes of your own*, then its Roles (AGENTSPLACE1g1) and its
 * Sessions (AGENTSPLACE1f). The preset a place matches is derived by a later row (AGENTSPLACE1a2), and the Workflow section by 1h, so the
 * page says what the configuration already reads.
 */
export function SetupMain({ found, state, roles, after, setUp }: {
  found: Found | null;
  state: SetupState['status'];
  /** The place's Roles section, drawn under its head: the application's, which reads the driver. */
  roles?: ReactNode;
  /** What follows Roles (AGENTSPLACE1f): the place's Sessions, and a repository's Driving and Line and landing until their rows build them. */
  after?: ReactNode;
  /**
   * The place's *Set up…* (AGENTSPLACE1j), pressed from its head: open, the page replaces the place's sections in the main area, and the
   * panel is the application's, which reads the plan and presses Apply. None for *This machine*, which has no set-up.
   */
  setUp?: { open: boolean; onOpen: () => void; panel: ReactNode };
}) {
  const { t } = useTranslation();
  if (!found) {
    return (
      <ViewMain state={state === 'loading' ? 'loading' : 'none'} none={{ headline: t('agents.setup.none.headline'), body: t('agents.setup.none.body') }}>
        {null}
      </ViewMain>
    );
  }
  const yes = (on: boolean) => t(on ? 'agents.setup.yes' : 'agents.setup.no');
  const name = found.kind === 'machine' ? t('agents.setup.machine') : found.kind === 'workspace' ? found.row.workspace ?? '' : found.kind === 'repository' ? found.row.repository ?? '' : found.repository;
  const where = found.kind === 'repository' ? found.row.workspace : found.kind === 'imported' ? found.workspace : undefined;
  const needs = found.kind === 'imported' ? [] : found.row.needs;
  const changes = found.kind === 'imported' || found.kind === 'machine' ? null : ownChanges(found.row);
  const follows = found.kind === 'repository' || found.kind === 'workspace' ? found.row.values?.follows : null;
  const offersSetUp = setUp !== undefined && found.kind !== 'machine';
  if (offersSetUp && setUp.open) {
    return <ViewMain header={<PageHead title={name} line={where ?? undefined} />}>{setUp.panel}</ViewMain>;
  }
  return (
    <ViewMain
      header={(
        <PageHead
          title={name}
          line={where ?? undefined}
          acts={offersSetUp ? <Button onClick={setUp.onOpen}>{t('agents.setup.set.open')}</Button> : undefined}
        />
      )}
    >
      <dl className="m-0 grid">
        {found.kind === 'imported' && (
          <>
            <Fact name={t('agents.setup.driving')}>{yes(false)}</Fact>
            <Fact name={t('agents.setup.checkout')}>{yes(found.checkout)}</Fact>
          </>
        )}
        {found.kind === 'repository' && (
          <>
            <Fact name={t('agents.setup.driving')}>{yes(found.row.driven)}</Fact>
            <Fact name={t('agents.setup.held')}>{yes(found.row.held)}</Fact>
            <Fact name={t('agents.setup.trees')}>{t(found.row.trees ? 'agents.setup.treesOwn' : 'agents.setup.treesNone')}</Fact>
          </>
        )}
        {found.kind === 'workspace' && (
          <>
            <Fact name={t('agents.setup.driving')}>{t('agents.setup.drivenCount', { count: found.driven })}</Fact>
            <Fact name={t('agents.setup.imported_label')}>{t('agents.setup.imported', { count: found.notDriven })}</Fact>
          </>
        )}
        {found.kind === 'machine' && <Fact name={t('agents.setup.works')}>{t('agents.setup.worksCount', { count: found.row.works.length })}</Fact>}
        {changes !== null && (
          <Fact name={t('agents.setup.changes')}>{changes === 0 ? t('agents.setup.followsDefaults') : t('agents.setup.changesOwn', { count: changes })}</Fact>
        )}
        {follows?.default && (
          <Fact name={t('agents.setup.followsDefault')}>
            {t(`agents.setup.defaultName.${follows.default}`, { defaultValue: follows.default })}
            {' '}
            {follows.differs.length === 0 ? t('agents.setup.differsNone') : t('agents.setup.differsIn', { options: follows.differs.join(', ') })}
          </Fact>
        )}
        {needs.length > 0 && (
          <Fact name={t('agents.setup.needsLook')}>
            <ul className="m-0 list-none p-0 text-warn">
              {needs.map((need, at) => (
                <li key={`${need.code}${at}`}>{t(`agents.setup.need.${need.code}`, { defaultValue: need.code })}{need.detail ? `: ${need.detail}` : ''}</li>
              ))}
            </ul>
          </Fact>
        )}
      </dl>
      {found.kind !== 'imported' && roles}
      {found.kind !== 'imported' && after}
    </ViewMain>
  );
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
