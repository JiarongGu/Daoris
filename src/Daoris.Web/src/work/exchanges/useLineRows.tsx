import { useMemo } from 'react';
import type { Quest, Session } from '../../data/api';
import { useLineChannel, usePostLineChannel } from '../../bridge/sessions/asks';
import type { useSessionAtWork } from '../../bridge/sessions/atWork';
import { sentence } from '../../locales/format';
import type { ChannelAnswered, ChannelForward } from '../../asks/ChannelSection';
import { type Channel, type ChannelPost, forEntry } from '../channel';
import type { OpinionGate } from '../gates/opinion';
import { relationsOf } from '../head/relations';
import type { useAttendedRecord } from '../head/useAttendedRecord';
import type { HelperAskRow } from '../waits/HelperAsks';
import type { LineRowHome } from './LineRows';
import { type LineRow, lineRowsOf } from './rows';

export type LineRowsRead = { rows: LineRow[]; home: LineRowHome };

/**
 * The organism of a line's rows, oldest first: the requests of the attended session's line, each way (`AtWorkA.exchanges`), the
 * quests its session published and the one that published its own (`relationsOf`), its ask's channel kept to its line of work or,
 * on no ask held here, its own channel, and what each row's presses reach.
 */
export function useLineRows({
  session, quest, quests, sessions, atWork, helperAsks, channelAsk, only, record, docked, opinion, onToDock, onOpenSession, onOpenQuest, nameOf,
}: {
  /** The attended session, or none yet: no rows. */
  session: Session | null;
  quest: Quest | null | undefined;
  quests: readonly Quest[];
  sessions: readonly Session[];
  atWork: ReturnType<typeof useSessionAtWork>['data'];
  /** Its helpers' asks: one its agent asked is drawn as its card at its call, never a second time as a row. */
  helperAsks?: readonly HelperAskRow[];
  channelAsk: string | null;
  /** Its line of work on its ask's channel: the messages that concern it. */
  only: { entry: string; repository: string } | null;
  record: Pick<ReturnType<typeof useAttendedRecord>, 'channel' | 'postChannel' | 'forwardChannel'>;
  docked: readonly string[];
  /** The landing's second opinion, whose reviewer's findings are the verdict its row says. */
  opinion: OpinionGate | null;
  onToDock: (key: string) => void;
  onOpenSession: (session: string) => void;
  onOpenQuest?: (quest: string) => void;
  nameOf: (agent: string) => string;
}): LineRowsRead | undefined {
  // A line on no ask held here has a channel of its own, read for its rows.
  const lineChannel = useLineChannel(channelAsk || !session ? null : session.id);
  const postLine = usePostLineChannel();
  const { channel, postChannel, forwardChannel } = record;
  const askChannel: Channel | null = channelAsk && channel.available ? channel.channel ?? null : null;
  const messages = askChannel && only ? forEntry(askChannel.items, only) : lineChannel?.items ?? null;
  const exchanges = atWork && 'answer' in atWork ? atWork.answer.exchanges : undefined;
  const rows = useMemo(() => {
    if (!session) return [];
    const relations = relationsOf(session, quest, quests, sessions);
    const carded = new Set((helperAsks ?? []).filter((ask) => ask.askedBy === 'agent').map((ask) => `helper:${ask.id}`));
    return lineRowsOf({ exchanges, carded, asked: relations.asked, askedBy: relations.askedBy?.id ?? null, resumedAfter: relations.resumedAfter, messages: messages ?? [] });
  }, [session, quest, quests, sessions, helperAsks, exchanges, messages]);
  if (!session || rows.length === 0) return undefined;

  const told = (answered: ChannelAnswered) => ({
    onSuccess: (posted: { posted: boolean; code?: string | null }) => (posted.posted ? answered.done() : answered.refused(posted.code ?? 'other')),
    onError: (error: unknown) => answered.refused(sentence(error)),
  });
  const onPost = (post: ChannelPost, answered: ChannelAnswered) => (channelAsk
    ? postChannel.mutate({ ask: channelAsk, post }, told(answered))
    : postLine.mutate({ session: session.id, post }, told(answered)));
  // *Send to its session*, the person's own press, on an ask's channel.
  const onForward: ChannelForward | undefined = channelAsk ? (message, repository, answered) => forwardChannel.mutate(
    { ask: channelAsk, message, repository },
    {
      onSuccess: (forwarded) => (forwarded.sent ? answered.done() : answered.refused(forwarded.code ?? 'other')),
      onError: (error) => answered.refused(sentence(error)),
    },
  ) : undefined;
  const findings: Record<string, number> = {};
  for (const exchange of exchanges ?? []) {
    if (exchange.expects === 'opinion' && opinion?.reviewing && exchange.session === opinion.reviewing && typeof opinion.findings === 'number') {
      findings[exchange.id] = opinion.findings;
    }
  }

  return {
    rows,
    home: {
      attended: session.id, docked, onToDock, onOpenSession, onOpenQuest, nameOf, findings,
      channel: messages ? { onPost, onForward } : undefined,
    },
  };
}
