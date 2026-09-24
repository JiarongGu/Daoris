import { type RefObject, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useHarnesses, useSessionEvents } from '../shell';
import { Icon } from '../ui';
import { toTurns } from './conversation';
import { ConversationView } from './ConversationView';
import { useFollowTail } from './followTail';

/**
 * The attended session's conversation (D76, CONV2): the organism that holds its record and its
 * scroll, so the view below it holds neither.
 *
 * @remarks
 * **It follows the tail until the person scrolls up**, and then offers the way back — the rule the
 * console has kept since SURF4c, applied to the region the conversation scrolls in, which is the
 * frame's centre (handed in, since the head above scrolls with it).
 *
 * Desktop-only for the console's reason (D47 §4): the record arrives over the bridge.
 */
export function SessionConversation({ session, adapter, chat = false, tree, live, scroller }: {
  session: string;
  /** The harness it runs on — whose declaration says whether its door keeps a conversation (D76 §1). */
  adapter?: string;
  /** Whether it is a conversation with a person. */
  chat?: boolean;
  /** The session's tree, so a tool's path inside it reads relative to it. */
  tree?: string | null;
  live: boolean;
  /** The region the conversation scrolls in. */
  scroller: RefObject<HTMLElement | null>;
}) {
  const { t } = useTranslation();
  const { events, earlier, loaded, loadEarlier } = useSessionEvents(session);
  // The door's own word on what an empty record means — undefined until the roster answers, which reads
  // as the console sentence: it claims least.
  const harnesses = useHarnesses();
  const structured = harnesses.data?.harnesses?.find((row) => row.harness === adapter)?.structured;
  const { turns } = useMemo(() => toTurns(events), [events]);

  // What changes when the conversation grows: the last event, and its text as chunks join it.
  const last = events[events.length - 1];
  const { atTail, toTail } = useFollowTail(scroller, `${last?.seq ?? 0}:${turns.length}`, `${session}:${loaded}`);

  return (
    <>
      <ConversationView
        turns={turns}
        tree={tree}
        structured={structured}
        chat={chat}
        live={live}
        loaded={loaded}
        earlier={earlier}
        onLoadEarlier={() => void loadEarlier()}
      />
      {!atTail && turns.length > 0 && (
        <button
          type="button"
          onClick={() => toTail(true)}
          className="sticky bottom-2 ml-auto flex cursor-pointer items-center gap-1.5 rounded-control border border-line-strong bg-overlay px-2.5 py-1 text-small text-ink-soft shadow-sm hover:text-ink"
        >
          <Icon name="toBottom" size={13} />
          {t('work.conversation.bottom')}
        </button>
      )}
    </>
  );
}
