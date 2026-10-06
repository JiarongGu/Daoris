import { useTranslation } from 'react-i18next';
import { useClearHistory } from '../shell';
import { failure, type Notify } from '../ui';
import { type HistoryClearAnswer, clearSaid, type HistoryTarget, type HistoryUnitName } from './history';

/**
 * **The one owner of a clear** (HIST1e, D153; the history-clearing design §5, §6.1): the quest's page, the ask's page and a
 * workspace's Details each press this, so the clear has one implementation whichever door pressed it, as `workActs.ts` is the
 * one owner of a pause and an abandon.
 *
 * @remarks
 * **An organism**: it holds the bridge's hook, so the pages and the workspace's section, which each press it, hold none.
 * What the press says is `history.ts`'s rule, said here in the catalogue's words: what went, and how many changed since the
 * list and were kept; a file the disk kept apart, as an error. A refusal (one quest, one ask or one quest's failed sessions
 * that changed since the list) is the catalogue's sentence for its code and variant.
 */
export function useHistoryActs({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const clear = useClearHistory();

  return {
    /** The second press: exactly the units its first press listed; `done` once the driver has answered with what went. */
    clear: (target: HistoryTarget, units: readonly HistoryUnitName[], done?: (answer: HistoryClearAnswer) => void) =>
      clear.mutate({ target, units }, {
        onSuccess: (answer) => {
          for (const notice of clearSaid(t, answer)) notify(notice.text, notice.tone);
          done?.(answer);
        },
        onError: failure(notify),
      }),
    /** A clear on its way: the presses wait for it. */
    busy: clear.isPending,
  };
}
