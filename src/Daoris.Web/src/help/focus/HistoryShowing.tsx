import { useTranslation } from 'react-i18next';
import { Icon } from '../../design/ui';
import { type Place } from '../focus';
import { type HelpConversationRow } from '../history';
import { type Showing, strayFocus } from '../historyHome';
import { FOCUS_ICON } from './FocusPicker';
import { type PlaceRepository } from './focusPlaces';
import { placeName, ShowingPicker } from './ShowingPicker';

/** What the history's head needs to show and change what it lists. */
export type HistoryScope = {
  showing: Showing;
  /** The window's workspace; null for a window on every workspace. */
  window: string | null;
  /** The workspace the list holds alone (*This workspace*), else null. */
  workspace: string | null;
  repositories: readonly PlaceRepository[] | undefined;
  recent: readonly Place[];
  onShowing: (showing: Showing) => void;
};

/** The history's head line (`docs/2026-11-11-ask-scope-ux-design.md` §3): *Showing* and its chip. */
export function HistoryShowing({ scope }: { scope: HistoryScope }) {
  const { t } = useTranslation();
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1.5">
      <span className="text-small text-ink-soft">{t('help.history.showingLabel')}</span>
      <ShowingPicker
        showing={scope.showing}
        window={scope.window}
        repositories={scope.repositories}
        recent={scope.recent}
        onChoose={scope.onShowing}
      />
    </div>
  );
}

/** A row's focus where it differs from its home; the usual row says nothing. */
export function RowFocus({ row }: { row: HelpConversationRow }) {
  const { t } = useTranslation();
  const place = strayFocus(row);
  if (place === null) return null;
  return (
    <span className="inline-flex min-w-0 items-center gap-1 text-ink-soft">
      <Icon name={FOCUS_ICON[place.kind]} size={11} className="shrink-0" />
      <span className="min-w-0 wrap-anywhere">{t('help.history.rowFocus', { place: placeName(place, t('help.focus.machine')) })}</span>
    </span>
  );
}
