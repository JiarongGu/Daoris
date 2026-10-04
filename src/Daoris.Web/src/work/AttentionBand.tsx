import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useAcceptQuest, useAnswerGoAhead, useAsks, usePublishAsk, useQuests, useRegistry, useSessions,
} from '../queries';
import {
  retryNotice, useConsidered, useNudge, useRetryQuest, useRuleProposal, useRules, useSessionGroups, useTrustFolder,
  useUntrusted,
} from '../shell';
import { failure, type Notify } from '../ui';
import type { Attention, AttentionActs } from './AttentionRow';
import { attentionKey, AttentionList, type AttentionDoors, AttentionRegion, NothingNeedsYou } from './AttentionList';
import { needsAPerson } from './attention';

export type { AttentionDoors } from './AttentionList';

/** The sessions working now, which the line says when nothing needs the person (design §6.1). */
const WORKING: ReadonlySet<string> = new Set(['starting', 'working']);

/**
 * Overview's **what needs you**, the page's lead (UX6c, design §6).
 *
 * @remarks
 * **Overview keeps the landing** (D40 — "is anything sitting" is still the first question), and since UX6c this is its
 * first content: what holds work, what waits for the person's word, and what is ready for them, in three groups, the
 * longest waiting first in each. Continuous monitoring is the reported source of fatigue (research §2), so visibility is
 * load-bearing and *watching* is not the mechanism: the band is what a person checks instead of watching.
 *
 * **It settles what one press can** (§6.3): a proposed ask is published to what its declarations named, a parked quest
 * tried again, a departure accepted, each on its record's own route with its sentence said back; a go-ahead's answer, a
 * folder's trust and a widening of the rules ask once under the row. Every other row is a door. **No row starts a process
 * to find out**: every fact here is a list the page already holds or the tick hands it (`needsAPerson`), the session
 * groups included, which the frame reads on every view of a shell and this reads under the same key.
 *
 * **With nothing waiting it is one line**, *Nothing needs you*, and what is working (§6.1).
 *
 * The organism: it holds the queries and the acts, so the list and its rows hold none (components §2).
 */
export function AttentionBand({ doors = {}, notify = () => {}, onSessions }: {
  doors?: AttentionDoors;
  /** Where each act's sentence is said: the service's or the driver's, verbatim (frontend §4a). */
  notify?: Notify;
  /** Sessions itself, where *Ready for you*'s rows past its fifth are: a shell's alone. */
  onSessions?: () => void;
}) {
  const { t } = useTranslation();
  const sessions = useSessions(null, false);
  const quests = useQuests(null, false);
  const registry = useRegistry();
  // The asks still waiting (INT4d) — the Quests view's own list, so one fetch serves both. Each carries its go-aheads.
  const asks = useAsks(false);
  // The folders the driver is holding for the agent's trust (D73) — the shell's tick, so a browser has none.
  const untrusted = useUntrusted();
  // What agents proposed about the rules (PERM2) — the machine's file, so a browser has none.
  const rules = useRules();
  // An older shell never sends proposals, and one that answers RULES with something else sends none.
  const proposals = rules.data?.proposals;
  // The quests the driver's last look parked on their failed sessions (SESSUX1i) — the shell's tick, so a browser has none.
  const considered = useConsidered();
  // Where Sessions' list places each session (D126): the answer the frame already holds on every view, under its key.
  const groups = useSessionGroups();
  // A session to review has ended, so its record is in the list with the ended ones, which the frame holds too; asked only
  // while there is one to name, so a browser, which has no groups, asks nothing more.
  const reviewing = (groups.data ?? []).some((placed) => placed.group === 'review');
  const ended = useSessions(null, true, reviewing);

  const publish = usePublishAsk();
  const answerGoAhead = useAnswerGoAhead();
  const accept = useAcceptQuest();
  const retry = useRetryQuest();
  const trust = useTrustFolder();
  const settle = useRuleProposal();
  const nudge = useNudge();
  // The row an act is on its way for: its acts are held until it answers.
  const [acting, setActing] = useState<string | null>(null);

  const waiting = needsAPerson(
    (reviewing ? ended.data : undefined) ?? sessions.data ?? [], quests.data ?? [], registry.data ?? [], asks.data ?? [],
    untrusted.data ?? [], Array.isArray(proposals) ? proposals : [], considered.data ?? [], groups.data ?? []);

  if (waiting.length === 0) {
    return (
      <AttentionRegion>
        <NothingNeedsYou working={(sessions.data ?? []).filter((session) => WORKING.has(session.state)).length} />
      </AttentionRegion>
    );
  }

  // One owner for each act's press: what it is on its way for, its sentence said, a refusal in its own words, and the row
  // released either way.
  const run = <T,>(item: Attention, act: () => Promise<T>, said: (answer: T) => void) => {
    setActing(attentionKey(item));
    act().then(said, failure(notify)).finally(() => setActing(null));
  };
  const acts: AttentionActs = {
    // Each receiver is its own publish, the ask page's route: said one by one, and stopped at a refusal.
    publish: (item, to) => run(item, async () => {
      for (const receiver of to) {
        const answer = await publish.mutateAsync({ id: item.id, to: receiver });
        notify(answer.message);
      }
    }, () => nudge()),
    answer: (item, approved, words) => run(
      item,
      () => answerGoAhead.mutateAsync({ id: item.ask ?? '', number: item.number ?? 0, approved, words }),
      (answer) => { notify(answer.message); nudge(); },
    ),
    acceptDeparture: (item) => run(item, () => accept.mutateAsync(item.id), (answer) => notify(answer.message)),
    // A driver's verdicts, holds and rules reach only a shell, so in a browser these rows, and these acts, never appear.
    retry: (item) => run(item, () => retry.mutateAsync({ quest: item.id }), (state) => notify(t(...retryNotice(item.id, state)))),
    trust: (item) => {
      const hold = item.trust;
      if (hold) run(item, () => trust.mutateAsync(hold), (granted) => notify(granted.message, granted.verified ? 'ok' : 'error'));
    },
    acceptRule: (item) => run(item, () => settle.mutateAsync({ id: item.id, accept: true }),
      () => notify(t('settings.rules.proposals.accepted', { change: item.title }))),
    declineRule: (item) => run(item, () => settle.mutateAsync({ id: item.id, accept: false }),
      () => notify(t('settings.rules.proposals.declined', { change: item.title }))),
  };

  return (
    <AttentionRegion>
      <AttentionList items={waiting} doors={doors} acts={acts} acting={acting} onSessions={onSessions} />
    </AttentionRegion>
  );
}
