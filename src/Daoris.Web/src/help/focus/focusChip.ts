import i18n from '../../locales/i18n';
import { type Place, samePlace } from '../focus';

/** What the focus chip draws: its kind's icon, its words, and its whole label for assistive technology, never a tooltip alone. */
export type FocusChipWords = { icon: 'machine' | 'workspace' | 'repository' | 'gone'; text: string; label: string; pending?: true };

const nameOf = (place: Place) => (place.kind === 'machine' ? i18n.t('help.focus.machine')
  : place.kind === 'workspace' ? place.workspace : place.repository);

const labelOf = (place: Place, window: string | null) => {
  if (place.kind === 'machine') return i18n.t('help.focus.label.machine');
  if (place.kind === 'repository') return i18n.t('help.focus.label.repository', { repository: place.repository, workspace: place.workspace });
  return window !== null && samePlace(place, { kind: 'workspace', workspace: window })
    ? i18n.t('help.focus.label.workspaceHere', { workspace: place.workspace })
    : i18n.t('help.focus.label.workspace', { workspace: place.workspace });
};

/**
 * The chip's words for a conversation's focus (`docs/2026-11-11-ask-scope-ux-design.md` §1): the place, *this window* beside the
 * window's own workspace, a change waiting for the next message (shorter at the narrow width), and a place no longer on this
 * machine with the wider one the next message takes. Words beside the icon, so no state is told by colour alone.
 *
 * @param window The window's workspace; null for a window on every workspace.
 * @param gone Whether the focus is no longer on this machine; `next` is then the wider place the driver widens it to.
 */
export function focusChip({ focus, next, window, narrow, gone }: {
  focus: Place; next: Place | null; window: string | null; narrow: boolean; gone?: boolean;
}): FocusChipWords {
  const here = window !== null && focus.kind === 'workspace' && samePlace(focus, { kind: 'workspace', workspace: window });
  const name = nameOf(focus);
  if (gone) {
    const base = i18n.t('help.focus.gone', { place: name });
    const label = i18n.t('help.focus.label.gone', { repository: name });
    return next
      ? { icon: 'gone', text: `${base} · ${i18n.t('help.focus.next', { place: nameOf(next) })}`, label: i18n.t('help.focus.label.widening', { label, place: nameOf(next) }) }
      : { icon: 'gone', text: base, label };
  }

  const shown = here ? `${name} · ${i18n.t('help.focus.thisWindow')}` : name;
  const label = labelOf(focus, window);
  if (!next) return { icon: focus.kind, text: shown, label };
  const waiting = i18n.t('help.focus.label.next', { label, place: nameOf(next) });
  return narrow
    ? { icon: next.kind, text: i18n.t('help.focus.nextShort', { place: nameOf(next) }), label: waiting, pending: true }
    : { icon: focus.kind, text: `${name} · ${i18n.t('help.focus.next', { place: nameOf(next) })}`, label: waiting, pending: true };
}
