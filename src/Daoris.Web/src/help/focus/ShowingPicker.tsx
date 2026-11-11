import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { CHIP, ChipCombobox, type ComboGroup, type ComboRow } from '../../design/atoms/ChipCombobox';
import { Icon } from '../../design/ui';
import { type Place, placeSaid } from '../focus';
import { type Showing } from '../historyHome';
import { FOCUS_ICON } from './FocusPicker';
import { type FocusRow, focusRows, type PlaceRepository } from './focusPlaces';
import { useFolds } from './useFolds';

/** A place's own name, as a chip or a row says it. */
export const placeName = (place: Place, machine: string): string =>
  place.kind === 'machine' ? machine : place.kind === 'workspace' ? place.workspace : place.repository;

/**
 * The history's *Showing* chip and its picker (`docs/2026-11-11-ask-scope-ux-design.md` §3), on the composers' combobox: *This
 * workspace*, marked as this window's, and *All workspaces* above the places, which are the focus picker's own rows, *Recent* first,
 * each workspace unfolding onto its repositories by the same keys; any place chosen is *About* it. A window on every workspace has
 * no workspace of its own to offer, so it offers the one.
 *
 * @param window The window's workspace; null for a window on every workspace.
 * @param repositories This machine's repositories with their workspaces; undefined while they are read.
 */
export function ShowingPicker({ showing, window, repositories, recent, onChoose, defaultOpen }: {
  showing: Showing;
  window: string | null;
  repositories: readonly PlaceRepository[] | undefined;
  recent: readonly Place[];
  onChoose: (showing: Showing) => void;
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const machine = t('help.focus.machine');
  const drawn = useRef(new Map<string, Place>());
  const { open, setOpen, onKeyOver } = useFolds();
  const text = showing.kind === 'here' ? t('help.history.showingHere', { workspace: window ?? '' })
    : showing.kind === 'all' ? t('help.history.every')
      : t('help.history.showingAbout', { place: placeName(showing.place, machine) });

  const row = (id: string, value: string, label: string, icon: Parameters<typeof Icon>[0]['name'], beside?: string, mark?: string | null): ComboRow => ({
    id, value, choosable: true,
    content: (
      <>
        <Icon name={icon} size={12} className="shrink-0 self-center text-ink-faint" />
        <span className="min-w-0 truncate">{label}</span>
        {beside && <>{' '}<span className="min-w-[6ch] shrink truncate text-meta text-ink-faint">{beside}</span></>}
        {mark && <>{' '}<span className="ml-auto shrink-0 text-meta text-ink-faint">{mark}</span></>}
      </>
    ),
  });
  const placeRow = (id: string, each: FocusRow): ComboRow => {
    const drawnRow = row(id, each.key, placeName(each.place, machine), FOCUS_ICON[each.place.kind], each.beside,
      each.count !== undefined && !each.nested ? t('help.focus.count', { count: each.count }) : null);
    if (each.place.kind === 'workspace' && !each.nested) {
      drawnRow.content = (
        <>
          <span className="flex w-3 shrink-0 self-center text-ink-faint"><Icon name={each.open ? 'chevronDown' : 'chevronRight'} size={11} /></span>
          {drawnRow.content}
        </>
      );
    }
    return each.nested ? { ...drawnRow, className: 'pl-8' } : drawnRow;
  };

  const rowsFor = (typed: string) => (repositories === undefined ? [] : focusRows({ repositories, window, recent, open, typed }))
    .filter((each) => each.place.kind !== 'machine');

  const groups = (typed: string): ComboGroup[] => {
    const lead = typed.trim() ? [] : [
      ...(window === null ? [] : [row('here', 'here', t('help.history.showingHere', { workspace: window }), 'folder', undefined, t('help.focus.thisWindow'))]),
      row('all', 'all', t('help.history.every'), 'monitor'),
    ];
    const places = rowsFor(typed);
    drawn.current = new Map(places.map((each) => [each.key, each.place]));
    const of = (group: string) => places.filter((each) => each.group === group)
      .map((each) => placeRow(`${group}-${each.nested ? 'in-' : ''}${each.key}`, each));
    if (typed.trim()) return [{ key: 'found', rows: places.map((each) => placeRow(`found-${each.key}`, each)) }];
    return [
      { key: 'lead', rows: lead },
      { key: 'first', rows: of('first') },
      { key: 'recent', name: t('work.start.recent'), rows: of('recent') },
      { key: 'other', name: t('help.focus.other'), rows: of('other') },
    ];
  };

  const choose = (value: string) => {
    if (value === 'here' || value === 'all') onChoose({ kind: value });
    else {
      const place = drawn.current.get(value);
      if (place) onChoose({ kind: 'about', place });
    }
  };

  // A showing about a repository opens with its workspace unfolded.
  const onOpen = () => {
    setOpen(showing.kind === 'about' && showing.place.kind === 'repository' ? new Set([showing.place.workspace.toLowerCase()]) : new Set());
    return showing.kind === 'about' ? placeSaid(showing.place) : showing.kind;
  };

  return (
    <ChipCombobox
      chip={(
        <>
          <span className="min-w-0 truncate">{text}</span>
          <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
        </>
      )}
      chipLabel={t('help.history.showingIs', { value: text })}
      chipClassName={CHIP}
      listLabel={t('help.history.showingLabel')}
      find={t('help.focus.find')}
      groups={groups}
      empty={(typed) => t('help.focus.nothing', { typed })}
      onOpen={onOpen}
      onChoose={choose}
      onKey={onKeyOver(rowsFor)}
      panelClassName="max-h-72 w-72 max-w-[calc(100cqw-1.5rem)]"
      defaultOpen={defaultOpen}
    />
  );
}
