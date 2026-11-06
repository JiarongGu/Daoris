import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { useState } from 'react';
import { GIT_CHANGES_ENGINE, GIT_COMMIT_PLAN_ENGINE, GIT_CONFLICT_CACHE, GIT_HUNKS_BUDGET, GIT_STASH_PLAN_ENGINE, GIT_SWITCH_PLAN_ENGINE } from '../fixtures';
import { StashBox, type StashPlanValue } from './StashBox';
import { SwitchBox, type SwitchPlanValue } from './SwitchBox';
import { ChangeHunks } from './ChangeHunks';
import { ConflictHunks } from './ConflictHunks';
import { CommitBox, type CommitPlanValue } from './CommitBox';
import { type ChangesActs, ChangesTab } from './RepositoryGit';

// A repository's Changes tab (GITAPP2b, D204, the git-writes design §1, §4, §11), drawn where the person let Daoris change the
// checkout's files: what is staged against the last commit, what the files hold beyond it (a file staged and then removed is in
// both), what is untracked and what a merge left in conflict, each with its mode and blob, and when it was read. At the main
// area's three widths (80, 39 and 25rem) in English and 中文, in both themes; and its states: on its way, no checkout here,
// refused, none, and read again.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

// GITAPP2i: and the commit box (the message, Amend the last commit, the hooks it runs, who commits), whose plan a story may change.
const commitBox = (over: Partial<CommitPlanValue> = {}, amended = false): NonNullable<ChangesActs['Commit']> => function Commit({ busy }) {
  const [amend, setAmend] = useState(amended);
  return <CommitBox read={{ state: 'read', value: { ...GIT_COMMIT_PLAN_ENGINE, ...over } }} amend={amend} onAmend={setAmend} busy={busy} onPress={nothing} />;
};

// GITAPP2k: and the switch box (a branch chosen, its plan) and the stash box (what a Save would take, the stash shared with every linked tree).
const switchBox = (chosen = '', over: Partial<SwitchPlanValue> = {}): NonNullable<ChangesActs['Switch']> => function Switch({ busy }) {
  const [now, setNow] = useState(chosen);
  return (
    <SwitchBox
      branches={['main', 'release', 'physics']} chosen={now} onChoose={setNow} busy={busy} onPress={nothing}
      read={now === '' ? null : { state: 'read', value: { ...GIT_SWITCH_PLAN_ENGINE, to: now, ...over } }}
    />
  );
};
const stashBox = (over: Partial<StashPlanValue> = {}): NonNullable<ChangesActs['Stash']> => function Stash({ busy }) {
  return <StashBox read={{ state: 'read', value: { ...GIT_STASH_PLAN_ENGINE, ...over } }} busy={busy} onPress={nothing} onApply={nothing} onDrop={nothing} />;
};

// GITAPP2h: every story below carries the presses (Stage, Unstage, Show changes) a tab has where the switch is on.
const acts = (over: Partial<ChangesActs> = {}): ChangesActs => ({
  busy: false,
  last: null,
  press: nothing,
  undo: nothing,
  Hunks: ({ side, busy, onPress }) => (
    <ChangeHunks read={{ state: 'read', value: GIT_HUNKS_BUDGET }} verb={side === 'unstaged' ? 'stage' : 'unstage'} busy={busy} onPress={onPress} />
  ),
  Commit: commitBox(),
  // GITAPP2j: and the conflicted file's conflicts, with Mark resolved beside them.
  resolve: nothing,
  mark: nothing,
  Conflict: ({ busy, onPress }) => <ConflictHunks read={{ state: 'read', value: GIT_CONFLICT_CACHE }} busy={busy} onPress={onPress} />,
  Switch: switchBox('release'),
  Stash: stashBox(),
  ...over,
});

const meta: Meta<typeof ChangesTab> = {
  title: 'Repositories/ChangesTab',
  component: ChangesTab,
  args: { repository: 'engine', read: { state: 'read', value: GIT_CHANGES_ENGINE }, onRefresh: nothing, acts: acts() },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ChangesTab>;

/** At 80rem: the four lists, `notes.txt` in Staged as an addition and in Not staged as removed from disk. */
export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const WideChineseDark: Story = { decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { decorators: [storyWidths.half] };
export const HalfChinese: Story = { decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: paths wrap anywhere, and Refresh keeps its place beside the time. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** Opens the first row's changes, as a person would. */
const openChanges: Story['play'] = async ({ canvasElement }) => {
  canvasElement.querySelector<HTMLButtonElement>('li button[aria-expanded]')?.click();
};

/** A file's hunks open under its row, each line of change with its box, a hunk and the lines chosen pressed apart. */
export const HunksOpen: Story = { play: openChanges };
export const HunksOpenChinese: Story = { decorators: [chinese], play: openChanges };
export const HunksOpenDark: Story = { decorators: [dark], play: openChanges };
export const HunksOpenHalf: Story = { decorators: [storyWidths.half], play: openChanges };
export const HunksOpenHalfChinese: Story = { decorators: [storyWidths.half, chinese], play: openChanges };
export const HunksOpenNarrow: Story = { decorators: [storyWidths.narrow], play: openChanges };
export const HunksOpenNarrowChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark], play: openChanges };

/** Opens the conflicted file's conflicts and takes ours for all, as a person would (GITAPP2j). */
const openConflicts: Story['play'] = async ({ canvasElement }) => {
  const rows = [...canvasElement.querySelectorAll<HTMLButtonElement>('li button[aria-expanded]')];
  rows[rows.length - 1]?.click();
  await new Promise((resolve) => setTimeout(resolve, 0));
  const everywhere = [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((button) => /Ours everywhere|全部取我方/.test(button.textContent ?? ''));
  everywhere?.click();
};

/** The conflicted file's conflicts under its row: ours and theirs for each, a take chosen, the write held until every one has a take. */
export const ConflictsOpen: Story = { play: openConflicts };
export const ConflictsOpenChinese: Story = { decorators: [chinese], play: openConflicts };
export const ConflictsOpenDark: Story = { decorators: [dark], play: openConflicts };
export const ConflictsOpenChineseDark: Story = { decorators: [chinese, dark], play: openConflicts };
export const ConflictsOpenHalf: Story = { decorators: [storyWidths.half], play: openConflicts };
export const ConflictsOpenHalfChinese: Story = { decorators: [storyWidths.half, chinese], play: openConflicts };
export const ConflictsOpenHalfDark: Story = { decorators: [storyWidths.half, dark], play: openConflicts };
export const ConflictsOpenHalfChineseDark: Story = { decorators: [storyWidths.half, chinese, dark], play: openConflicts };
export const ConflictsOpenNarrow: Story = { decorators: [storyWidths.narrow], play: openConflicts };
export const ConflictsOpenNarrowChinese: Story = { decorators: [storyWidths.narrow, chinese], play: openConflicts };
export const ConflictsOpenNarrowDark: Story = { decorators: [storyWidths.narrow, dark], play: openConflicts };
export const ConflictsOpenNarrowChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark], play: openConflicts };

/** A file the driver cannot take by hunk says why where its conflicts would be. */
const noHunks = (why: string): ChangesActs['Conflict'] => ({ busy, onPress }) => (
  <ConflictHunks read={{ state: 'read', value: { ...GIT_CONFLICT_CACHE, why, hunks: [] } }} busy={busy} onPress={onPress} />
);
const openRow: Story['play'] = async ({ canvasElement }) => {
  const rows = [...canvasElement.querySelectorAll<HTMLButtonElement>('li button[aria-expanded]')];
  rows[rows.length - 1]?.click();
};
export const ConflictNotByHunk: Story = {
  args: { acts: acts({ Conflict: noHunks('`src/cache.cs` is in conflict with a side that has no file or no common ancestor, which is resolved by keeping or removing it, not hunk by hunk.') }) },
  play: openRow,
};
export const ConflictNotByHunkNarrowChinese: Story = { ...ConflictNotByHunk, decorators: [storyWidths.narrow, chinese] };

/** A resolve written, with *Undo* beside it; and a mark. */
export const Resolved: Story = { args: { acts: acts({ last: { verb: 'resolve', path: 'src/cache.cs', act: 'a4', done: true, said: null } }) } };
export const ResolvedChinese: Story = { ...Resolved, decorators: [chinese] };
export const ResolvedNarrowDark: Story = { ...Resolved, decorators: [storyWidths.narrow, dark] };
export const Marked: Story = { args: { acts: acts({ last: { verb: 'mark', path: 'src/cache.cs', act: 'a5', done: true, said: null } }) } };
export const MarkedNarrowChinese: Story = { ...Marked, decorators: [storyWidths.narrow, chinese] };

/** A resolve refused because the file's bytes moved since they were shown. */
export const ResolveRefused: Story = {
  args: { acts: acts({ last: { verb: 'resolve', path: 'src/cache.cs', act: null, done: false, said: '`src/cache.cs`\'s bytes changed since they were read: nothing was changed.' } }) },
};
export const ResolveRefusedNarrowChinese: Story = { ...ResolveRefused, decorators: [storyWidths.narrow, chinese] };

/** The last act done: said, with *Undo* beside it. */
export const Staged: Story = { args: { acts: acts({ last: { verb: 'stage', path: 'src/budget.cs', act: 'a1', done: true, said: null } }) } };
export const StagedChinese: Story = { ...Staged, decorators: [chinese] };
export const StagedNarrowDark: Story = { ...Staged, decorators: [storyWidths.narrow, dark] };

/** A refusal, in the driver's sentence. */
export const Refusal: Story = {
  args: {
    acts: acts({
      last: {
        verb: 'stage', path: 'src/budget.cs', act: null, done: false,
        said: 'Session `s-3f2a` works in `engine`\'s checkout (Repositories → the repository\'s page), so this act is refused; try again once it ends: nothing was changed.',
      },
    }),
  },
};
export const RefusalNarrowChinese: Story = { ...Refusal, decorators: [storyWidths.narrow, chinese] };

/** A commit done, with what a pre-commit hook changed in it said beside it. */
export const Committed: Story = {
  args: {
    acts: acts({
      last: { verb: 'commit', path: null, act: 'a2', done: true, said: null, notes: 'A hook changed what was committed: `src/budget.cs` differ from what was staged.' },
    }),
  },
};
export const CommittedChinese: Story = { ...Committed, decorators: [chinese] };
export const CommittedNarrowDark: Story = { ...Committed, decorators: [storyWidths.narrow, dark] };

/** A hook refused: git's words and the hook's, and nothing committed. */
export const HookRefused: Story = {
  args: { acts: acts({ last: { verb: 'commit', path: null, act: 'a3', done: false, said: null, notes: 'Git refused the commit (lint failed: src/budget.cs line 12).' } }) },
};
export const HookRefusedNarrowChinese: Story = { ...HookRefused, decorators: [storyWidths.narrow, chinese] };

/** Nothing staged: Commit is held and says why; an amend of the last commit starts from its message. */
export const NothingStaged: Story = { args: { acts: acts({ Commit: commitBox({ empty: true }) }) } };
export const NothingStagedNarrowChinese: Story = { ...NothingStaged, decorators: [storyWidths.narrow, chinese] };
export const Amending: Story = { args: { acts: acts({ Commit: commitBox({}, true) }) } };
export const AmendingHalfDark: Story = { ...Amending, decorators: [storyWidths.half, dark] };
export const NoHooksNoIdentity: Story = { args: { acts: acts({ Commit: commitBox({ hooks: [], identity: null }) }) } };
export const ConflictHeld: Story = {
  args: { acts: acts({ Commit: commitBox({ tree: null, why: 'a file in conflict stands in the index, so it cannot be committed' }) }) },
};

/** An act running: every press waits. */
export const Working: Story = { args: { acts: acts({ busy: true }) } };

/** The held read shown while a fresh one is on its way: the time of the read stays, it says it reads again, and Refresh waits. */
export const ReadingAgain: Story = { args: { refreshing: true } };

/** The three observations agree. */
export const None: Story = { args: { read: { state: 'read', value: { ...GIT_CHANGES_ENGINE, staged: [], unstaged: [], untracked: [], unmerged: [] } } } };

/** The first read on its way. */
export const Reading: Story = { args: { read: { state: 'reading' } } };

/** A repository registered here by a teammate, with no checkout on this machine. */
export const NoCheckout: Story = { args: { read: { state: 'noCheckout' } } };

/** Git did not answer: the driver's sentence in the tab's place. */
export const Refused: Story = {
  args: { read: { state: 'refused', sentence: 'git could not read this checkout\'s changes: it may be busy, or git did not start.' } },
};

// GITAPP2k: a switch done (with the way back), one the plan holds for a changed tracked file, and a stash Save done / holding to untracked files only.
export const SwitchDone: Story = {
  args: { acts: acts({ last: { verb: 'switch', path: 'release', act: 'a2', done: true, said: null, notes: 'Switch back to `main` to undo it.' } }) },
};
export const SwitchDoneChinese: Story = { ...SwitchDone, decorators: [chinese] };
export const SwitchDoneNarrowDark: Story = { ...SwitchDone, decorators: [storyWidths.narrow, dark] };

export const SwitchHeld: Story = {
  args: { acts: acts({ Switch: switchBox('release', { dirty: true, why: 'tracked files are changed here; commit or stash them first, since a switch never carries them' }) }) },
};
export const SwitchHeldNarrowChinese: Story = { ...SwitchHeld, decorators: [storyWidths.narrow, chinese] };
export const SwitchHeldHalfDark: Story = { ...SwitchHeld, decorators: [storyWidths.half, dark] };

export const Stashed: Story = {
  args: { acts: acts({ last: { verb: 'stash', path: null, act: 'a3', done: true, said: null, notes: 'Saved as stash `3fa91c0e2b7d` in `refs/stash`, which every linked tree shares; `git stash apply 3fa91c0e2b7d4a18` puts it back.' } }) },
};
export const StashedChinese: Story = { ...Stashed, decorators: [chinese] };
export const StashedNarrowDark: Story = { ...Stashed, decorators: [storyWidths.narrow, dark] };

export const StashUntrackedOnly: Story = { args: { acts: acts({ Stash: stashBox({ tracked: 0, untracked: 3 }) }) } };
export const StashUntrackedOnlyNarrowChinese: Story = { ...StashUntrackedOnly, decorators: [storyWidths.narrow, chinese] };
export const StashUntrackedOnlyHalfDark: Story = { ...StashUntrackedOnly, decorators: [storyWidths.half, dark] };

// GITAPP2l: the saved stashes, each applied by its commit id; one has two untracked files in the way.
export const StashApply: Story = { args: { acts: acts({ Stash: stashBox() }) } };
export const StashApplyNarrowChinese: Story = { ...StashApply, decorators: [storyWidths.narrow, chinese] };
export const StashApplyHalfDark: Story = { ...StashApply, decorators: [storyWidths.half, dark] };
export const StashApplyHeld: Story = { args: { acts: acts({ Stash: stashBox({ applyWhy: 'tracked files are changed here, so save or discard them first' }) }) } };
export const StashApplyHeldNarrowDark: Story = { ...StashApplyHeld, decorators: [storyWidths.narrow, dark] };
export const StashApplyHeldHalfChinese: Story = { ...StashApplyHeld, decorators: [storyWidths.half, chinese] };
export const StashApplied: Story = {
  args: { acts: acts({ last: { verb: 'stashApply', path: null, act: 'a4', done: true, said: null, notes: 'Some files are in conflict: resolve them in the Changes tab. The stash is kept in `refs/stash` (`3fa91c0e2b7d`).' } }) },
};
export const StashAppliedNarrowChinese: Story = { ...StashApplied, decorators: [storyWidths.narrow, chinese] };
export const StashAppliedHalfDark: Story = { ...StashApplied, decorators: [storyWidths.half, dark] };
