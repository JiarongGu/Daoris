import { useEffect, useMemo, useState } from 'react';
import { useEveryAsk, useEveryQuest, useEverySession } from '../../data/queries';
import { useHelpPublished } from '../../bridge/shell';
import { publishedCounts, publishedItems } from '../published/published';
import { PublishedList } from '../published/PublishedList';
import { type StarterDoor } from '../starters';
import { type Session } from '../../data/api';

export function useHelpFollow({ shown, onGo }: {
  shown: Session | null;
  onGo: ((door: StarterDoor) => void) | undefined;
}) {
  // What the conversation published: its own list, by id, each one's state read from the lists the
  // shell pushes, every workspace's, never asked on a clock. Read only where it published something; a row opens its record on Quests,
  // the door a go uses, and adds no act.
  const publishedRead = useHelpPublished(shown?.id ?? null);
  const following = (publishedRead.data?.length ?? 0) > 0;
  const everyQuest = useEveryQuest(following);
  const everyAsk = useEveryAsk(following);
  const everySession = useEverySession(following);
  const [publishedOpen, setPublishedOpen] = useState(false);
  useEffect(() => {
    setPublishedOpen(false);
  }, [shown?.id]);
  const publishedRows = useMemo(
    () => publishedItems(publishedRead.data ?? [], { quests: everyQuest.data, asks: everyAsk.data, sessions: everySession.data }),
    [publishedRead.data, everyQuest.data, everyAsk.data, everySession.data],
  );
  const published = following ? (
    <PublishedList
      items={publishedRows}
      counts={publishedCounts(publishedRows)}
      failed={everyQuest.isError || everyAsk.isError}
      open={publishedOpen}
      onToggle={() => setPublishedOpen((was) => !was)}
      onOpen={(door) => onGo?.(door)}
    />
  ) : undefined;

  return { published };
}
