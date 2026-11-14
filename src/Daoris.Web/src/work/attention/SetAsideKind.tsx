import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import type { Attention } from './AttentionRow';

/**
 * One kind of row a group holds many of, set aside in one press: a line saying how many rows carry that kind's words, and *Set all
 * aside…*, which asks once under it before anything is sent.
 *
 * @remarks
 * **The same act as each row's own *Set aside*.** The shell sets aside the rows of the kind it holds now, each kept in the history and
 * hidden only while it stands as it did, so one that moves on is shown again. Nothing runs and nothing is answered.
 *
 * A molecule: handed the kind and its count, whether the act is on its way and the press, it draws them and reports the press.
 */
export function SetAsideKind({ kind, count, busy = false, onSetAside }: {
  kind: Attention['kind'];
  count: number;
  /** The act is on its way: the press waits for it. */
  busy?: boolean;
  /** Every row of `kind` set aside, and how to tell the ask how it ended. */
  onSetAside: (kind: Attention['kind'], count: number, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const label = t('work.attention.aside.act', { count });
  return (
    <div className="grid gap-1.5 px-[1.15rem] pb-1">
      <p className="m-0 flex flex-wrap items-center gap-x-3 gap-y-1 text-small text-ink-soft">
        <span>{t('work.attention.aside.lead', { count, kind: t(`work.attention.${kind}`) })}</span>
        <Button disabled={busy || asking} onClick={() => setAsking(true)}>{label}</Button>
      </p>
      {asking && (
        <InlineConfirm
          label={label}
          says={<Inline text={t('work.attention.aside.ask', { count })} />}
          meanIt={t('work.attention.aside.meanIt', { count })}
          busy={busy}
          onConfirm={(answered) => onSetAside(kind, count, answered)}
          onClose={() => setAsking(false)}
        />
      )}
    </div>
  );
}
