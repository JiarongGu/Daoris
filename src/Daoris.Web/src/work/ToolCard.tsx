import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Dot, Icon, type IconName } from '../ui';
import type { Block, ToolContent } from './conversation';
import { inTree } from './identity';
import { type DiffLine, diffCounts, lineDiff } from './lineDiff';

/** A tool call's ACP kind, as the glyph it wears. Anything else is a generic tool. */
const KIND_ICON: Record<string, IconName> = {
  read: 'read', edit: 'edit', delete: 'edit', move: 'edit', search: 'search', execute: 'execute',
  fetch: 'fetch', think: 'think',
};

/** A tool call's status, as a liveness mark: running is live, a failure is the one alarm. */
const STATUS_TONE: Record<string, 'live' | 'idle' | 'ended' | 'parked'> = {
  pending: 'idle', in_progress: 'live', completed: 'ended', failed: 'parked',
};

/**
 * One tool call in a conversation (D76, CONV2): a row that says what it did — its kind, its title,
 * the file it touched, what an edit changed, and how it went — and opens to what it carried.
 *
 * @remarks
 * **Everything here is the wire's own field** (D52): the kind, title, status, places and content an
 * ACP tool call reports. Nothing is inferred from a title or parsed out of the output. The one
 * change on the way to the screen is a path inside the session's tree, shown relative to it.
 *
 * **Closed by default, open when it failed**: a finished call is a line a reader scans past, and a
 * failed one is the line they came for. An edit shows its `+n −m` closed, because that is the size of
 * the change and it costs one glance.
 *
 * **A call the stop cut reads as stopped** (CONV4b), in the quiet tone and closed, whatever status the
 * harness gave it: the person asked for it. What the harness said is one click away, unchanged.
 *
 * A molecule: the call arrives as props, and the one state it owns is whether it is open.
 */
export function ToolCard({ call, tree }: {
  call: Block;
  /** The session's tree, so a path inside it reads relative to it. */
  tree?: string | null;
}) {
  const { t } = useTranslation();
  const stopped = Boolean(call.stopped);
  const failed = call.status === 'failed' && !stopped;
  // Follows the call until the person opens or closes it (REV3): read once at the first draw, a call
  // drawn running and failing later stayed closed over the line the reader came for.
  const [chosen, setChosen] = useState<boolean | null>(null);
  const open = chosen ?? failed;

  // Each diff once per content, shared by the header's counts and the body (REV3 CLEAN1): a
  // conversation re-renders its cards as it streams, and this was worked out twice every time.
  const lines = useMemo(
    () => (call.content ?? []).map((item) => (item.type === 'diff' ? lineDiff(item.oldText, item.newText) : null)),
    [call.content]);
  const diffs = lines.filter((diff) => diff !== null);
  const counts = diffs.length > 0
    ? diffs.map(diffCounts)
      .reduce((sum, c) => ({ added: sum.added + c.added, removed: sum.removed + c.removed }), { added: 0, removed: 0 })
    : null;
  const where = call.locations?.[0] ? inTree(call.locations[0], tree) : undefined;
  const title = inTree(call.title || call.id || t('work.tool.untitled'), tree);
  const status = call.status ?? 'pending';
  const hasBody = Boolean(call.content?.length || call.input || call.output);

  return (
    <div className={cn('my-1.5 rounded-control border bg-raised', failed ? 'border-warn' : 'border-line')}>
      <button
        type="button"
        aria-expanded={open}
        disabled={!hasBody}
        onClick={() => setChosen(!open)}
        className="flex w-full min-w-0 cursor-pointer items-center gap-2 border-0 bg-transparent px-2.5 py-1.5 text-left disabled:cursor-default"
      >
        <Icon name={hasBody ? (open ? 'chevronDown' : 'chevronRight') : 'tool'} size={13} className="shrink-0 text-ink-faint" />
        <Icon name={KIND_ICON[call.toolKind ?? ''] ?? 'tool'} size={14} className="shrink-0 text-ink-soft" />
        <span className="min-w-0 truncate text-small text-ink">{title}</span>
        {where && !title.includes(where) && (
          <span className="min-w-0 truncate font-mono text-meta text-ink-faint">{where}</span>
        )}
        {counts && (
          <span className="shrink-0 font-mono text-meta">
            <span className="text-st-done">+{counts.added}</span>{' '}
            <span className="text-st-declined">−{counts.removed}</span>
          </span>
        )}
        <Dot
          className="ml-auto shrink-0"
          tone={stopped ? 'idle' : STATUS_TONE[status] ?? 'idle'}
          label={stopped ? t('work.tool.status.stopped') : t(`work.tool.status.${status}`, { defaultValue: status })}
        />
      </button>

      {open && hasBody && (
        <div className="grid gap-2 border-t border-line px-2.5 py-2">
          {(call.content ?? []).map((item, index) => <Content key={index} item={item} lines={lines[index]} tree={tree} />)}
          {call.input && <Raw label={t('work.tool.input')} text={call.input} />}
          {call.output && <Raw label={t('work.tool.output')} text={call.output} />}
        </div>
      )}
    </div>
  );
}

function Content({ item, lines, tree }: { item: ToolContent; lines?: DiffLine[] | null; tree?: string | null }) {
  const { t } = useTranslation();
  if (item.type === 'diff') {
    return (
      <figure className="m-0 overflow-hidden rounded-control border border-line">
        {item.path && (
          <figcaption className="border-b border-line bg-page px-2.5 py-1 font-mono text-meta text-ink-soft">{inTree(item.path, tree)}</figcaption>
        )}
        <pre className="m-0 max-h-96 overflow-auto py-1 font-mono text-small leading-relaxed">
          {(lines ?? lineDiff(item.oldText, item.newText)).map((line, index) => (
            <div
              key={index}
              className={cn(
                'px-2.5 whitespace-pre',
                line.kind === 'add' && 'bg-st-done/10 text-st-done',
                line.kind === 'del' && 'bg-st-declined/10 text-st-declined',
                line.kind === 'same' && 'text-ink-soft',
              )}
            >
              <span aria-hidden className="mr-2 select-none text-ink-faint">
                {line.kind === 'add' ? '+' : line.kind === 'del' ? '−' : ' '}
              </span>
              {line.text}
            </div>
          ))}
        </pre>
      </figure>
    );
  }

  if (item.type === 'terminal') {
    return <p className="m-0 text-small text-ink-soft">{t('work.tool.terminal', { id: item.text ?? '' })}</p>;
  }

  return (
    <pre className="m-0 max-h-80 overflow-auto whitespace-pre-wrap rounded-control bg-page px-2.5 py-2 font-mono text-small text-ink-soft">
      {item.text}
    </pre>
  );
}

function Raw({ label, text }: { label: string; text: string }) {
  return (
    <div>
      <span className="text-meta text-ink-faint">{label}</span>
      <pre className="m-0 mt-0.5 max-h-48 overflow-auto whitespace-pre-wrap break-all rounded-control bg-page px-2.5 py-1.5 font-mono text-meta text-ink-soft">
        {text}
      </pre>
    </div>
  );
}
