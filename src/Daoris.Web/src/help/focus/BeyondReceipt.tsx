import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type BeyondEntry } from './beyond';
import { Button, Icon } from '../../design/ui';
import { routeKey } from '../../person/Confirmations';
import { type Place, placeSaid, samePlace } from '../focus';
import { FOCUS_ICON } from './FocusPicker';

type Say = (key: string, options?: Record<string, unknown>) => string;

/** A place as a receipt's row says it: every workspace for this machine, a repository with its workspace. */
const placeWords = (t: Say, place: Place) => (place.kind === 'machine' ? t('help.beyond.place.every')
  : place.kind === 'workspace' ? t('help.beyond.place.workspace', { workspace: place.workspace })
    : t('help.beyond.place.repository', { repository: place.repository, workspace: place.workspace }));

/** A place's own name, as the fold lists it. */
const nameOf = (t: Say, place: Place) => (place.kind === 'machine' ? t('help.beyond.place.every')
  : place.kind === 'workspace' ? place.workspace : place.repository);

/** A press's act in the window's own words, as Notifications' line says it; the connector's act by its tool. */
const actOf = (t: Say, what: string | null) => {
  if (what === null) return '';
  const dot = what.lastIndexOf('.');
  return dot > 0 ? t(routeKey(what.slice(0, dot), what.slice(dot + 1)), { defaultValue: what.slice(dot + 1) }) : what;
};

/** One entry's sentence, in the design's words. */
const sentenceOf = (t: Say, entry: BeyondEntry) => {
  const place = placeWords(t, entry.place);
  if (entry.act === 'search') {
    return typeof entry.count === 'number' ? t('help.beyond.searchFound', { place, count: entry.count }) : t('help.beyond.search', { place });
  }
  if (entry.act === 'read') return entry.what ? t('help.beyond.read', { place, what: entry.what }) : t('help.beyond.readPlace', { place });
  if (entry.act === 'propose') return t('help.beyond.propose', { place });
  const act = actOf(t, entry.what);
  return entry.outcome
    ? t('help.beyond.did', { act, place, outcome: t(`notifications.helpPressed.outcome.${entry.outcome}`) })
    : t('help.beyond.didNow', { act, place });
};

/**
 * The receipt under an Ask Daoris turn of what it looked at and did beyond its conversation's focus
 * (`docs/2026-11-11-ask-scope-ux-design.md` §4): folded to the places, each once; unfolded to what Daoris's doors saw, each with
 * *Make … the focus*, which chooses that place as any choice does, pending until the next message. A place already the focus, or
 * waiting to be, says so and presses nothing. A turn that stayed within its focus draws none.
 *
 * A molecule: drawn from props; the panel holds the focus.
 */
export function BeyondReceipt({ entries, focus, next, onFocus, defaultOpen = false }: {
  entries: readonly BeyondEntry[];
  /** The conversation's applied focus, and the one waiting for the next message. */
  focus: Place;
  next: Place | null;
  onFocus: (place: Place) => void;
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  if (entries.length === 0) return null;
  const places = [...new Map(entries.map((entry) => [placeSaid(entry.place), entry.place])).values()];
  const fold = t('help.beyond.fold', { places: places.map((place) => nameOf(t, place)).join(t('help.beyond.joiner')) });
  return (
    <section data-beyond-receipt="" aria-label={t('help.beyond.label')} className="grid min-w-0 gap-1 border-l-2 border-line pl-2 text-small">
      <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-entries`} onClick={() => setOpen((was) => !was)}
        className="justify-self-start text-ink-soft">
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={11} className="shrink-0" />
        <span className="min-w-0 wrap-anywhere">{fold}</span>
      </Button>
      {open && (
        <div id={`${id}-entries`} className="grid min-w-0 gap-1 pl-4">
          <p className="m-0 text-meta text-ink-faint">{t('help.beyond.source')}</p>
          <ul className="m-0 grid list-none gap-1 p-0">
            {entries.map((entry, at) => {
              const already = samePlace(entry.place, focus) || (next !== null && samePlace(entry.place, next));
              const name = entry.place.kind === 'machine' ? t('help.focus.machine') : nameOf(t, entry.place);
              return (
                <li key={`${entry.at}-${at}`} data-beyond-act={entry.act} className="flex min-w-0 flex-wrap items-center justify-between gap-x-2 gap-y-0.5">
                  <span className="inline-flex min-w-0 items-baseline gap-1 text-ink">
                    <Icon name={FOCUS_ICON[entry.place.kind]} size={11} className="shrink-0 self-center text-ink-faint" />
                    <span className="min-w-0 wrap-anywhere">{sentenceOf(t, entry)}</span>
                  </span>
                  <Button variant="ghost" disabled={already} onClick={() => onFocus(entry.place)}>
                    {already ? t('help.beyond.already') : t('help.beyond.make', { place: name })}
                  </Button>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </section>
  );
}

/** The entries one turn holds: those noted from its words' moment until the next words'. */
export function entriesOfTurn(entries: readonly BeyondEntry[], from: string, until: string | undefined): BeyondEntry[] {
  const start = Date.parse(from);
  const end = until === undefined ? Number.POSITIVE_INFINITY : Date.parse(until);
  return entries.filter((entry) => {
    const at = Date.parse(entry.at);
    return at >= start && at < end;
  });
}

/** What a conversation draws under each turn: its receipt, or nothing for a turn that stayed within its focus. */
export function beyondBeneath(entries: readonly BeyondEntry[] | undefined, { focus, next, onFocus }: {
  focus: Place; next: Place | null; onFocus: (place: Place) => void;
}): (ask: { at: string }, until: string | undefined) => ReactNode {
  return (ask, until) => {
    const held = entriesOfTurn(entries ?? [], ask.at, until);
    return held.length === 0 ? null : <BeyondReceipt entries={held} focus={focus} next={next} onFocus={onFocus} />;
  };
}
