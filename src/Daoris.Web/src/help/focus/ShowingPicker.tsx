import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { CHIP, ChipCombobox, type ComboGroup, type ComboRow } from '../../design/atoms/ChipCombobox';
import { Icon } from '../../design/ui';
import { type Place, placeSaid } from '../focus';
import { type Showing } from '../historyHome';
import { FOCUS_ICON } from './FocusPicker';
import { focusRows, type PlaceRepository } from './focusPlaces';

/** A place's own name, as a chip or a row says it. */
export const placeName = (place: Place, machine: string): string =>
  place.kind === 'machine' ? machine : place.kind === 'workspace' ? place.workspace : place.repository;

/**
 * The history's *Showing* chip and its picker (`docs/2026-11-11-ask-scope-ux-design.md` §3), on the composers' combobox: *This
 * workspace* and *All workspaces* above the places, which are the focus picker's own rows, *Recent* first; any place chosen is
 * *About* it. A window on every workspace has no workspace of its own to offer, so it offers the one.
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
  const text = showing.kind === 'here' ? t('help.history.showingHere', { workspace: window ?? '' })
    : showing.kind === 'all' ? t('help.history.every')
      : t('help.history.showingAbout', { place: placeName(showing.place, machine) });

  const row = (id: string, value: string, label: string, icon: Parameters<typeof Icon>[0]['name'], beside?: string): ComboRow => ({
    id, value, choosable: true,
    content: (
      <>
        <Icon name={icon} size={12} className="shrink-0 self-center text-ink-faint" />
        <span className="min-w-0 truncate">{label}</span>
        {beside && <>{' '}<span className="min-w-[6ch] shrink truncate text-meta text-ink-faint">{beside}</span></>}
      </>
    ),
  });

  const groups = (typed: string): ComboGroup[] => {
    const lead = typed.trim() ? [] : [
      ...(window === null ? [] : [row('here', 'here', t('help.history.showingHere', { workspace: window }), 'folder')]),
      row('all', 'all', t('help.history.every'), 'monitor'),
    ];
    const places = (repositories === undefined ? [] : focusRows({ repositories, window, recent, open: new Set(), typed }))
      .filter((each) => each.place.kind !== 'machine');
    drawn.current = new Map(places.map((each) => [each.key, each.place]));
    const of = (group: string) => places.filter((each) => each.group === group)
      .map((each) => row(`${group}-${each.key}`, each.key, placeName(each.place, machine), FOCUS_ICON[each.place.kind], each.beside));
    if (typed.trim()) return [{ key: 'found', rows: places.map((each) => row(`found-${each.key}`, each.key, placeName(each.place, machine), FOCUS_ICON[each.place.kind], each.beside)) }];
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
      onOpen={() => (showing.kind === 'about' ? placeSaid(showing.place) : showing.kind)}
      onChoose={choose}
      panelClassName="max-h-72 w-72 max-w-[calc(100cqw-1.5rem)]"
      defaultOpen={defaultOpen}
    />
  );
}
