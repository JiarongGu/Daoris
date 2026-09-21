import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { Dot } from '../ui';

/**
 * One thing that is waiting on a person, as Overview's band shows it.
 *
 * @remarks
 * **Two kinds, and they are different questions.** A `parked` session is stopped at a checkpoint
 * only a person can clear (D46). An `unanswerable` quest is addressed to a repository this
 * deployment has no registration for — it will sit forever, and nothing else says so.
 *
 * A third kind belongs here by design §4 — **finished work nobody has looked at** — and is not
 * buildable yet: nothing records that anybody looked. It arrives with the *viewed* mark SURF6
 * introduces, and the band says as much rather than leaving a silent gap.
 */
export type Attention = {
  /** The session or quest id — what the door opens. */
  id: string;
  kind: 'parked' | 'unanswerable';
  /** What it is, derived: a session's identity or a quest's title. */
  title: string;
  repository: string;
  /** When it started waiting — a park's last move, a quest's filing. */
  since: string;
  /** The session's analysis, or the sentence explaining why nobody can take the quest. */
  detail?: string | null;
};

/**
 * A row in *what needs you* — and a **door**, because a band that only counted would send people
 * looking for the thing it just told them about.
 *
 * A molecule: it is handed the item and reports a click.
 */
export function AttentionRow({ item, onOpen }: {
  item: Attention;
  onOpen?: (item: Attention) => void;
}) {
  const { t } = useTranslation();

  return (
    <li>
      <button
        type="button"
        onClick={() => onOpen?.(item)}
        className="block w-full border-l-[3px] border-l-st-open px-3 py-2 text-left transition-colors duration-(--speed) hover:bg-accent-soft/50"
      >
        <span className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
          <span className="min-w-0 truncate text-body font-semibold text-ink">{item.title}</span>
          <span className="shrink-0 font-mono text-meta text-ink-faint">
            {t('work.attention.since', { ago: ago(item.since) })}
          </span>
        </span>
        <span className="mt-0.5 flex flex-wrap items-baseline gap-x-2 text-small">
          <Dot tone="parked" label={t(`work.attention.${item.kind}`)} />
          <span className="text-accent">{item.repository}</span>
        </span>
        {item.detail && (
          <span className="mt-0.5 line-clamp-2 block text-small text-ink-soft">{item.detail}</span>
        )}
      </button>
    </li>
  );
}
