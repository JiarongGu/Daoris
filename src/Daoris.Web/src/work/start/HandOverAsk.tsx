import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type AgentAccounts, type AgentChoice, AgentChip } from './AgentChip';
import { InlineConfirm } from '../InlineConfirm';
import type { HandOverTold } from '../sessionActs';

/** The roster a hand-over chooses from, as *New conversation* holds it: each agent's names and accounts, and the set's order. */
export type HandOverRoster = {
  /** The agents present on this machine, the conversation's own among them: it is left out here. */
  harnesses: readonly string[];
  labels?: Readonly<Record<string, string>>;
  names?: Readonly<Record<string, string>>;
  accounts: AgentAccounts;
  /** Each agent's accounts of Daoris's in the set's order, every workspace's (POOL1c, D197). */
  orderOf?: (harness: string) => readonly string[] | null;
};

/**
 * **A hand-over's ask** (MIXAGENT1b-i, D163 §2, the mixed-agents design §5): *Go on with another agent…* opens it under the
 * page header as the stop's ask opens, on the primary's hue since nothing is destroyed. It says what goes on and what carries
 * over, the agent and account chip (the agents other than the conversation's own, and the chosen one's accounts in the set's
 * order), then *Hand over* and *Never mind*. A refusal is said inside it and keeps the choice (UX5 U68). While the conversation
 * works the move is *Stop and hand over…*'s: the turn is stopped after the ask is kept, and the ask says where it goes on.
 *
 * @remarks
 * A molecule: the roster and the press arrive as props. The agent is the person's to name, so the first other agent is chosen
 * as it opens; with none, it says so and offers only *Close*. A kept ask with no new conversation yet is said in place of the
 * move (`kept`), with only *Close* left, since a second press would ask the same twice.
 */
export function HandOverAsk({ from, first, working = false, roster, initial, busy = false, refused = null, onHandOver, onClose }: {
  /** The conversation's own agent, which is not offered. */
  from?: string | null;
  /** The harnesses of other tools, listed first (the offer's candidates); the rest follow in the roster's order. */
  first?: readonly string[];
  /** It works: the move stops its turn. */
  working?: boolean;
  roster: HandOverRoster;
  /** The choice it opens with, where Daoris's offer made one (MIXAGENT1b-ii); an agent it does not offer is left out. */
  initial?: AgentChoice;
  /** A hand-over on its way: the presses wait for it. */
  busy?: boolean;
  /** A refusal it opens saying, as a story draws one; after that, the refusals are its own act's. */
  refused?: string | null;
  /** The second press: the agent, its account and the name the ask says it by, told how it ended. */
  onHandOver: (choice: { agent: string; account: string; name: string }, told: HandOverTold) => void;
  /** Put down, or the hand-over landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  // The offer's candidates (other tools) lead, and the rest stay a choice: only Daoris's own offer is narrowed (MIXAGENT1b-iii).
  const others = roster.harnesses.filter((harness) => harness !== from)
    .sort((a, b) => Number(!first?.includes(a)) - Number(!first?.includes(b)));
  const [choice, setChoice] = useState<AgentChoice>(
    initial && others.includes(initial.adapter) ? initial : { adapter: others[0] ?? '', profile: '' });
  const [kept, setKept] = useState<string | null>(null);
  const names = roster.names ?? {};
  const labels = roster.labels ?? {};
  const nameOf = (id: string) => names[id] ?? labels[id] ?? id;

  const says = kept ?? (others.length === 0
    ? t('work.handOver.nobody')
    : t(working ? 'work.handOver.working' : 'work.handOver.idle'));

  return (
    <InlineConfirm
      // Keyed by whether it was kept: a kept ask is another, with its wait let go and only *Close* left.
      key={kept ? 'kept' : 'ask'}
      className="mt-2.5"
      tone="primary"
      label={t('work.handOver.title')}
      says={says}
      meanIt={kept || others.length === 0 ? undefined : t('work.act.handOverMeanIt')}
      busy={busy}
      refused={refused}
      onConfirm={(answered) => onHandOver(
        { agent: choice.adapter, account: choice.profile, name: nameOf(choice.adapter) },
        { ...answered, kept: setKept },
      )}
      onClose={onClose}
    >
      {!kept && others.length > 0 && (
        <AgentChip
          value={choice}
          onChange={setChoice}
          harnesses={others}
          labels={labels}
          names={names}
          accounts={roster.accounts}
          orderOf={roster.orderOf}
          named
          disabled={busy}
        />
      )}
    </InlineConfirm>
  );
}
