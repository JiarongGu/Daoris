import { Fragment, type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SkeletonRows } from '../../design/ui';
import { cn } from '../../lib/cn';
import { Markdown } from '../conversation/Markdown';
import type { StoryItem, StoryWho } from './story';

/** The session's agent as its lines name it: its maker and the model that ran, null where the record named none. */
export type StoryActor = { maker: string; model: string | null };

type Of<K extends StoryItem['kind']> = Extract<StoryItem, { kind: K }>;

/** A line's actor, small, above or beside its words. */
function Actor({ children, tone = 'soft' }: { children: ReactNode; tone?: 'soft' | 'accent' | 'open' }) {
  return (
    <span className={cn('text-small font-medium', tone === 'accent' ? 'text-accent' : tone === 'open' ? 'text-ink-open' : 'text-ink-soft')}>
      {children}
    </span>
  );
}

function useWho() {
  const { t } = useTranslation();
  return (who: StoryWho) => (who === 'you' ? t('work.session.story.you')
    : who === 'daoris' ? t('work.session.story.daoris')
      : who === 'supervisor' ? t('work.session.story.supervisor') : who);
}

/** A step's rule: the step, its edge and what it came to, across the stream. */
function StepRule({ item }: { item: Of<'step'> }) {
  const { t } = useTranslation();
  const said = t(item.edge === 'start' ? 'work.session.story.stepStart' : 'work.session.story.stepEnd', { node: item.node });
  const facts = [
    item.edge === 'end' ? t(`work.session.story.mark.${item.mark}`) : null,
    item.attempt && item.attempt > 1 ? t('work.session.story.attempt', { attempt: item.attempt }) : null,
  ].filter(Boolean).join(' · ');
  const glyph = item.edge === 'start' ? '●' : item.mark === 'passed' ? '✓' : item.mark === 'failed' ? '▲' : '–';
  return (
    <div data-part="step" className="flex items-center gap-2 py-1 text-small text-ink-soft">
      <span className="h-px w-4 bg-line-strong" aria-hidden="true" />
      <span className={cn('font-semibold', item.mark === 'failed' ? 'text-ink-danger' : item.mark === 'passed' ? 'text-ink-done' : 'text-ink')}>
        <span aria-hidden="true">{glyph} </span>{said}
      </span>
      {facts && <span className="text-ink-faint">{facts}</span>}
      <span className="h-px flex-1 bg-line-strong" aria-hidden="true" />
    </div>
  );
}

/** A burst of activity folded to one line that counts its calls, opened in place to each call. */
function ToolsFold({ item }: { item: Of<'tools'> }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const said = [
    ...item.calls.map((call) => t(`work.session.story.calls.${call.kind}`, { count: call.count })),
    ...(item.calls.length === 0 ? [t('work.session.story.calls.thought')] : []),
    ...(item.failed > 0 ? [t('work.session.story.calls.failed', { count: item.failed })] : []),
  ].join(' · ');
  return (
    <div data-part="tools" className="grid gap-1">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-fit cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-soft hover:text-ink"
      >
        <span aria-hidden="true">{open ? '▾' : '▸'}</span>
        <span className={cn(item.failed > 0 && 'text-ink-danger')}>{said}</span>
      </button>
      {open && (
        <ul className="m-0 grid list-none gap-0.5 border-l border-line pl-3">
          {item.blocks.filter((block) => block.kind === 'tool').map((block) => (
            <li key={block.key} className={cn('truncate text-small', block.status === 'failed' ? 'text-ink-danger' : 'text-ink-soft')}>
              {block.title ?? block.toolKind ?? block.kind}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Line({ item, actor }: { item: StoryItem; actor: StoryActor }) {
  const { t } = useTranslation();
  const who = useWho();
  const agent = `${actor.maker} · ${actor.model ?? t('work.session.head.notReported')}`;
  switch (item.kind) {
    case 'step':
      return <StepRule item={item} />;
    case 'tools':
      return <ToolsFold item={item} />;
    case 'owner':
      return (
        <div data-part="owner" className="grid gap-0.5 rounded-control bg-sunken px-2.5 py-1.5">
          <Actor tone="accent">{who(item.who)}</Actor>
          <p className="m-0 whitespace-pre-line text-body text-ink">{item.text}</p>
          {item.held && <span className="text-meta text-ink-faint">{t('work.session.story.held')}</span>}
        </div>
      );
    case 'agent':
      return (
        <div data-part="agent" className="grid min-w-0 gap-0.5">
          <Actor>{agent}</Actor>
          <Markdown text={item.text} />
        </div>
      );
    case 'note':
      return (
        <p data-part="note" className="m-0 text-small text-ink-soft">
          <span aria-hidden="true">◇ </span><Actor>{t('work.session.story.daoris')}</Actor> {item.text}
        </p>
      );
    case 'helper':
      return (
        <div data-part="helper" className="grid gap-0.5 pl-4">
          <p className="m-0 text-small text-ink-soft">
            <span aria-hidden="true">↗ </span>
            {item.agent ? t('work.session.story.helper', { agent: item.agent }) : t('work.session.story.helperSomeone')}
            {': '}<span className="text-ink">{item.question}</span>
          </p>
          <p className="m-0 pl-4 text-small text-ink-soft">
            <span aria-hidden="true">← </span>
            {item.answer ? <span className="text-ink">{item.answer.text}</span> : t('work.session.story.helperWaiting')}
          </p>
        </div>
      );
    case 'opinion':
      return (
        <div data-part="opinion" className="grid gap-0.5">
          <Actor>
            <span aria-hidden="true">⚖ </span>
            {[t('work.session.story.opinion'), item.reviewer, item.model].filter(Boolean).join(' · ')}
          </Actor>
          <Markdown text={item.text} />
        </div>
      );
    case 'channel':
      return (
        <div data-part="channel" className="grid gap-0.5">
          <Actor><span aria-hidden="true">⇢ </span>{`${t('work.session.story.channel')} · ${who(item.who)}`}</Actor>
          <p className="m-0 whitespace-pre-line text-body text-ink">{item.text}</p>
        </div>
      );
    default:
      return null;
  }
}

/**
 * **The story**: the session's one stream, each line its kind's part in the order it happened (`storyOf`). Steps are labelled rules;
 * the person's words stand on the sunken ground; the agent's name its maker and the model that ran; each burst of activity is one
 * line that opens to each call; a helper's question and answer sit indented; a second opinion and a channel message are a line each.
 * The person's read mark is a dashed rule before the first line after it.
 */
export function StoryStream({ story, actor, readMark, loading = false }: {
  story: readonly StoryItem[];
  actor: StoryActor;
  /** When the person last read this session (ms); absent, no mark is drawn. */
  readMark?: number | null;
  loading?: boolean;
}) {
  const { t } = useTranslation();
  if (loading) {
    return (
      <div className="grid gap-2" aria-busy="true">
        <p className="m-0 text-small text-ink-faint">{t('work.session.story.loading')}</p>
        <SkeletonRows rows={4} />
      </div>
    );
  }
  if (story.length === 0) return <p className="m-0 py-6 text-center text-body text-ink-faint">{t('work.session.story.empty')}</p>;
  const since = readMark == null ? -1 : story.findIndex((item) => item.at > readMark);
  return (
    <ol aria-label={t('work.session.story.label')} className="m-0 grid list-none gap-2.5 p-0">
      {story.map((item, index) => (
        <Fragment key={item.key}>
          {index === since && (
            <li data-part="since" className="flex items-center gap-2 text-meta text-ink-open">
              <span className="flex-1 border-t border-dashed border-st-open" aria-hidden="true" />
              {t('work.session.story.since')}
              <span className="w-4 border-t border-dashed border-st-open" aria-hidden="true" />
            </li>
          )}
          <li data-block={item.key} className="min-w-0">
            <Line item={item} actor={actor} />
          </li>
        </Fragment>
      ))}
    </ol>
  );
}
