import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EmptyState, SkeletonRows } from '../../design/ui';
import { useRootRem, useWidth } from '../../design/useWidth';
import { useFollowTail } from '../conversation/followTail';
import type { Track } from '../track/passage';
import { ChangesRail, type ChangesFacts, changesFact } from './ChangesRail';
import { PanelComposer, type Reads } from './PanelComposer';
import { type HeadFacts, PanelHead, PanelTrack, useModelWords } from './PanelHead';
import { panelLayoutOf } from './panelLayout';
import type { StoryItem } from './story';
import { StoryStream } from './StoryStream';
import { type WaitAnswer, WaitCard, type WaitCardFacts } from './WaitCard';

/** Everything the panel draws of one session, read by its host from the hooks the page already holds. */
export type PanelFacts = {
  /** The session's id: another one opens fresh, at its tail or, ended, at its top. */
  id: string;
  head: HeadFacts;
  track: Track | null;
  story: readonly StoryItem[];
  /** Every wait of the session on the person, oldest first. */
  waits: readonly WaitCardFacts[];
  changes: ChangesFacts | null;
  /** The page's clock (ms). */
  now: number;
  reads: Reads;
  readMark?: number | null;
  onAnswer: (wait: string, answer: WaitAnswer) => void;
  onSend: (words: string) => void;
  onStep?: (node: string) => void;
  onReview?: () => void;
  onFile?: (path: string) => void;
};

/**
 * **The dock**: every wait of the session as its card, oldest first under its count, pinned. At most 40% of the panel, scrolling
 * inside; narrow, one card whole and the rest a line each, a line's press making it the whole one.
 */
function Dock({ waits, now, narrow, onAnswer }: {
  waits: readonly WaitCardFacts[]; now: number; narrow: boolean; onAnswer: PanelFacts['onAnswer'];
}) {
  const { t } = useTranslation();
  const [whole, setWhole] = useState<string | null>(null);
  const shown = whole && waits.some((wait) => wait.id === whole) ? whole : waits[0]?.id;
  return (
    <div data-region="dock" className="grid max-h-[40%] min-h-0 shrink-0 content-start gap-1.5 overflow-y-auto py-1.5">
      <p className="sticky top-0 m-0 bg-page text-meta font-semibold uppercase tracking-wide text-ink-open">
        {t('work.session.wait.count', { count: waits.length })}
      </p>
      {waits.map((wait) => (
        <WaitCard
          key={wait.id}
          wait={wait}
          now={now}
          layout={narrow && wait.id !== shown ? 'line' : 'whole'}
          onOpen={narrow && wait.id !== shown ? () => setWhole(wait.id) : undefined}
          onAnswer={(answer) => onAnswer(wait.id, answer)}
        />
      ))}
    </div>
  );
}

/**
 * **The session panel** (design §10.3, the fresh design's session panel): one session's whole story and its steering, in one
 * column. A fixed head (the state, the title, where and who) over the track; the story, the only part that scrolls, with the
 * Changes rail beside it from 68rem of the panel's width; at the foot the live band (`band`, the host's), the dock of waits and
 * the composer. Below 40rem the rail goes and the head carries the changes as one fact. With no session it says so; loading, it
 * draws its skeleton in the real layout.
 */
export function SessionPanel({ facts, band, acts, loading = false, width: given }: {
  facts: PanelFacts | null;
  /** What runs now (the host's live band), drawn above the dock. */
  band?: ReactNode;
  /** The head's acts: *Pause*, *Resume*, *Stop* and ⋯, the host's. */
  acts?: ReactNode;
  loading?: boolean;
  /** The panel's width in px where the host knows it (a story, a test); measured otherwise. */
  width?: number;
}) {
  const { t } = useTranslation();
  const root = useRef<HTMLElement>(null);
  const measured = useWidth(root);
  const rem = useRootRem();
  const layout = panelLayoutOf(given ?? measured, rem);
  const modelWords = useModelWords();
  // The story follows its tail only while the reader is at it; an ended session opens at its top. Away from the tail, what
  // arrived since is counted on the pill that goes back.
  const scroller = useRef<HTMLDivElement>(null);
  const count = facts?.story.length ?? 0;
  const ended = !facts || facts.head.state === 'finished' || facts.head.state === 'stopped';
  const { atTail, toTail } = useFollowTail(scroller, `${count}:${facts?.story[count - 1]?.key ?? ''}`, facts?.id ?? '', ended);
  const [seen, setSeen] = useState(count);
  useEffect(() => { if (atTail) setSeen(count); }, [atTail, count]);
  const fresh = atTail ? 0 : Math.max(0, count - seen);
  if (!facts) {
    return (
      <section ref={root} aria-label={t('work.session.label')} className="grid h-full place-content-center">
        <EmptyState icon="inbox" headline={t('work.session.empty.headline')} body={t('work.session.empty.body')} />
      </section>
    );
  }
  const { head, changes } = facts;
  const fact = layout.narrow && changes && changes.files.length > 0 ? (
    <button
      type="button"
      onClick={facts.onReview}
      className="shrink-0 cursor-pointer border-0 bg-transparent p-0 text-small text-accent"
    >
      {changesFact(t, changes)} ▸
    </button>
  ) : null;
  return (
    <section
      ref={root}
      aria-label={t('work.session.label')}
      data-layout={layout.narrow ? 'narrow' : layout.rail ? 'wide' : 'middle'}
      className="flex h-full min-h-0 min-w-0 flex-col gap-1"
    >
      {loading ? (
        <div data-region="head" className="grid gap-1.5 border-b border-line pb-2"><SkeletonRows rows={2} /></div>
      ) : <PanelHead head={head} now={facts.now} acts={acts} changes={fact} />}
      {!loading && facts.track && <PanelTrack track={facts.track} onStep={facts.onStep} />}
      <div className="flex min-h-0 min-w-0 flex-1 gap-4">
        <div className="relative flex min-h-0 min-w-0 flex-1">
          <div ref={scroller} data-region="story" className="min-h-0 min-w-0 flex-1 overflow-y-auto py-2">
            <StoryStream
              story={facts.story}
              actor={{ maker: head.maker, model: head.model }}
              readMark={facts.readMark}
              loading={loading}
            />
          </div>
          {!atTail && count > 0 && (
            <button
              type="button"
              onClick={() => toTail(true)}
              className="absolute bottom-2 left-1/2 -translate-x-1/2 cursor-pointer rounded-full border border-line bg-overlay px-3 py-1 text-small text-ink shadow-sm"
            >
              ↓ {fresh > 0 ? t('work.session.story.newBelow', { count: fresh }) : t('work.conversation.bottom')}
            </button>
          )}
        </div>
        {layout.rail && changes && (
          <aside data-region="rail" className="w-72 shrink-0 overflow-y-auto border-l border-line py-2 pl-4">
            <ChangesRail changes={changes} now={facts.now} onReview={facts.onReview} onFile={facts.onFile} />
          </aside>
        )}
      </div>
      {band && <div data-region="band" className="shrink-0">{band}</div>}
      {facts.waits.length > 0 && <Dock waits={facts.waits} now={facts.now} narrow={layout.narrow} onAnswer={facts.onAnswer} />}
      <div data-region="composer" className="shrink-0">
        <PanelComposer
          reads={facts.reads}
          chip={`${head.maker} · ${modelWords(head)}`}
          onSend={facts.onSend}
          disabled={loading}
        />
      </div>
    </section>
  );
}
