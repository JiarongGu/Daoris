import { useContext, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { span } from '../format';
import { cn } from '../lib/cn';
import { Dot, Icon, type IconName } from '../ui';
import type { Block, ToolContent } from './conversation';
import { languageOf } from './codeLines';
import { inTree } from './identity';
import { type DiffLine, diffCounts, lineDiff, patchLines } from './lineDiff';
import { DiffLines } from './PatchView';
import { FileOpener, type LineRange, namedLines, treePath } from './preview';

/**
 * A file the card names, as a door into the side bar's preview (PREVIEW1, D111): the path relative to
 * the tree, underlined as a link is, and never the card's own toggle — a press opens the file, not the card.
 */
function PreviewDoor({ path, lines, open }: { path: string; lines: LineRange | null; open: (file: { path: string; lines: LineRange | null }) => void }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      onClick={(event) => {
        event.stopPropagation();
        open({ path, lines });
      }}
      aria-label={lines
        ? t('work.preview.openLines', { path, from: lines.from, to: lines.to })
        : t('work.preview.open', { path })}
      // Gives way at its FRONT, as the review's paths do: the file's name is the half a person looks for.
      dir="rtl"
      className="min-w-0 shrink cursor-pointer truncate border-0 bg-transparent p-0 text-left font-mono text-meta text-ink-soft underline decoration-line-strong decoration-dotted underline-offset-2 hover:text-accent hover:decoration-accent"
    >
      <span dir="ltr">{path}</span>
    </button>
  );
}

/** A title around the path it names: what reads before the path and after it, the wrapping quotes left off. */
function around(title: string, where: string): { lead: string; trail: string } | null {
  const at = title.indexOf(where);
  if (at < 0) return null;
  return {
    lead: title.slice(0, at).replace(/[\s`'"]+$/, ''),
    trail: title.slice(at + where.length).replace(/^[`'"]+/, '').trim(),
  };
}

/** A tool call's ACP kind, as the glyph it wears. Anything else is a generic tool. */
const KIND_ICON: Record<string, IconName> = {
  read: 'read', edit: 'edit', delete: 'edit', move: 'edit', search: 'search', execute: 'execute',
  fetch: 'fetch', think: 'think',
};

/**
 * A tool call's status, as a liveness mark: running is live, a failure is the one alarm. A call the
 * driver refused (HELP4) is not a failure of the harness's, so it is quiet.
 */
const STATUS_TONE: Record<string, 'live' | 'idle' | 'ended' | 'failed'> = {
  pending: 'idle', in_progress: 'live', completed: 'ended', failed: 'failed', refused: 'idle',
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
  // Never the id (SESS1 S5): a call the page holds only the updates of says it began earlier.
  const title = inTree(call.title || t(call.continued ? 'work.tool.continued' : 'work.tool.untitled'), tree);
  const status = call.status ?? 'pending';
  const hasBody = Boolean(call.content?.length || call.input || call.output);
  // How long it ran, by the driver's clock (SESS1): from its first event to the update that ended it —
  // said from a second on, since under that is the time the wire takes, not the call.
  const took = call.finished ? Date.parse(call.finished) - Date.parse(call.at) : Number.NaN;
  // How much it carried, closed: the lines of its text and output. An edit says its size as +n −m.
  const texts = [...(call.content ?? []).filter((item) => item.type !== 'diff').map((item) => item.text ?? ''), call.output ?? ''];
  const carried = counts ? 0 : texts.reduce((sum, text) => sum + (text ? text.split(/\r?\n/).length : 0), 0);

  // The file this call acted on, as a door into the preview (PREVIEW1, D111): only where the frame opens
  // previews, and only a path the page can see is inside the tree. The lines are a read's own input's.
  const opener = useContext(FileOpener);
  const previewing = opener && call.locations?.[0] ? treePath(call.locations[0], tree) : null;
  const named = previewing && call.toolKind === 'read' ? namedLines(call.input) : null;
  const split = previewing && where ? around(title, where) : null;
  const toggle = () => setChosen(!open);

  const chevron = <Icon name={hasBody ? (open ? 'chevronDown' : 'chevronRight') : 'tool'} size={13} className="shrink-0 text-ink-faint" />;
  const kind = <Icon name={KIND_ICON[call.toolKind ?? ''] ?? 'tool'} size={14} className="shrink-0 text-ink-soft" />;
  const marks = (
    <>
      {counts && (
        <span className="shrink-0 font-mono text-meta">
          <span className="text-st-done">+{counts.added}</span>{' '}
          <span className="text-st-declined">−{counts.removed}</span>
        </span>
      )}
      {!open && carried > 1 && (
        <span className="shrink-0 text-meta tabular-nums text-ink-faint">{t('work.tool.lines', { count: carried })}</span>
      )}
      {took >= 1000 && <span className="ml-auto shrink-0 text-meta tabular-nums text-ink-faint">{span(took)}</span>}
      <Dot
        className={cn('shrink-0', !(took >= 1000) && 'ml-auto')}
        tone={stopped ? 'idle' : STATUS_TONE[status] ?? 'idle'}
        label={stopped ? t('work.tool.status.stopped') : t(`work.tool.status.${status}`, { defaultValue: status })}
      />
    </>
  );

  return (
    <div className={cn('my-1.5 rounded-control border bg-raised', failed ? 'border-warn' : 'border-line')}>
      {previewing && opener
        ? (
          // The row still opens the card anywhere but the path; the path is its own door, so the two are
          // siblings — a button inside a button is neither. The toggle is named by the whole title.
          <div
            className={cn('flex w-full min-w-0 items-center gap-2 px-2.5 py-1.5', hasBody && 'cursor-pointer')}
            onClick={hasBody ? toggle : undefined}
          >
            <button
              type="button"
              aria-expanded={open}
              aria-label={title}
              disabled={!hasBody}
              onClick={(event) => {
                event.stopPropagation();
                toggle();
              }}
              className="flex min-w-0 cursor-pointer items-center gap-2 border-0 bg-transparent p-0 text-left disabled:cursor-default"
            >
              {chevron}
              {kind}
              {(split ? split.lead : title) && (
                <span className="min-w-0 truncate text-small text-ink">{split ? split.lead : title}</span>
              )}
            </button>
            <PreviewDoor path={previewing} lines={named} open={opener} />
            {split?.trail && <span className="min-w-0 truncate text-small text-ink">{split.trail}</span>}
            {marks}
          </div>
        )
        : (
          <button
            type="button"
            aria-expanded={open}
            disabled={!hasBody}
            onClick={toggle}
            className="flex w-full min-w-0 cursor-pointer items-center gap-2 border-0 bg-transparent px-2.5 py-1.5 text-left disabled:cursor-default"
          >
            {chevron}
            {kind}
            <span className="min-w-0 truncate text-small text-ink">{title}</span>
            {where && !title.includes(where) && (
              <span className="min-w-0 truncate font-mono text-meta text-ink-faint">{where}</span>
            )}
            {marks}
          </button>
        )}

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
  const opener = useContext(FileOpener);
  if (item.type === 'diff') {
    // The file an edit wrote, as a door into its preview where one opens (PREVIEW1): an edit names no lines.
    const previewing = opener && item.path ? treePath(item.path, tree) : null;
    return (
      <figure className="m-0 overflow-hidden rounded-control border border-line">
        {item.path && (
          <figcaption className="flex min-w-0 border-b border-line bg-page px-2.5 py-1 font-mono text-meta text-ink-soft">
            {previewing && opener ? <PreviewDoor path={previewing} lines={null} open={opener} /> : inTree(item.path, tree)}
          </figcaption>
        )}
        {/* The review's own lines (REVIEW2), highlighted in the file's language — unnumbered, since an
            edit's text is a piece of the file and its lines are not the file's. */}
        <div className="max-h-96 overflow-auto py-1 font-mono text-small leading-relaxed">
          <DiffLines
            lines={patchLines(lines ?? lineDiff(item.oldText, item.newText))}
            language={item.path ? languageOf(item.path) : null}
            numbered={false}
          />
        </div>
      </figure>
    );
  }

  if (item.type === 'terminal') {
    return <p className="m-0 text-small text-ink-soft">{t('work.tool.terminal', { id: item.text ?? '' })}</p>;
  }

  return (
    <pre className="m-0 max-h-80 overflow-auto whitespace-pre-wrap rounded-control bg-page px-2.5 py-2 font-mono text-small text-ink-soft">
      {unfenced(item.text ?? '')}
    </pre>
  );
}

/**
 * A tool's text, without the Markdown fence an adapter wrapped it in (UX5 U63). The protocol door's
 * Claude Code adapter sends a read's text as one fenced block, and the card showed the fence as the
 * output's first and last lines, where the native door's same read has none. Only a text that is
 * exactly one fenced block is unwrapped — the fence is the adapter's wrapping, not the file — and
 * anything else, a fence inside shell output included, is shown as it came.
 */
export function unfenced(text: string): string {
  const match = /^```[^\n`]*\n([\s\S]*?)\n```[ \t]*\n?$/.exec(text);
  if (!match) return text;
  const inside = match[1]!;
  return /^```/m.test(inside) ? text : inside;
}

function Raw({ label, text }: { label: string; text: string }) {
  return (
    <div>
      <span className="text-meta text-ink-faint">{label}</span>
      <pre className="m-0 mt-0.5 max-h-48 overflow-auto whitespace-pre-wrap wrap-anywhere rounded-control bg-page px-2.5 py-1.5 font-mono text-meta text-ink-soft">
        {text}
      </pre>
    </div>
  );
}
