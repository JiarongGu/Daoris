import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../../locales/format';
import { Button } from '../../design/ui';
import { Refused } from '../InlineConfirm';
import type { HandOverOffer } from './handOverOffer';
import type { HandOverTold } from '../sessionActs';

/**
 * **Daoris's offer to hand a conversation over** (MIXAGENT1b-ii, D163 point 3, the mixed-agents design §5): one line under the
 * page header, where nothing else is asked, saying why (the account is near its limit, or cut until when) and offering *Go on
 * with {agent} ({account})* and *Choose…*, which opens the hand-over's ask with that choice made. *Not now* puts it down for
 * this run. Under the `ask` rule (D163) nothing is taken without a press.
 *
 * @remarks
 * A molecule: the offer and its press arrive as props. The line is the page's own, not Overview's band (`useAccountsKnown` reads
 * the roster and the accounts for a list of every session; the frame already holds both for the one it attends). A refusal is said
 * in the line and keeps it; a kept answer, for a conversation that is talking, says where it goes on.
 */
export function HandOverOfferLine({ offer, agentName, accountName, busy = false, onGoOn, onChoose, onDismiss }: {
  offer: HandOverOffer;
  /** What a person calls the agent it names. */
  agentName: string;
  /** What a person calls the account it names; null where the agent's own sign-in. */
  accountName: string | null;
  /** A hand-over on its way: the presses wait for it. */
  busy?: boolean;
  /** *Go on with…*: told how it ended, as the ask's second press is. */
  onGoOn: (told: HandOverTold) => void;
  onChoose: () => void;
  onDismiss: () => void;
}) {
  const { t } = useTranslation();
  const [refusal, setRefusal] = useState<string | null>(null);
  const [kept, setKept] = useState<string | null>(null);
  const why = offer.why === 'limit' && offer.until
    ? t('work.handOver.offer.limit', { time: moment(offer.until) })
    : t(offer.why === 'limit' ? 'work.handOver.offer.limitNoTime' : 'work.handOver.offer.near');

  return (
    <div
      role="group"
      aria-label={t('work.handOver.offer.label')}
      className="mt-2.5 flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5 border-l-[3px] border-st-open pl-2.5 text-small text-ink-soft"
    >
      <p className="m-0 min-w-0 wrap-anywhere">{kept ?? why}</p>
      {!kept && (
        <>
          <Button
            variant="primary"
            disabled={busy}
            onClick={() => {
              setRefusal(null);
              onGoOn({ done: () => {}, refused: setRefusal, kept: setKept });
            }}
          >
            {accountName
              ? t('work.handOver.offer.goOn', { agent: agentName, account: accountName })
              : t('work.handOver.offer.goOnOwn', { agent: agentName })}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={onChoose}>{t('work.handOver.offer.choose')}</Button>
        </>
      )}
      <Button variant="ghost" disabled={busy} onClick={onDismiss}>{t(kept ? 'common.dismiss' : 'work.handOver.offer.notNow')}</Button>
      {refusal && <Refused sentence={refusal} />}
    </div>
  );
}
