import { useCallback, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useConvergenceView } from './ConvergenceView';
import { KnowledgeModes, KnowledgeStrip } from './knowledge/KnowledgeModes';
import { type KnowledgeMode, readKnowledgeMode, storeKnowledgeMode } from './knowledge/modes';
import { useSearchView } from './SearchView';
import type { Notify } from './ui';
import type { ListPanes } from './work/listPanes';
import type { ViewLayout } from './work/ViewFrame';

/**
 * Knowledge's mode as the application holds it (UX6i, D150 §2.2): the one it was left in, remembered for the place, and
 * a change of it.
 *
 * @remarks
 * **A switch is a door that names no item** (UX6b, D150 §1 rule 6): the other mode's remembered choice is read again
 * before it is shown, so a finding that went while Search was in front opens nothing chosen, never its gone state. The
 * mode in front, chosen again, reads nothing, as a press on the place you are on reopens nothing.
 */
export function useKnowledgeMode(lists: ListPanes): [KnowledgeMode, (mode: KnowledgeMode) => void] {
  const [mode, setMode] = useState<KnowledgeMode>(readKnowledgeMode);
  const shown = useRef(mode);
  const { reopen } = lists;
  const choose = useCallback((next: KnowledgeMode) => {
    if (next !== shown.current) reopen(next);
    shown.current = next;
    setMode(next);
    storeKnowledgeMode(next);
  }, [reopen]);
  return [mode, choose];
}

/**
 * **The Knowledge place** (UX6i, D150 §2.2): Search and Convergence, two views of one index, as one place whose list's
 * head switches between them. What it hands the frame (D118 §5) is the mode's own list and main area, on Knowledge's pane.
 *
 * @remarks
 * **Each mode keeps today's list, main area, filters and memory** (D118 §3f, FRAME1f): the mode's view hands its list
 * whole, its rows, its labels (the result list, the finding list), its chosen item and how that item stands, and the
 * place puts it on its pane, named *Knowledge*, under the two-way choice (`KnowledgeModes`), with the choice's marks on its
 * strip. The chosen item and the filters stay the mode's list's (`chosenIn`, `daoris.list.search.*` and
 * `daoris.list.convergence.*`), so nothing remembered before the two were one place is lost; the pane's closing and width
 * are the place's (`daoris.list.knowledge.*`).
 *
 * **Both modes are held on every view**, as each was: what was typed in Search lasts while Convergence is in front, and
 * neither asks the service anything until it is in front. An empty search's way to Convergence is the other mode.
 */
export function useKnowledgeView({ active, mode, onMode, lists, notify, semantic, handed, onHanded }: {
  /** The place is in front: only then does the mode in front ask, and say its errors. */
  active: boolean;
  mode: KnowledgeMode;
  /** The person, or a door, chose a mode. */
  onMode: (mode: KnowledgeMode) => void;
  /** The application's list memory: each mode's chosen item and filters are its own list's. */
  lists: ListPanes;
  notify: Notify;
  /** Whether this deployment matches meaning too (D24): each mode words its answers by it. */
  semantic: boolean;
  /** Words another door asked Search for (CTX1), once per `id`. */
  handed?: { text: string; id: number } | null;
  onHanded?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  const searchPane = lists.pane('search');
  const search = useSearchView({
    active: active && mode === 'search',
    chosen: searchPane.chosen,
    onChoose: (item) => lists.choose('search', item),
    filters: searchPane.filters,
    onFilters: (filters) => lists.setFilters('search', filters),
    notify,
    semantic,
    onConverge: () => onMode('convergence'),
    handed,
    onHanded,
  });
  const convergencePane = lists.pane('convergence');
  const convergence = useConvergenceView({
    active: active && mode === 'convergence',
    chosen: convergencePane.chosen,
    onChoose: (item) => lists.choose('convergence', item),
    filters: convergencePane.filters,
    onFilters: (filters) => lists.setFilters('convergence', filters),
    notify,
    semantic,
  });

  const shown = mode === 'search' ? search : convergence;
  const list = shown.list!;
  return {
    list: {
      ...list,
      view: 'knowledge',
      chosenIn: list.view,
      name: t('nav.knowledge'),
      head: <KnowledgeModes mode={mode} onMode={onMode} />,
      strip: <KnowledgeStrip mode={mode} onMode={onMode} />,
    },
    main: shown.main,
  };
}
