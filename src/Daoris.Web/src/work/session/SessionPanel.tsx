import { type ReactNode, type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EmptyState, SkeletonRows } from '../../design/ui';
import { useRootRem, useWidth } from '../../design/useWidth';
import { Composer } from '../conversation/Composer';
import { useFollowTail } from '../conversation/followTail';
import type { Track } from '../track/passage';
import { ChangesRail, type ChangesFacts, changesFact } from './ChangesRail';
import { PanelComposer, type Reads } from './PanelComposer';
import { RunTrack } from '../track/RunTrack';
import { type HeadFacts, type HeadFactsLine, PanelHead, useModelWords } from './PanelHead';
import { PanelNarrow, panelLayoutOf } from './panelLayout';
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
  onAnswer: (wait: string, answer: WaitAnswer) => void | Promise<unknown>;
  onSend: (words: string) => void;
  /** A track chip's press: the step's first rule in the story (`toStepRows`). */
  onStep?: (node: string) => void;
  /** The track's *Flowchart*: the run's view. */
  onFlowchart?: () => void;
  onReview?: () => void;
  onFile?: (path: string) => void;
};

/**
 * **The dock**: every wait of the session as its card, oldest first under its count, pinned. At most 40% of the panel, scrolling
 * inside; narrow, one card whole and the rest a line each, a line's press making it the whole one.
 */
export function Dock({ waits, now, narrow, onAnswer, count = waits.length, children }: {
  waits: readonly WaitCardFacts[]; now: number; narrow: boolean; onAnswer: PanelFacts['onAnswer']; children?: ReactNode;
  /** How many obligations its cards are, as the bell counts them; its cards' number where the host says none. */
  count?: number;
}) {
  const { t } = useTranslation();
  const [whole, setWhole] = useState<string | null>(null);
  const shown = whole && waits.some((wait) => wait.id === whole) ? whole : waits[0]?.id;
  return (
    <div data-region="dock" className="grid max-h-[40%] min-h-0 shrink-0 content-start gap-1.5 overflow-y-auto py-1.5">
      {waits.length > 0 && count > 0 && (
        <p data-dock-count className="sticky top-0 m-0 bg-page text-caption font-semibold uppercase tracking-wide text-you-ink">
          {t('work.session.wait.count', { count })}
        </p>
      )}
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
      {children}
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
export function SessionPanel({
  facts, band, acts, chips, track, asking, dock, composer, story, reading, loading = false, width: given, onChangesShown, onWait, headFacts,
}: {
  facts: PanelFacts | null;
  /** Told whether the layout shows the changes (the rail, or narrow the head's fact), on a change: its host reads them only then. */
  onChangesShown?: (shown: boolean) => void;
  /** The head's oldest wait's press: to its card in the dock. */
  onWait?: () => void;
  /** The head's facts line, the host's (`sessionFacts`). */
  headFacts?: HeadFactsLine | null;
  /** The head's chips (`HeadChips`), the host's: they read the record, which the facts do not carry. */
  chips?: ReactNode;
  /** The host's track (`RunTrack` with its holders read from its run), in place of the one drawn from `facts.track`. */
  track?: ReactNode;
  /** What asks under the head: a stop's, a delete's, a rename's, a hand-over's or a pause's ask, the host's. */
  asking?: ReactNode;
  /** What runs now (the host's live band), drawn above the dock. */
  band?: ReactNode;
  /** The head's acts: *Pause*, *Resume*, *Stop* and ⋯, the host's. */
  acts?: ReactNode;
  /** The host's dock (`SessionDock`), in place of the cards of `facts.waits`: the page's, which reads its waits itself. */
  dock?: ReactNode;
  /** The host's composer (`PanelComposer` over the box the session is offered); null where the session takes no words. */
  composer?: ReactNode;
  /** The host's drawing of the story, in place of the stream over `facts.story`: the page's, with its way through the record. */
  story?: ReactNode;
  /** The story region and its tail where the host's reading follows it, not the panel. */
  reading?: { scroller: RefObject<HTMLDivElement | null>; atTail: boolean; toTail: (smooth?: boolean) => void };
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
  // arrived since is counted on the pill that goes back. A host that reads the record follows it itself.
  const own = useRef<HTMLDivElement>(null);
  const nobody = useRef<HTMLDivElement>(null);
  const scroller = reading?.scroller ?? own;
  const count = facts?.story.length ?? 0;
  const ended = !facts || facts.head.state === 'finished' || facts.head.state === 'stopped';
  const followed = useFollowTail(reading ? nobody : own, `${count}:${facts?.story[count - 1]?.key ?? ''}`, facts?.id ?? '', ended);
  const { atTail, toTail } = reading ?? followed;
  // What the reader has seen, by the session: another opens with all of its lines seen, and an ended one counts nothing new.
  const [seen, setSeen] = useState({ id: facts?.id, count });
  if (seen.id !== facts?.id) setSeen({ id: facts?.id, count });
  useEffect(() => { if (atTail) setSeen({ id: facts?.id, count }); }, [atTail, count, facts?.id]);
  const fresh = atTail || ended || seen.id !== facts?.id ? 0 : Math.max(0, count - seen.count);
  const changesShown = layout.rail || layout.narrow;
  useEffect(() => { onChangesShown?.(changesShown); }, [changesShown, onChangesShown]);
  const back = () => {
    toTail(true);
    scroller.current?.focus({ preventScroll: true });
  };
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
      className="shrink-0 cursor-pointer border-0 bg-transparent p-0 text-small text-accent-ink"
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
      ) : <PanelHead head={head} now={facts.now} acts={acts} changes={fact} chips={chips} onWait={onWait} facts={headFacts} />}
      {!loading && (track || facts.track) && (
        <div data-region="track" className="shrink-0">
          {track ?? (facts.track && (
            <RunTrack
              track={facts.track}
              holderOf={() => t('workflow.wait.holder.person')}
              onChip={(node) => facts.onStep?.(node)}
              onFlowchart={() => facts.onFlowchart?.()}
            />
          ))}
        </div>
      )}
      {asking && <div data-region="asking" className="shrink-0">{asking}</div>}
      <div className="flex min-h-0 min-w-0 flex-1 gap-4">
        <div className="relative flex min-h-0 min-w-0 flex-1">
          <div
            ref={scroller}
            data-region="story"
            tabIndex={-1}
            className="min-h-0 min-w-0 flex-1 overflow-y-auto py-2 outline-none"
          >
            {story ?? (
              <StoryStream
                story={facts.story}
                actor={{ maker: head.maker, model: head.model }}
                readMark={facts.readMark}
                loading={loading}
              />
            )}
          </div>
          {!atTail && count > 0 && (
            <button
              type="button"
              onClick={back}
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
      <PanelNarrow.Provider value={layout.narrow}>
        {dock ?? (facts.waits.length > 0 && <Dock waits={facts.waits} now={facts.now} narrow={layout.narrow} onAnswer={facts.onAnswer} />)}
      </PanelNarrow.Provider>
      {composer !== null && (
        <div data-region="composer" className="shrink-0">
          {composer ?? (
            <PanelComposer reads={facts.reads} chip={`${head.maker} · ${modelWords(head)}`}>
              <Composer
                key={facts.id}
                live={!loading}
                endings={false}
                attachments={false}
                placeholder={t('work.session.composer.placeholder')}
                onSend={(words) => facts.onSend(words.trim())}
                onFinish={() => {}}
              />
            </PanelComposer>
          )}
        </div>
      )}
    </section>
  );
}
