import { type ReactNode, useEffect, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type BesideHome, BesideHomes } from '../beside/BesideRows';
import { ChildFocus, TaskFocus } from '../beside/ChildFocus';
import { type BesideReader, pathTo } from '../beside/besides';
import { SinceYouLeft } from '../catchup/SinceYouLeft';
import { ReadingFind } from '../conversation/ConversationFind';
import type { useSessionReading } from '../conversation/useSessionReading';
import type { LineRowHome } from '../exchanges/LineRows';
import type { Wait } from '../waits/waits';
import { type StoryItem, storyOf } from './story';
import { type StoryActor, StoryStream } from './StoryStream';
import { lineOfBlock, storyWaitsOf } from './storyWaits';

type Reading = ReturnType<typeof useSessionReading>;

/**
 * **The story as the page reads it**: the session's one stream (`StoryStream`) over its reading (`useSessionReading`), with every way
 * through it the conversation offers. A long run has the find bar (its first failure, its last words, a search stepping through
 * its hits); a record that began before the page holds *Earlier* above it; a jump, a hit or a wait's raising event brings its line
 * into view, the pages before loaded first; the read mark opens it under *Since you left*; a subagent's or a task's line opens
 * what it started through `reader` (the host's, under its `BesideReading`), and one the live band opens takes the story's place
 * with a way back.
 */
export function SessionStory({ reading, story, actor, scroller, prelude, after, rowHome, onToWait, reader }: {
  /** What the page says of the session above its story. */
  prelude?: ReactNode;
  /** What stands under the story: the session's Accept cards, the host's. */
  after?: ReactNode;
  /** How a subagent's and a task's lines read what they started; absent, they say what the record holds and open nothing. */
  reader?: BesideReader;
  /** A raising line's *To the wait*: to the wait's line, where it is answered. */
  onToWait?: (wait: Wait) => void;
  /** What its line's rows press. */
  rowHome?: LineRowHome;
  reading: Reading;
  story: readonly StoryItem[];
  actor: StoryActor;
  scroller: RefObject<HTMLElement | null>;
}) {
  const { t } = useTranslation();
  const { loaded, earlier, loadEarlier, reveal, show, caught, now, focus, setFocus, lifetimes, find } = reading;
  useEffect(() => {
    if (!reveal) return;
    const line = lineOfBlock(story, reveal.key);
    if (!line) return;
    const frame = requestAnimationFrame(() => {
      scroller.current?.querySelector(`[data-block="${line}"]`)?.scrollIntoView({ block: 'center' });
    });
    return () => cancelAnimationFrame(frame);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- the reveal's count says when to look again
  }, [reveal, scroller]);

  const named = (kind: string, id: string | undefined) => lifetimes.some((event) => event.kind === kind && event.id === id);
  const path = focus && 'child' in focus && named('child', focus.child[focus.child.length - 1]) ? focus.child : null;
  const task = focus && 'task' in focus && named('task', focus.task) ? focus.task : null;
  const home: BesideHome = {
    reader, lifetimes, nested: false, onShow: show,
    onFocus: (child) => setFocus({ child: pathTo(lifetimes, child) }),
    draw: (shown) => <StoryStream story={storyOf({ turns: shown, steps: [], exchanges: [], rows: [] })} actor={actor} />,
  };
  if (path || task) {
    return (
      <BesideHomes.Provider value={home}>
        {path && <ChildFocus path={path} home={home} onBack={(to) => setFocus(to === 0 ? null : { child: path.slice(0, to) })} />}
        {task && <TaskFocus task={task} home={home} onBack={() => setFocus(null)} />}
      </BesideHomes.Provider>
    );
  }
  const read = caught.read ? Date.parse(caught.read) : Number.NaN;
  return (
    <BesideHomes.Provider value={home}><div className="grid min-w-0 gap-2">
      {prelude}
      {find && (
        <div className="sticky top-0 z-[9] -mx-1 border-b border-line bg-page px-1 pb-1.5">
          <ReadingFind find={find} />
        </div>
      )}
      {earlier && (
        <Button variant="ghost" onClick={() => void loadEarlier()} className="justify-self-center text-small">
          {t('work.conversation.earlier')}
        </Button>
      )}
      <StoryStream
        story={story}
        actor={actor}
        home={rowHome}
        waits={storyWaitsOf(story, { marks: reading.marks, revealed: reveal?.key, onToWait })}
        loading={!loaded}
        readMark={Number.isFinite(read) ? read : null}
        since={caught.digest && caught.read
          ? <SinceYouLeft digest={caught.digest} read={caught.read} now={now} onJump={show} />
          : undefined}
      />
      {after}
    </div></BesideHomes.Provider>
  );
}
