import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { CHIP, ChipCombobox, type ComboGroup, type ComboRow } from '../../design/atoms/ChipCombobox';
import { Icon, type IconName } from '../../design/ui';
import { type Place, placeSaid, samePlace } from '../focus';
import { focusChip, type FocusChipWords } from './focusChip';
import { type FocusRow, focusRows, type PlaceRepository } from './focusPlaces';
import { useFolds } from './useFolds';

/** Each kind's icon: this machine, a workspace, a repository (the icon set's `projects`), and a place no longer here. */
export const FOCUS_ICON: Record<FocusChipWords['icon'], IconName> = {
  machine: 'monitor', workspace: 'folder', repository: 'projects', gone: 'failure',
};

const nameOf = (place: Place, machine: string) => (place.kind === 'machine' ? machine
  : place.kind === 'workspace' ? place.workspace : place.repository);

/**
 * Ask Daoris's focus chip and its picker (`docs/2026-11-11-ask-scope-ux-design.md` §1): what the conversation is about, first in
 * the composer bar, its words and its label saying the place, a change waiting and a place gone, never by colour alone. The
 * picker is the composers' combobox, opened upward from the panel's foot: the window's place, *Recent*, this machine, the other
 * workspaces unfolding onto their repositories; typing flattens them into one list. A choice is handed out at once
 * (`onChoose`); choosing the applied focus while a change waits keeps it as it is.
 *
 * @param window The window's workspace; null for a window on every workspace.
 * @param repositories This machine's repositories with their workspaces; undefined while they are read, which disables the chip.
 * @param wider Where a gone focus widens to with the next message.
 */
export function FocusPicker({ focus, next, window, repositories, recent, onChoose, narrow = false, gone = false, wider = null, widened = null, reveal, defaultOpen }: {
  focus: Place;
  next: Place | null;
  widened?: Place | null;
  window: string | null;
  repositories: readonly PlaceRepository[] | undefined;
  recent: readonly Place[];
  onChoose: (place: Place) => void;
  narrow?: boolean;
  gone?: boolean;
  wider?: Place | null;
  /** Opens the picker: each new number is one ask (the command table's *Ask Daoris: Change focus…*). */
  reveal?: number;
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const { open, setOpen, onKeyOver } = useFolds();
  // The places the list last drew, by row value, so a choice made from a search is the place it showed.
  const drawn = useRef(new Map<string, Place>());
  const words = focusChip({ focus, next: gone ? wider : next, window, narrow, gone, widened });
  const machine = t('help.focus.machine');

  if (repositories === undefined) {
    return (
      <div className="grid min-w-0 gap-0.5">
        <button type="button" disabled aria-label={words.label} className={cn(CHIP, 'cursor-default opacity-70')}>…</button>
        <p role="status" className="m-0 text-meta text-ink-faint">{t('help.scope.reading')}</p>
      </div>
    );
  }

  const rowsFor = (typed: string) => focusRows({ repositories, window, recent, open, typed });

  const content = (row: FocusRow) => {
    const icon = FOCUS_ICON[row.place.kind];
    const mark = row.mark === 'this-window' ? t('help.focus.thisWindow')
      : row.mark === 'every-workspace' ? t('help.focus.every')
      : row.mark === 'no-checkout' ? t('work.start.noCheckout')
      : row.count !== undefined ? t('help.focus.count', { count: row.count })
      : null;
    return (
      <>
        <span className="flex w-3 shrink-0 self-center">
          {samePlace(row.place, focus) && !row.nested && row.group === 'first' && <Icon name="check" size={12} className="text-accent" />}
        </span>
        <span className="flex w-3 shrink-0 self-center text-ink-faint">
          {row.place.kind === 'workspace' && !row.nested && <Icon name={row.open ? 'chevronDown' : 'chevronRight'} size={11} />}
        </span>
        <Icon name={icon} size={12} className="shrink-0 self-center text-ink-faint" />
        <span className="min-w-0 truncate">{nameOf(row.place, machine)}</span>
        {row.beside && <>{' '}<span className="min-w-[6ch] shrink truncate text-meta text-ink-faint">{row.beside}</span></>}
        {mark && <>{' '}<span className="ml-auto shrink-0 text-meta text-ink-faint">{mark}</span></>}
      </>
    );
  };
  const comboRow = (row: FocusRow): ComboRow => ({
    id: `${row.group}-${row.nested ? 'in-' : ''}${row.key}`,
    value: row.key,
    choosable: true,
    content: content(row),
    className: row.nested ? 'pl-8' : undefined,
  });

  const groups = (typed: string): ComboGroup[] => {
    const rows = rowsFor(typed);
    drawn.current = new Map([[placeSaid(focus), focus], ...rows.map((row): [string, Place] => [row.key, row.place])]);
    if (typed.trim()) return [{ key: 'found', rows: rows.map(comboRow) }];
    const of = (group: FocusRow['group']) => rows.filter((row) => row.group === group).map(comboRow);
    const waiting = next && !gone ? t('help.focus.pending', { next: nameOf(next, machine) }) : null;
    const goneLine = gone && wider ? t('help.focus.goneLine', { place: nameOf(focus, machine), wider: nameOf(wider, machine) }) : null;
    const line = (said: string) => <p className="m-0 px-2 pb-1 pt-1.5 text-small text-ink-soft">{said}</p>;
    return [
      ...(waiting ? [{
        key: 'pending',
        label: waiting,
        head: line(waiting),
        rows: [{
          id: 'keep',
          value: placeSaid(focus),
          choosable: true,
          content: (
            <>
              <span className="w-6 shrink-0" />
              <Icon name={FOCUS_ICON[focus.kind]} size={12} className="shrink-0 self-center text-ink-faint" />
              <span className="min-w-0 truncate">{t('help.focus.keep', { now: nameOf(focus, machine) })}</span>
            </>
          ),
        }],
      }] : []),
      goneLine ? { key: 'first', label: goneLine, head: line(goneLine), rows: of('first') } : { key: 'first', rows: of('first') },
      { key: 'recent', name: t('work.start.recent'), rows: of('recent') },
      { key: 'machine', rows: of('machine') },
      { key: 'other', name: t('help.focus.other'), rows: of('other') },
    ];
  };

  // A focus on a repository opens with its workspace unfolded, the repository active.
  const onOpen = () => {
    setOpen(focus.kind === 'repository' ? new Set([focus.workspace.toLowerCase()]) : new Set());
    return placeSaid(focus);
  };

  const onKey = onKeyOver(rowsFor);

  const choose = (value: string) => onChoose(drawn.current.get(value) ?? focus);

  return (
    <ChipCombobox
      chip={(
        <>
          <Icon name={FOCUS_ICON[words.icon]} size={13} className={cn('shrink-0', words.icon === 'gone' ? 'text-warn' : 'text-ink-faint')} />
          {words.short
            ? <><span className="min-w-0 truncate @max-[24rem]:hidden">{words.text}</span><span aria-hidden className="hidden min-w-0 truncate @max-[24rem]:inline">{words.short}</span></>
            : <span className="min-w-0 truncate">{words.text}</span>}
          <Icon name="chevronUp" size={12} className="shrink-0 text-ink-faint" />
        </>
      )}
      chipLabel={words.label}
      chipClassName={cn(CHIP, words.pending && 'bg-accent-soft text-ink', words.icon === 'gone' && 'border-warn text-warn')}
      listLabel={t('help.focus.list')}
      find={t('help.focus.find')}
      groups={groups}
      empty={(typed) => t('help.focus.nothing', { typed })}
      onOpen={onOpen}
      onChoose={choose}
      onKey={onKey}
      foot={() => <p className="m-0 px-1.5 py-1 text-meta text-ink-faint">{t('help.focus.foot')}</p>}
      reveal={reveal ? { typed: '', active: placeSaid(focus), n: reveal } : null}
      upward
      panelClassName="max-h-80 w-80 max-w-[calc(100cqw-1.5rem)]"
      defaultOpen={defaultOpen}
    />
  );
}
