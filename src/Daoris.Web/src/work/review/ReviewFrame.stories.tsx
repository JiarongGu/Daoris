import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { Button, EmptyState } from '../../design/ui';
import type { DiffFile, LineBehind, ReviewKnown } from './diff';
import type { DiffLayout } from './PatchView';
import { NothingToLand, ReviewFailed, ReviewFiles, ReviewHead, ReviewReading } from './ReviewFrame';

// The review while git reads (REVIEW4), as the side bar holds it at a usual width: loading at once, slow with how long,
// each kind of refusal with its next move, loaded, and reading again behind the last answer — in light and dark, and
// 中文 where its words differ.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const KNOWN: ReviewKnown = {
  title: 'Draw the report header from the quarter’s columns, and keep the totals row pinned',
  repository: 'portal-ui',
  branch: 'daoris/s-2394e5d9',
  base: '0fda18c2b7e4a9d1',
  commits: 4,
  machine: null,
};

const FILES: DiffFile[] = [
  {
    path: 'src/report/header.tsx', status: 'modified', added: 18, removed: 6,
    patch: '@@ -1,4 +1,6 @@\n import { columns } from "./columns";\n-export const Header = () => null;\n+export function Header({ quarter }: { quarter: string }) {\n+  return <tr>{columns(quarter).map((c) => <th key={c}>{c}</th>)}</tr>;\n+}\n',
  },
  { path: 'src/report/columns.ts', status: 'added', added: 31, removed: 0, patch: '@@ -0,0 +1,2 @@\n+export const columns = (quarter: string) => [quarter, "total"];\n+\n' },
  { path: 'src/report/totals.tsx', status: 'modified', added: 4, removed: 2, patch: '@@ -1 +1 @@\n-sticky=false\n+sticky\n' },
  { path: 'assets/report-logo.png', status: 'added', added: null, removed: null, patch: null },
];

const refused = (code: string) => Object.assign(new Error('the host’s English'), { code, parameters: { session: 's1a2b3c4' } });

/** The side bar's column at a usual width: the review's frame, then its body. */
function Pane({ children }: { children: ReactNode }) {
  return <section className="flex h-[34rem] w-[360px] max-w-full flex-col border border-line bg-page">{children}</section>;
}

/**
 * The loaded review with the reader's marks held as the pane holds them; the first file open on its patch. The branch is the
 * record's guess unless the answer named the tree's own (REVIEWBASE4).
 */
function Loaded({ refreshing = null, behind = null, branch = KNOWN.branch, detached = false }: {
  refreshing?: number | null;
  behind?: LineBehind | null;
  branch?: string | null;
  detached?: boolean;
}) {
  const [open, setOpen] = useState<Record<string, boolean>>({ [FILES[0]!.path]: true });
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  const [layout, setLayout] = useState<DiffLayout>('unified');
  return (
    <Pane>
      <ReviewHead {...KNOWN} branch={branch} detached={detached} refreshing={refreshing} behind={behind} />
      <div aria-busy={refreshing !== null || undefined} className={cn('flex min-h-0 flex-1 flex-col', refreshing !== null && 'opacity-60')}>
        <ReviewFiles
          files={FILES}
          open={open}
          viewed={viewed}
          layout={layout}
          onLayout={setLayout}
          onToggle={(path) => setOpen((was) => ({ ...was, [path]: !was[path] }))}
          onViewed={(path, next) => setViewed((was) => ({ ...was, [path]: next }))}
          onPreview={() => {}}
        />
      </div>
    </Pane>
  );
}

const meta: Meta<typeof ReviewReading> = {
  title: 'Work/ReviewFrame',
  component: ReviewReading,
};
export default meta;

type Story = StoryObj<typeof ReviewReading>;

/** A first read, the moment it begins: the frame from the record, its words on the count's line, the skeleton. */
export const Loading: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewReading repository={KNOWN.repository} since={Date.now()} />
    </Pane>
  ),
};

/** Loading, in dark. */
export const LoadingDark: Story = { ...Loading, decorators: [dark] };

/** Loading, in 中文. */
export const LoadingChinese: Story = { ...Loading, decorators: [chinese] };

/** A read of a large session, 23 seconds in: the words say for how long, counting each second. */
export const Slow: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewReading repository={KNOWN.repository} since={Date.now() - 23_000} />
    </Pane>
  ),
};

/** Slow, in dark. */
export const SlowDark: Story = { ...Slow, decorators: [dark] };

/** Slow, in 中文: its number set apart. */
export const SlowChinese: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewReading repository={KNOWN.repository} since={Date.now() - 83_000} />
    </Pane>
  ),
  decorators: [chinese],
};

/** A wait that ran out: what happened, why, and *Read again*. */
export const Failed: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewFailed error={refused('TIMEOUT')} onRetry={() => {}} />
    </Pane>
  ),
};

/** Failed, in dark. */
export const FailedDark: Story = { ...Failed, decorators: [dark] };

/** Failed, in 中文. */
export const FailedChinese: Story = { ...Failed, decorators: [chinese] };

/** A record from another machine: no tree here, the machine named, and nothing to read again. */
export const OnAnotherMachine: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} branch={null} machine="studio-pc" />
      <ReviewFailed error={refused('SESSION_NOT_REVIEWABLE')} machine="studio-pc" onRetry={() => {}} />
    </Pane>
  ),
};

/** On another machine, in dark. */
export const OnAnotherMachineDark: Story = { ...OnAnotherMachine, decorators: [dark] };

/** A tree gone with no landing recorded: where its work went, and what a landed branch would have given. */
export const TreeGone: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewFailed error={refused('SESSION_TREE_GONE')} onRetry={() => {}} />
    </Pane>
  ),
};

/** The driver still starting: it reads again by itself, and offers to now. */
export const DriverStarting: Story = {
  render: () => (
    <Pane>
      <ReviewHead {...KNOWN} />
      <ReviewFailed error={refused('DRIVER_NOT_READY')} onRetry={() => {}} />
    </Pane>
  ),
};

/** Loaded: the frame with the range git measured from, the files, the first open on its patch. */
export const Ready: Story = { render: () => <Loaded /> };

/** Loaded, in dark. */
export const ReadyDark: Story = { ...Ready, decorators: [dark] };

/** Loaded, in 中文. */
export const ReadyChinese: Story = { ...Ready, decorators: [chinese] };

/** A tree holding work no commit does (REVIEWDIRTY1): the committed files, then the uncommitted under their own heading, at 680 px. */
function Dirty({ committed = true }: { committed?: boolean }) {
  const [open, setOpen] = useState<Record<string, boolean>>({ 'uncommitted:src/report/draft.ts': true });
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  const [layout, setLayout] = useState<DiffLayout>('unified');
  return (
    <section className="flex h-[34rem] w-[680px] max-w-full flex-col border border-line bg-page">
      <ReviewHead {...KNOWN} />
      <div className="flex min-h-0 flex-1 flex-col">
        <ReviewFiles
          files={committed ? FILES.slice(0, 2) : []}
          open={open}
          viewed={viewed}
          layout={layout}
          onLayout={setLayout}
          onToggle={(path) => setOpen((was) => ({ ...was, [path]: !was[path] }))}
          onViewed={(path, next) => setViewed((was) => ({ ...was, [path]: next }))}
          uncommitted={{
            files: [
              { path: 'src/report/draft.ts', status: 'modified', added: 3, removed: 1, patch: '@@ -1 +1,3 @@\n-sticky=false\n+sticky\n+pinned\n+totals\n' },
              { path: 'src/report/notes.md', status: 'added', added: 12, removed: 0, patch: '@@ -0,0 +1 @@\n+# Notes\n' },
              { path: 'assets/export.bin', status: 'added', added: null, removed: null, patch: '' },
            ],
            truncated: false,
          }}
        />
      </div>
    </section>
  );
}

/** Uncommitted work beside committed work, apart under its own heading. */
export const DirtyTree: Story = { render: () => <Dirty /> };

/** The same, in dark. */
export const DirtyTreeDark: Story = { ...DirtyTree, decorators: [dark] };

/** The same, in 中文. */
export const DirtyTreeChinese: Story = { ...DirtyTree, decorators: [chinese] };

/** Only uncommitted work: the session has committed nothing yet, and its work is below. */
export const DirtyTreeNothingCommitted: Story = { render: () => <Dirty committed={false} /> };

/** A live session's second open: the last answer held dimmed while git reads again, the head saying so. */
export const ReadingAgain: Story = { render: () => <Loaded refreshing={Date.now() - 6_000} /> };

/** Reading again, in dark. */
export const ReadingAgainDark: Story = { ...ReadingAgain, decorators: [dark] };

/**
 * Work not based on the latest line (REVIEWBASE1): the line the host knows has 7 commits this work does not, said under the facts
 * with *Updates*, the door that says whether the tree can be brought up to date.
 */
export const BehindTheLine: Story = { render: () => <Loaded behind={{ line: 'main', commits: 7, ahead: 4 }} /> };

/** Behind the line, in dark. */
export const BehindTheLineDark: Story = { ...BehindTheLine, decorators: [dark] };

/** Behind the line, in 中文. */
export const BehindTheLineChinese: Story = { ...BehindTheLine, decorators: [chinese] };

/**
 * A tree whose HEAD the line holds whole, as the review's pane says it: its work merged, then the line checked out detached and
 * its `daoris/` branch deleted (REVIEWBASE4). The head names no branch, the foot says there is nothing to land where *Accept* would
 * be, and *Discard tree* leads.
 */
function NothingBeyond() {
  const { t } = useTranslation();
  return (
    <Pane>
      <ReviewHead
        {...KNOWN}
        branch={null}
        detached
        base="9c41e07d2b5a8f13"
        commits={null}
        behind={{ line: 'main', commits: 0, ahead: 0 }}
      />
      <div className="min-h-0 flex-1">
        <EmptyState icon="check" headline={t('work.review.even.headline')} body={t('work.review.even.body', { line: 'main' })} />
      </div>
      <footer className="grid shrink-0 gap-2 border-t border-line px-3 py-2">
        <NothingToLand line="main" />
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="primary">{t('work.review.discard')}</Button>
        </div>
      </footer>
    </Pane>
  );
}

/** Nothing beyond the line (REVIEWBASE1), where "Nothing landed" would be false, and nothing to land (REVIEWBASE4). */
export const NothingBeyondTheLine: Story = { render: () => <NothingBeyond /> };

/** Nothing beyond the line, in dark. */
export const NothingBeyondTheLineDark: Story = { ...NothingBeyondTheLine, decorators: [dark] };

/** Nothing beyond the line, in 中文. */
export const NothingBeyondTheLineChinese: Story = { ...NothingBeyondTheLine, decorators: [chinese] };

/** A tree its agent switched to a branch of its own (TREEFOLLOW1): the head names that branch, not the record's `daoris/` guess. */
export const OnItsOwnBranch: Story = { render: () => <Loaded branch="feature/TK-9-report-header" /> };

/** On its own branch, in dark. */
export const OnItsOwnBranchDark: Story = { ...OnItsOwnBranch, decorators: [dark] };

/**
 * A detached tree holding commits beyond the line (REVIEWBASE4): *detached HEAD* in the branch's place, and the range apart, since
 * HEAD is past the commit the review is measured from.
 */
export const DetachedWithWork: Story = {
  render: () => <Loaded branch={null} detached behind={{ line: 'main', commits: 0, ahead: 4 }} />,
};

/** Detached with work, in dark. */
export const DetachedWithWorkDark: Story = { ...DetachedWithWork, decorators: [dark] };

/** Detached with work, in 中文. */
export const DetachedWithWorkChinese: Story = { ...DetachedWithWork, decorators: [chinese] };
