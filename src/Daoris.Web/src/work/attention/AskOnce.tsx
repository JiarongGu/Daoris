import { useTranslation } from 'react-i18next';
import { Inline, SelectField } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { opinionState } from '../gates/opinion';
import { unsettledWords } from '../gates/OpinionGate';
import { TrustAsk } from '../waits/TrustAsk';
import type { AttentionActId } from './attention';
import type { Attention } from './AttentionRow';

/**
 * The question an act asks once under its row (D41 §4): what the second press will do, then the move and *Never mind*.
 * A folder's trust is the agent's own question (`TrustAsk`); a go-ahead's yes or no takes the person's words; a choice of
 * receiver is the workspace's receivers. Named by the act's own label, so a person and a test find it as the press they made.
 */
export function AskOnce({ act, item, label, busy, words, onWords, choice, onChoice, onCancel, onConfirm, onGrant }: {
  act: AttentionActId;
  item: Attention;
  label: string;
  busy: boolean;
  words: string;
  onWords: (words: string) => void;
  choice: string;
  onChoice: (choice: string) => void;
  onCancel: () => void;
  onConfirm: (answered: Answered) => void;
  /** A folder's trust, in its own question: told how the grant ended, it closes only once it landed (UXFIX2b3b). */
  onGrant: (answered: Answered) => void;
}) {
  const { t } = useTranslation();
  if (act === 'trust' && item.trust) {
    return (
      <div className="mt-2">
        <TrustAsk
          hold={{ ...item.trust, ...(item.quest ? { quest: item.quest } : {}), ...(item.ask ? { ask: item.ask } : {}) }}
          busy={busy}
          onGrant={onGrant}
          onCancel={onCancel}
        />
      </div>
    );
  }
  const answering = act === 'approve' || act === 'refuse';
  // A *not yet* needs words: they are what the set-up step's session acts on (REVIEWENV1g, design §3.3).
  const notYet = act === 'not-yet';
  // *Go on anyway…* says what stays unsettled, and takes words the person may give (XAGENT1g, the second-agent design §8.5).
  const anyway = act === 'opinion-anyway' && item.opinion ? item.opinion.opinion : null;
  // INTEGRATE1o part o3: passing a run's declined request over needs the person's words, which the run's record keeps with the skip.
  const passOver = act === 'pass-over';
  // INTEGRATE1p (C33): a declined request of the run's own, asked again (nothing to say) or gone on without (the person's words).
  const askAgain = act === 'ask-again';
  const goOn = act === 'go-on';
  // SESSWATCH1a: what ending a quiet session does, and that the person's waiting words go to the next part (§5.2).
  const quietAsk = act === 'quiet-carry-on' || act === 'quiet-stop';
  const quietSays = act === 'quiet-carry-on' ? 'work.attention.ask.carryOn'
    : item.quiet?.intake ? 'work.attention.ask.quietStopIntake' : 'work.attention.ask.quietStop';
  const sentence = quietAsk
    ? [t(quietSays), ...(act === 'quiet-carry-on' && item.quiet?.words ? [t('work.attention.quietWords')] : [])].join(' ')
    : anyway
    ? t('opinion.ask.anyway', { unsettled: unsettledWords(t, opinionState(anyway.state), anyway) })
    : passOver
    ? t('workflow.run.node.skipSays', { node: item.declined?.node ?? '' })
    : askAgain
    ? t('workflow.run.node.askAgainSays', { node: item.declined?.node ?? '' })
    : goOn
    ? t('workflow.run.node.goOnSays', { node: item.declined?.node ?? '' })
    : notYet
    ? t(item.session ? 'review.ask.notYet' : 'review.ask.notYetOwn', { quest: item.id })
    : act === 'approve'
      ? t('work.attention.ask.approve', { id: item.ask ?? '' })
      : act === 'refuse'
        // GOAHEADSTALE1: a stale one's refusal sends no session on, since none waits for it.
        ? t(item.stale ? 'work.attention.ask.refuseStale' : 'work.attention.ask.refuse', { id: item.ask ?? '' })
        : act === 'accept-rule'
          ? t('work.attention.ask.acceptRule', { change: item.title })
          : t('work.attention.ask.choose', { workspace: item.where });
  const move = act === 'quiet-carry-on'
    ? t('work.attention.act.carryOn')
    : act === 'quiet-stop'
    ? t('work.act.stopMeanIt')
    : anyway
    ? t('opinion.ask.anywayMeanIt')
    : passOver
    ? t('workflow.run.node.skipMeanIt')
    : askAgain
    ? t('workflow.run.node.askAgainMeanIt')
    : goOn
    ? t('workflow.run.node.goOnMeanIt')
    : notYet
    ? t('review.ask.notYetMeanIt')
    : act === 'approve'
      ? t('asks.goAhead.approve')
      : act === 'refuse'
        ? t('asks.goAhead.refuse')
        : act === 'accept-rule'
          ? t('work.attention.act.allowRule')
          : t('asks.record.publish');
  // The words a go-ahead's answer, a second opinion's gate or a *not yet* takes, in the one field each names.
  const wordsLabel = answering ? t('asks.goAhead.words') : anyway ? t('opinion.ask.words') : notYet ? t('review.ask.notYetWords')
    : passOver ? t('workflow.run.node.skipWords') : goOn ? t('workflow.run.node.goOnWords') : null;
  return (
    // Only a refusal ends or removes something (platform UX §4): every other ask here is outward and loses nothing.
    <InlineConfirm
      className="mt-2"
      tone={act === 'refuse' || quietAsk ? 'danger' : 'primary'}
      label={label}
      says={<Inline text={sentence} />}
      meanIt={move}
      busy={busy}
      ready={!(act === 'choose' && !choice) && !((notYet || passOver || goOn) && !words.trim())}
      onConfirm={onConfirm}
      onClose={onCancel}
    >
      {wordsLabel && (
        <input
          aria-label={wordsLabel}
          placeholder={wordsLabel}
          value={words}
          onChange={(event) => onWords(event.target.value)}
          className="min-h-[1.9rem] min-w-0 flex-1 basis-48 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
      )}
      {act === 'choose' && (
        <SelectField
          value={choice}
          onChange={onChoice}
          ariaLabel={t('work.attention.ask.choosePlaceholder')}
          placeholder={t('work.attention.ask.choosePlaceholder')}
          options={(item.choices ?? []).map((name) => ({ value: name, label: name }))}
        />
      )}
    </InlineConfirm>
  );
}
