import { useMemo } from 'react';
import { ago } from '../../locales/format';
import { type GraphCommit, graphLanes } from './graphLanes';

/**
 * A commit as the graph draws it: what its row says, beside what its lanes are laid from. The bridge's `GitLogCommit` is
 * one, so `GIT_GRAPH`'s commits are handed as they are answered, and the molecule imports nothing of the bridge (D52).
 */
export type GraphCommitRow = GraphCommit & {
  subject: string;
  author: string;
  authoredAt: string | null;
  committedAt: string | null;
};

/** A row's height, which the lanes are drawn against: every row is one line, so each commit's dot sits at its centre. */
const ROW = 32;
/** A lane's width, and the gutter before the first. */
const LANE = 14;
const GUTTER = 8;
const DOT = 4;

const NO_TIPS: readonly string[] = [];

const x = (lane: number) => GUTTER + lane * LANE;
const y = (row: number) => row * ROW + ROW / 2;

/**
 * **A commit graph** (UX6h1, the built-in Git design §2.3): its commits newest first, each a row with its subject, its short
 * id, its author and when it was committed, beside the lanes `graphLanes` lays from their parents. A merge's dot is hollow.
 *
 * @remarks
 * **A molecule**: the commits arrive as a prop, as `GIT_GRAPH` answers them, the pages accumulated. The lanes are one drawing
 * beside the rows, hidden from a reader, which reads the rows; every row is one line at one height so a dot meets its row.
 */
export function CommitGraph({ label, commits, tips = NO_TIPS }: {
  /** The list's accessible name: whose history, on which line. */
  label: string;
  commits: readonly GraphCommitRow[];
  /** The tips the graph was walked from, in the order asked: the first keeps the first lane. */
  tips?: readonly string[];
}) {
  const layout = useMemo(() => graphLanes(commits, tips), [commits, tips]);
  const width = GUTTER * 2 + Math.max(0, layout.lanes - 1) * LANE;
  const height = layout.rows.length * ROW;

  return (
    // `min-w-0`: in a grid its width is the tab's, not its longest subject's, so a subject truncates and the row's last
    // columns stay in view (the installed window, 2026-10-11).
    <div className="relative min-w-0">
      <svg
        aria-hidden
        data-lanes={layout.lanes}
        width={width}
        height={height}
        className="pointer-events-none absolute left-0 top-0"
      >
        {layout.rows.flatMap((row, at) => row.lines.map((line) => {
          const top = y(at);
          // The last row's lines run off its bottom: the next page picks them up.
          const bottom = at + 1 < layout.rows.length ? y(at + 1) : height;
          const middle = (top + bottom) / 2;
          return (
            <path
              key={`${at}-${line.from}-${line.to}`}
              d={`M ${x(line.from)} ${top} C ${x(line.from)} ${middle} ${x(line.to)} ${middle} ${x(line.to)} ${bottom}`}
              fill="none"
              strokeWidth={1.5}
              className="stroke-ink-faint"
            />
          );
        }))}
        {layout.rows.map((row, at) => {
          const merge = (commits[at]?.parents.length ?? 0) > 1;
          return (
            <circle
              key={row.commit}
              cx={x(row.lane)}
              cy={y(at)}
              r={DOT}
              strokeWidth={1.5}
              data-merge={merge || undefined}
              className={merge ? 'fill-page stroke-accent' : 'fill-accent stroke-accent'}
            />
          );
        })}
      </svg>
      <ol aria-label={label} className="m-0 list-none p-0" style={{ paddingLeft: width + 4 }}>
        {commits.map((commit) => {
          const at = commit.committedAt ?? commit.authoredAt;
          return (
            <li key={commit.commit} data-commit={commit.commit} tabIndex={-1} className="flex min-w-0 items-center gap-2" style={{ height: ROW }}>
              <span className="min-w-0 flex-1 truncate text-body text-ink">{commit.subject}</span>
              <code className="shrink-0 font-mono text-small text-ink-soft">{commit.commit.slice(0, 7)}</code>
              {/* The author is a column of its own, and gives way first, by the main area's width, never the window's
                  (D118 §3b). */}
              <span className="hidden w-32 shrink-0 truncate text-small text-ink-soft @xl/main:inline">{commit.author}</span>
              {at && <span className="shrink-0 text-meta text-ink-faint tabular-nums">{ago(at)}</span>}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
