import { useTranslation } from 'react-i18next';
import { Icon, Segmented, StripMark } from '../ui';
import { KNOWLEDGE_MODES, type KnowledgeMode } from './modes';

/** Each mode's glyph, the one its place wore on the activity bar before the two were one place. */
const GLYPH = { search: 'search', convergence: 'convergence' } as const;

/**
 * **Knowledge's list head** (UX6i, D150 §2.2): a two-way choice, *Search · Convergence*, over the mode's own list. Each
 * mode keeps the list it had as a place, so the choice is the one thing the place adds; it stays in reach above the list's
 * rows, outside their scroll (`ListPane`'s `head`).
 *
 * @remarks
 * **A molecule**: the mode in front arrives, and each choice goes out. Named by the place, so a reader hears *Knowledge*
 * and then which mode is chosen; each option is the name its place had (`nav.search`, `nav.convergence`).
 */
export function KnowledgeModes({ mode, onMode }: { mode: KnowledgeMode; onMode: (mode: KnowledgeMode) => void }) {
  const { t } = useTranslation();
  return (
    <div className="shrink-0 border-b border-line px-2 py-1.5">
      <Segmented
        label={t('nav.knowledge')}
        value={mode}
        options={KNOWLEDGE_MODES.map((each) => ({ value: each, label: t(`nav.${each}`) }))}
        onChange={onMode}
        fill
      />
    </div>
  );
}

/**
 * Knowledge's list closed to its strip (D118 §3a): the two modes, each by its glyph and named, the one in front marked as
 * the activity bar marks its place. The strip stands for the head's choice, so a list closed to give a page room still
 * switches between Search and Convergence.
 */
export function KnowledgeStrip({ mode, onMode }: { mode: KnowledgeMode; onMode: (mode: KnowledgeMode) => void }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
      {KNOWLEDGE_MODES.map((each) => (
        <StripMark
          key={each}
          label={t(`nav.${each}`)}
          initialOf={t(`nav.${each}`)}
          face={<Icon name={GLYPH[each]} size={15} />}
          current={mode === each}
          onPress={() => onMode(each)}
        />
      ))}
    </ul>
  );
}
