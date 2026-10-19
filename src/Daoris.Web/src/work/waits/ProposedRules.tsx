import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Button, Inline } from '../../design/ui';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { proposalChange } from '../../settings/repository/proposals';
import { type Answered, InlineConfirm, Refused } from '../InlineConfirm';
import { InConversation } from './WaitAnchor';

/** The person's answer to a rule a session proposed: which, allow or decline, and how the answer ended (UXFIX2). */
export type RuleAnswer = (proposal: RuleProposal, accept: boolean, answered: Answered) => void;

/**
 * Where an *Allow…*'s ask is open, the frame's (D168 point 3): the proposal's id where it is open in this place, and how to open
 * or put it down, so it is open in the head or at the foot, never both.
 */
export type RuleAsk = { open: string | null; onOpen: (id: string | null) => void };

/**
 * What the frame hands the head and the foot's strip for a session's rule proposals (PROPOSEHERE1b, D168 point 3): the machine's
 * proposals as the shell read them, of which the session's own open ones are drawn, the answer's one door, its pending, and
 * whether the session takes words now, so an *Allow* tells it to go on.
 */
export type RuleWaits = {
  proposals?: readonly RuleProposal[] | null;
  onAnswer?: RuleAnswer;
  /** An answer is on its way, pressed in either place: every press waits for it. */
  busy?: boolean;
  /** The session takes words now (`useSessionReach`), so an *Allow* tells it; false, the card says it is not told. */
  heard?: boolean;
};

/** The outline a proposal wears once its call's *To the wait* led to it, as the strip outlines a line. */
const SOUGHT = 'outline-2 outline-offset-2 outline-accent/60';

/**
 * **The rules a session proposed, answered where the person talks to it** (PROPOSEHERE1b, D168 point 2; D74): what each would
 * change, in the person's language, the session's reason as it wrote it, and its presses. 🔴 A widening never applies without
 * the person (the permission-scopes design §6), so *Allow…* asks once, as the band's does (PROPOSEHERE1a), saying what it
 * widens; *Decline* changes nothing and is direct (platform UX §4).
 *
 * @remarks
 * **Props only** (components §2): the frame holds the answer, its pending and where the ask is open (`ask`), and hands the head
 * and the strip the same. Without `onAnswer` there is no door, and nothing offers one. A session that takes no words now is
 * said so on the card: allowing still changes the rules, and nothing tells it.
 */
export function ProposedRules({ proposals, busy = false, onAnswer, heard = true, lead, inConversation, marked, ask }: {
  /** The session's own proposals the person still answers (`rulesAsked`). */
  proposals: readonly RuleProposal[];
  busy?: boolean;
  onAnswer?: RuleAnswer;
  heard?: boolean;
  /** The line above the list, where it stands: absent, none. */
  lead?: string;
  /** The way to each proposal's call in the conversation, by its id, where the conversation found it (WAITFOOT1c). */
  inConversation?: Readonly<Record<string, () => void>>;
  /** The proposal a raising block's *To the wait* led to: outlined. */
  marked?: string;
  /** Where its *Allow…* is open, the frame's; absent, each item holds its own. */
  ask?: RuleAsk;
}) {
  return (
    <>
      {lead && <p className="m-0 mb-2 text-small text-ink-soft">{lead}</p>}
      <ul className="m-0 grid list-none gap-3 p-0">
        {proposals.map((proposal) => (
          <ProposedRule
            key={proposal.id} proposal={proposal} busy={busy} onAnswer={onAnswer} heard={heard}
            onConversation={inConversation?.[proposal.id]} marked={marked === proposal.id} ask={ask}
          />
        ))}
      </ul>
    </>
  );
}

/** One proposal: *Allow…* asks under it; *Decline* waits in place, and a refusal is said beside it (UXFIX2b3b). */
function ProposedRule({ proposal, busy, onAnswer, heard, onConversation, marked, ask }: {
  proposal: RuleProposal;
  busy: boolean;
  onAnswer?: RuleAnswer;
  heard: boolean;
  onConversation?: () => void;
  marked: boolean;
  ask?: RuleAsk;
}) {
  const { t } = useTranslation();
  const [own, setOwn] = useState(false);
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  const refusalId = useId();
  const change = proposalChange(proposal);
  const asking = ask ? ask.open === proposal.id : own;
  const open = (now: boolean) => (ask ? ask.onOpen(now ? proposal.id : null) : setOwn(now));
  const waiting = busy || pending;
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  const decline = () => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onAnswer!(proposal, false, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  return (
    <li
      tabIndex={-1}
      data-rule={proposal.id}
      className={cn('grid gap-1 rounded-control border border-line bg-raised px-3 py-2', marked ? SOUGHT : 'outline-none')}
    >
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="min-w-0 break-words text-body font-semibold">{change}</span>
        {onConversation && <span className="ml-auto"><InConversation onPress={onConversation} /></span>}
      </p>
      {/* The session's reason, as it wrote it (content, shown as it is). */}
      {proposal.why && <p className="m-0 whitespace-pre-wrap text-small text-ink-soft">{proposal.why}</p>}
      <p className="m-0 font-mono text-meta text-ink-faint">{t('work.wait.rule.proposed', { ago: ago(proposal.proposed) })}</p>
      {!heard && <p className="m-0 text-small text-ink-soft">{t('work.wait.rule.unheard')}</p>}
      {onAnswer && !asking && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <Button variant="primary" disabled={waiting} aria-describedby={refusal ? refusalId : undefined} onClick={() => open(true)}>
            {t('work.attention.act.acceptRule')}
          </Button>
          <Button disabled={waiting} aria-describedby={refusal ? refusalId : undefined} onClick={decline}>
            {t('work.attention.act.declineRule')}
          </Button>
          {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
      {onAnswer && asking && (
        // Outward, and nothing goes: the primary's hue (LAND4's rule), as the band's ask is.
        <InlineConfirm
          className="mt-1"
          tone="primary"
          label={t('work.attention.act.acceptRule')}
          says={(
            <>
              <Inline text={t('work.attention.ask.acceptRule', { change })} />
              {heard && <> {t('work.wait.rule.tells')}</>}
            </>
          )}
          meanIt={t('work.attention.act.allowRule')}
          busy={busy}
          onConfirm={(answered) => onAnswer(proposal, true, answered)}
          onClose={() => open(false)}
        />
      )}
    </li>
  );
}
