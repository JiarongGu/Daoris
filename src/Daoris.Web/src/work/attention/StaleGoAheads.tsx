import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import type { Attention } from './AttentionRow';

/** How many stale go-aheads a group holds before it offers to refuse them in one press: a lone one is its own row's *Refuse…*. */
export const STALE_AT_ONCE = 2;

/**
 * The stale go-aheads refused in one press, with one reason (GOAHEADSTALE1): a line above the group's rows saying how many nothing on
 * their asks waits for, and *Refuse all …*, which asks once under it, taking the person's reason, before anything is sent.
 *
 * @remarks
 * **The same door as each row's own Refuse.** The press hands back the go-aheads it was drawn over and the person's words; the band
 * refuses each on its own ask through the service's answer, as that row's *Refuse…* would, so each refusal is the person's own and
 * is said back. Nothing new runs: no session waits for any of them, and the band sends none on. There is no *Approve all*: a yes is
 * read one act at a time (D135 §2), and a stale go-ahead is offered none.
 *
 * **A reason is required.** Every later session on these asks is handed the refusal with the person's words, which is what tells it
 * why; the move waits for them.
 *
 * A molecule: handed the rows, whether its act is on its way and the press, it draws them and reports the press.
 */
export function StaleGoAheads({ items, busy = false, onRefuse }: {
  items: readonly Attention[];
  /** The refusals are on their way: the press and the ask wait for them. */
  busy?: boolean;
  /** Every one of `items` refused with `words`, and how to tell the ask how it ended. */
  onRefuse: (items: readonly Attention[], words: string, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const [words, setWords] = useState('');
  const count = items.length;
  const label = t('work.attention.stale.act', { count });
  const close = () => { setAsking(false); setWords(''); };
  return (
    <div className="grid gap-1.5 px-[1.15rem] pb-1">
      <p className="m-0 flex flex-wrap items-center gap-x-3 gap-y-1 text-small text-ink-soft">
        <span>{t('work.attention.stale.lead', { count })}</span>
        <Button disabled={busy || asking} onClick={() => setAsking(true)}>{label}</Button>
      </p>
      {asking && (
        <InlineConfirm
          label={label}
          says={<Inline text={t('work.attention.stale.ask', { count })} />}
          meanIt={t('work.attention.stale.meanIt', { count })}
          busy={busy}
          ready={words.trim().length > 0}
          onConfirm={(answered) => onRefuse(items, words.trim(), answered)}
          onClose={close}
        >
          <input
            aria-label={t('work.attention.stale.reason')}
            placeholder={t('work.attention.stale.reason')}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-h-[1.9rem] min-w-0 flex-1 basis-48 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}
    </div>
  );
}
