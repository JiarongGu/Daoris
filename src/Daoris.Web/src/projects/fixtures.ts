import type { Registration, Repository } from '../data/api';
import type { RepositoryLine } from '../settings/repository/Lines';
import type { GraphCommitRow } from './git/CommitGraph';
import type { RepositorySetupProps } from './setup/RepositorySetup';

// Repositories as the registry and the index answer them, for the list's and the page's stories: the
// example family's two adopters, a teammate's registration with no checkout here, one that declared nothing, two not
// adopted (one with a root here, one without), and a name in 中文. The engine's branches and its line's history
// as Git's reads answer them.

export const ENGINE: Registration = {
  repository: 'engine', adopted: true, registered: true, workspace: 'default', root: 'C:/somewhere/engine',
  summary: 'The example engine: the runtime half of the example family — simulation, rendering, and the asset pipeline the game builds on.',
  owns: ['the engine runtime — simulation, rendering, assets', "the engine's public API surface"],
  accepts: ['a feature the game needs from the engine, with the failing case', 'an API gap, with the call site that hit it'],
  packs: ['dotnet'], entries: 42,
};

export const GAME: Registration = {
  repository: 'game', adopted: true, registered: true, workspace: 'default', root: 'C:/somewhere/game',
  summary: 'The example game: the content half of the example family — world design, gameplay, and everything a player touches.',
  owns: ['the game — world design, gameplay, content'],
  accepts: ['a playtest finding, with the reproduction', 'a content request, with its design intent'],
  packs: [], entries: 18,
};

/** A teammate's registration, mirrored from a remote: no checkout on this machine. */
export const MIRRORED: Registration = {
  repository: 'studio-tools', adopted: true, registered: true, workspace: 'studio',
  summary: 'Build and release tooling the studio shares.', owns: ['the build'], accepts: ['a broken pipeline'], packs: [], entries: 6,
};

/** Adopted, and declared nothing: addressable, and an asker would be guessing. */
export const UNDECLARED: Registration = {
  repository: 'sandbox', adopted: true, registered: true, workspace: 'default', root: 'C:/somewhere/sandbox',
  owns: [], accepts: [], packs: [], entries: 0,
};

/** Not adopted, with a root here: drivable over the protocol door. */
export const NEWBIE: Registration = {
  repository: 'newbie', adopted: false, registered: true, addressable: true, workspace: 'default', root: 'C:/somewhere/newbie',
  owns: [], accepts: [], packs: [], entries: 3,
};

/** Not adopted, with no root here: nowhere to start it. */
export const ELSEWHERE: Registration = {
  repository: 'elsewhere', adopted: false, registered: true, addressable: false, workspace: 'default',
  owns: [], accepts: [], packs: [], entries: 0,
};

/** A name in 中文: the repository's own, content, never translated. */
export const CJK: Registration = {
  repository: '渲染管线', adopted: true, registered: true, workspace: 'default', root: 'C:/somewhere/渲染管线',
  summary: '渲染管线：着色器、材质与后期处理，供游戏与编辑器共用。', owns: ['着色器', '材质系统'], accepts: ['一个渲染错误，附截图'],
  packs: [], entries: 12,
};

export const COUNTS: Record<string, Repository> = {
  engine: {
    name: 'engine', total: 42, local: 30, canonical: 12, workspace: 'default',
    fed: {
      commit: 'c0ffee1234567890', shortCommit: 'c0ffee12', committedAt: new Date(Date.now() - 3 * 3600_000).toISOString(),
      branch: 'main', origin: 'person@machine-a',
    },
  },
  game: { name: 'game', total: 18, local: 18, canonical: 0, workspace: 'default' },
  'studio-tools': { name: 'studio-tools', total: 6, local: 6, canonical: 0, workspace: 'studio' },
  newbie: { name: 'newbie', total: 3, local: 3, canonical: 0, workspace: 'default' },
  渲染管线: { name: '渲染管线', total: 12, local: 12, canonical: 0, workspace: 'default' },
};

/** The engine's line, set for the repository itself. */
export const LINE: RepositoryLine = { repository: 'engine', workspace: 'default', branch: 'develop', source: 'repository' };

const nothing = () => {};

/** The driver's table of session languages, as it answers it. */
export const LANGUAGES = [{ code: 'en', name: 'English' }, { code: 'zh', name: 'Simplified Chinese (简体中文)' }];

/**
 * The engine's Setup as Daoris decides it: driven here with a tree per session, its line its workspace's, its work
 * merged into its line, no session language, no standing answer, read by agents outside it, writing into nothing else
 * and no rule of its own. Every section folds.
 */
export const SETUP_DEFAULTS: RepositorySetupProps = {
  repository: 'engine',
  driving: { drivable: true, held: false, ownTree: true, onDrive: nothing, onHold: nothing },
  work: {
    line: { repository: 'engine', workspace: 'default', branch: 'main', source: 'workspace' },
    onLine: nothing,
    onLinePolicy: nothing,
  },
  sessions: {
    language: { resolved: null, table: LANGUAGES, onSet: nothing },
    standing: null,
    onStanding: nothing,
  },
  reach: {
    across: { repository: 'engine', workspace: 'default', checkout: true, read: true, source: 'default', writesTo: [] },
    readAbove: { read: true, source: 'default' },
    candidates: ['game', 'sandbox'],
    onRead: nothing,
    onWrite: nothing,
    rules: { allow: [], ask: [], deny: [] },
    onAddRule: nothing,
    onRemoveRule: nothing,
  },
};

/**
 * The engine's Setup with the person's own values: its own line, work put on a branch a plugin pushes and accepts
 * automatically, its own session language, a standing answer, read by nobody outside it, writing into the game, and two
 * rules of its own. Line and landing, Sessions and Reach open; Driving folds.
 */
export const SETUP_OWN: RepositorySetupProps = {
  ...SETUP_DEFAULTS,
  work: {
    ...SETUP_DEFAULTS.work,
    line: { repository: 'engine', workspace: 'default', branch: 'develop', source: 'repository' },
  },
  sessions: {
    language: {
      resolved: { repository: 'engine', workspace: 'default', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'repository' },
      table: LANGUAGES,
      onSet: nothing,
    },
    standing: {
      says: 'dev writes allowed; test locally against dev\nprod only on a yes\nnever push to main',
      at: new Date(Date.now() - 26 * 3_600_000).toISOString(),
    },
    onStanding: nothing,
  },
  reach: {
    ...SETUP_DEFAULTS.reach,
    across: { repository: 'engine', workspace: 'default', checkout: true, read: false, source: 'repository', writesTo: ['game'] },
    candidates: ['sandbox'],
    rules: { allow: ['Bash(npm run test:*)'], ask: [], deny: ['Bash(git push:*)'] },
  },
};

/**
 * The engine's Setup with its process as a shell answers it: how its work is committed
 * at Daoris's default, on *Line and landing*, and what a helper its agent asks for does from its workspace, on *Sessions*.
 */
export const SETUP_PROCESS: RepositorySetupProps = {
  ...SETUP_DEFAULTS,
  work: {
    ...SETUP_DEFAULTS.work,
    commits: { repository: 'engine', workspace: 'default', rule: { mode: 'as-you-go' }, source: 'default' },
    onCommits: nothing,
  },
  sessions: {
    ...SETUP_DEFAULTS.sessions,
    helpers: { repository: 'engine', workspace: 'default', rule: { mode: 'on', agents: ['codex-acp', 'dsh'] }, source: 'workspace' },
    onHelpers: nothing,
  },
};

/**
 * The engine's process set for it: branches named by a ticket its key finds and squashed to one commit, each commit
 * waiting for the person's yes under their convention, and no helper offered. Line and landing and Sessions open.
 */
export const SETUP_PROCESS_OWN: RepositorySetupProps = {
  ...SETUP_PROCESS,
  work: {
    ...SETUP_PROCESS.work,
    commits: {
      repository: 'engine', workspace: 'default', source: 'repository',
      rule: { mode: 'ask', convention: 'Imperative subject line under 72 characters, the ticket key first; say why in the body.' },
    },
  },
  sessions: {
    ...SETUP_PROCESS.sessions,
    helpers: { repository: 'engine', workspace: 'default', rule: { mode: 'off', agents: [] }, source: 'repository' },
  },
};

/** A whole commit id from a short one, as git answers them: forty hex digits. */
const id = (short: string) => short.padEnd(40, '0');
const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();

/** What names a session's branch, as `GIT_BRANCHES` reads it: the newest session record on its tree. */
const sessionOf = (session: string, state: string, quest: string | null) =>
  ({ session: session as string | null, state: state as string | null, quest, grewFrom: null as string | null, from: null as string | null });

/** What a plugin last answered about a landed branch's pull request, asked some hours ago. */
const pullRequestOf = (state: string, asked: number, done: number | null = null) => ({
  state, pullRequest: 'https://code.example.invalid/engine/pull/12' as string | null, mergeCommit: null as string | null,
  sourceCommit: null as string | null, target: 'main' as string | null, how: null as string | null,
  at: done === null ? null : hoursAgo(done), message: null as string | null, plugin: 'azure' as string | null, askedAt: hoursAgo(asked),
});

/** A landed branch's record and how it stands to its own name on origin, one of the driver's `GitOrigin`. */
const landedOf = (origin: string, { pushed = true, pullRequest = null, ahead = null, behind = null, state = null, failed = null }: {
  pushed?: boolean;
  pullRequest?: string | null;
  ahead?: number | null;
  behind?: number | null;
  state?: ReturnType<typeof pullRequestOf> | null;
  failed?: { code: string; plugin: string; at: string } | null;
} = {}) => ({
  session: 's-21', landedAt: hoursAgo(30), origin, quest: null as string | null, title: null as string | null, plugin: 'azure' as string | null,
  pushed, pullRequest, pushedTip: null as string | null, originCommit: null as string | null, originAhead: ahead, originBehind: behind,
  pullRequestState: state, pullRequestAskFailed: failed,
});

/** A branch as `GIT_BRANCHES` lists it: its kind, and its last commit some hours ago. */
const branchOf = (name: string, kind: string, short: string, hours: number, subject: string, {
  ahead = null, behind = null, tree = null, session = null, landed = null,
}: {
  ahead?: number | null;
  behind?: number | null;
  tree?: string | null;
  session?: ReturnType<typeof sessionOf> | null;
  landed?: ReturnType<typeof landedOf> | null;
} = {}) => ({ name, kind, commit: id(short), at: hoursAgo(hours) as string | null, subject, ahead, behind, tree, session, landed });

/**
 * The engine's branches as `GIT_BRANCHES` answers them for its repository, in the driver's order (the line, then each
 * kind newest first): its line, two sessions' (one no record names), three landed (in step with an open pull request, gone
 * from origin after it merged, never pushed), seven of the person's and one only origin holds. Its shape is the bridge's,
 * which `ProjectsView.git.test.tsx` holds it to; a fixture beside the molecules imports none.
 */
export const GIT_BRANCHES_ENGINE = {
  repository: 'engine', workspace: 'default',
  line: {
    branch: 'main' as string | null, source: 'workspace', commit: id('c4f1a2') as string | null, origin: id('c4f1a2') as string | null,
    ahead: 0 as number | null, behind: 0 as number | null,
  },
  fetch: { at: hoursAgo(3), heard: true } as { at: string; heard: boolean } | null,
  holds: true, missing: [], commands: ['git for-each-ref refs/heads refs/remotes'], problem: null,
  branches: [
    branchOf('main', 'line', 'c4f1a2', 2, 'Merge the asset cache'),
    branchOf('daoris/engine-physics-step', 'session', '9be03d', 1, 'Fix the physics step on a slow frame', {
      ahead: 2, behind: 2, tree: 'engine-physics-step', session: sessionOf('s-41', 'working', 'q7'),
    }),
    branchOf('daoris/engine-audio-mix', 'session', '61c0aa', 26, 'Mix the music under the effects', { ahead: 3, behind: 0 }),
    branchOf('review/q6-asset-cache', 'landed', 'a19c5b', 5, 'Cache assets by their content hash', {
      landed: landedOf('in-step', { pullRequest: 'https://code.example.invalid/engine/pull/12', state: pullRequestOf('open', 2) }),
    }),
    branchOf('review/q4-input-map', 'landed', '2f7d10', 50, "Map the gamepad's inputs", {
      landed: landedOf('gone', { pullRequest: 'https://code.example.invalid/engine/pull/9', state: pullRequestOf('completed', 29, 30) }),
    }),
    branchOf('review/q2-shader-cache', 'landed', '0e4b9c', 90, 'Warm the shader cache', { landed: landedOf('not-pushed', { pushed: false }) }),
    branchOf('spike/renderer', 'yours', '5d2e77', 80, 'Try a forward renderer', { ahead: 4, behind: 9 }),
    branchOf('try/ecs', 'yours', '7a1e02', 100, 'Try an entity system', { ahead: 6, behind: 12 }),
    branchOf('notes/frame-budget', 'yours', '33d9e1', 120, 'Note the frame budget', { ahead: 1, behind: 14 }),
    branchOf('spike/vulkan', 'yours', 'b80c4f', 150, 'Draw a triangle on Vulkan', { ahead: 9, behind: 20 }),
    branchOf('fix/readme', 'yours', '4c2a90', 200, 'Fix the readme', { ahead: 0, behind: 22 }),
    branchOf('experiment/jobs', 'yours', 'e51f37', 300, 'Run the physics on jobs', { ahead: 5, behind: 30 }),
    branchOf('old/menu', 'yours', '90ad6e', 400, 'Draw the old menu', { ahead: 2, behind: 41 }),
    branchOf('origin/release', 'origin', 'd04c11', 10, 'Cut the release'),
  ],
};

/**
 * The game's branches as `GIT_BRANCHES` answers them for it alone: a checkout holding no branch of Daoris's, so a
 * workspace's read lists it apart and its tab draws it once the person includes it. Its line, set for the repository, is
 * behind origin's copy and last fetched days ago; then two of the person's.
 */
export const GIT_BRANCHES_GAME = {
  repository: 'game', workspace: 'default',
  line: {
    branch: 'develop' as string | null, source: 'repository', commit: id('6b20fd') as string | null, origin: id('e3a7c1') as string | null,
    ahead: 0 as number | null, behind: 2 as number | null,
  },
  fetch: { at: hoursAgo(80), heard: true } as { at: string; heard: boolean } | null,
  holds: false, missing: [], commands: ['git for-each-ref refs/heads refs/remotes'], problem: null,
  branches: [
    branchOf('develop', 'line', '6b20fd', 72, 'Tune the jump arc'),
    branchOf('level/forest', 'yours', '1c5e88', 30, 'Block out the forest level', { ahead: 3, behind: 0 }),
    branchOf('try/coyote-time', 'yours', 'f02b9a', 200, 'Try coyote time on ledges', { ahead: 1, behind: 6 }),
  ],
};

/** The engine's line's history as `GIT_GRAPH` answers its first page: a branch merged in, beside the line, to its root. */
export const GIT_HISTORY_ENGINE: LoggedCommit[] = [
  logged('c4f1a2', ['77a0e1', 'a19c5b'], 'Merge the asset cache', 'Mei Lin', 2),
  logged('a19c5b', ['3e88f0'], 'Cache assets by their content hash', 'Mei Lin', 5),
  logged('77a0e1', ['3e88f0'], 'Name the frame budget in the settings', 'Sam Ortiz', 7),
  logged('3e88f0', ['1b07c9'], 'Load assets on a worker', 'Sam Ortiz', 30),
  logged('1b07c9', [], 'Begin the engine', 'Mei Lin', 400),
];

/** The same history as two pages of `GIT_GRAPH`: the first answers `more`, and its tail's parents are the second's. */
export const GIT_HISTORY_PAGES: [LoggedCommit[], LoggedCommit[]] = [GIT_HISTORY_ENGINE.slice(0, 3), GIT_HISTORY_ENGINE.slice(3)];

const ZEROS = '0000000000000000000000000000000000000000';
const blob = (short: string) => `${short}${'a'.repeat(40 - short.length)}`;

/**
 * The engine's checkout as `GIT_CHANGES` reads it: a modified file staged, `notes.txt` staged as an addition and then
 * removed from disk (in both lists), a file changed after staging, one untracked, and one in conflict after a merge.
 */
export const GIT_CHANGES_ENGINE = {
  head: id('c4f1a2') as string | null,
  staged: [
    { path: 'src/budget.cs', status: 'M', oldMode: '100644', newMode: '100644', oldBlob: blob('3b9d10'), newBlob: blob('9c41e7') },
    { path: 'notes.txt', status: 'A', oldMode: '000000', newMode: '100644', oldBlob: ZEROS, newBlob: blob('5e02f1') },
  ],
  unstaged: [
    { path: 'notes.txt', status: 'D', oldMode: '100644', newMode: '000000', oldBlob: blob('5e02f1'), newBlob: ZEROS },
    { path: 'src/budget.cs', status: 'M', oldMode: '100644', newMode: '100644', oldBlob: blob('9c41e7'), newBlob: ZEROS },
  ],
  untracked: ['scratch/trace.log'],
  unmerged: [
    { path: 'src/cache.cs', code: 'UU', modes: ['100644', '100644', '100644'], blobs: [blob('1a2b3c'), blob('4d5e6f'), blob('7a8b9c')] },
  ],
  readAt: hoursAgo(0.05),
};

/**
 * `src/budget.cs`'s hunks on the index → worktree side, as `GIT_HUNKS` answers them: one hunk of a changed line and an
 * added one, with the ids a press sends back (`index` is the entry the list shows for it).
 */
export const GIT_HUNKS_BUDGET = {
  path: 'src/budget.cs',
  side: 'unstaged' as 'unstaged' | 'staged',
  head: id('c4f1a2') as string | null,
  index: `100644 ${blob('9c41e7')}`,
  source: blob('d70a41'),
  binary: false,
  hunks: [
    {
      id: 'a1b2c3d4e5f6',
      header: '@@ -10,4 +10,5 @@ class Budget',
      lines: [
        { kind: ' ', text: '    var total = 0;' },
        { kind: '-', text: '    total += cost;' },
        { kind: '+', text: '    total += cost * rate;' },
        { kind: '+', text: '    total = Math.Round(total);' },
        { kind: ' ', text: '    return total;' },
      ],
    },
  ],
};

/**
 * `src/cache.cs`'s conflicts, as `GIT_CONFLICT` answers them: two, the first with a line each side changed and the second where
 * ours holds a line theirs removed, with the stages and the worktree id a press sends back.
 */
export const GIT_CONFLICT_CACHE = {
  path: 'src/cache.cs',
  head: id('c4f1a2') as string | null,
  base: `100644 ${blob('1a2b3c')}`,
  ours: `100644 ${blob('4d5e6f')}`,
  theirs: `100644 ${blob('7a8b9c')}`,
  worktree: blob('e2d4a8'),
  why: null as string | null,
  hunks: [
    {
      id: '3f9a1c7b2d40',
      before: ['class Cache', '{'],
      ours: ['    private const int Size = 64;'],
      theirs: ['    private const int Size = 128;'],
      after: ['    private readonly Dictionary<string, Entry> _items = new();'],
    },
    {
      id: '8c2e5d9a4b17',
      before: ['    public void Clear()'],
      ours: ['    {', '        _items.Clear();', '        Hits = 0;'],
      theirs: [],
      after: ['    }'],
    },
  ],
};

/**
 * What a commit of the engine's staged changes rests on, as `GIT_COMMIT_PLAN` answers it: the tip and the index as a tree,
 * HEAD's message for an amend, the hooks its git runs and who commits.
 */
export const GIT_COMMIT_PLAN_ENGINE = {
  head: id('c4f1a2') as string | null,
  branch: 'refs/heads/main' as string | null,
  tree: id('7e3b90') as string | null,
  why: null as string | null,
  empty: false,
  message: 'Round the budget total\n' as string | null,
  hooks: ['pre-commit', 'commit-msg'],
  identity: 'Aurora Dev <aurora@example.com>' as string | null,
};

/** `GIT_SWITCH_PLAN` for the engine's `release` branch from `main`: nothing changed, a post-checkout hook. */
export const GIT_SWITCH_PLAN_ENGINE = {
  from: 'main' as string | null,
  head: id('c4f1a2') as string | null,
  to: 'release',
  dirty: false,
  elsewhere: null as string | null,
  why: null as string | null,
  hooks: ['post-checkout'],
};

/** `GIT_STASH_PLAN` for the engine: two tracked files changed, one untracked, one session tree sharing the stash. */
export const GIT_STASH_PLAN_ENGINE = {
  head: id('c4f1a2') as string | null,
  stash: null as string | null,
  tracked: 2,
  untracked: 1,
  sharedWith: 1,
  why: null as string | null,
  hooks: [] as string[],
  entries: [
    { id: '3fa91c0e2b7d4a18c6e5d0b9a7f1e2c3d4b5a697', message: 'On release: before the switch', untracked: true, collides: 0, mine: true },
    { id: '9bd20e51a7c3f4086d1e2b3c4a5f6978e0d1c2b3', message: '', untracked: false, collides: 2, mine: false },
  ] as { id: string; message: string; untracked: boolean; collides: number; mine: boolean }[],
  applyWhy: null as string | null,
};

/**
 * The physics session's branch beside the engine's line since they parted, as `GIT_GRAPH` answers `sinceParted` with the two
 * tips, the branch then the line: its two commits (`left`) and the line's two since it grew from `77a0e1` (`right`),
 * the asset cache's merge among them, in date order.
 */
export const GIT_BRANCH_PARTED: LoggedCommit[] = [
  { ...logged('9be03d', ['4d1e7b'], 'Fix the physics step on a slow frame', 'Sam Ortiz', 1), side: 'left' },
  { ...logged('c4f1a2', ['77a0e1', 'a19c5b'], 'Merge the asset cache', 'Mei Lin', 2), side: 'right' },
  { ...logged('4d1e7b', ['77a0e1'], 'Step the physics at a fixed rate', 'Sam Ortiz', 4), side: 'left' },
  { ...logged('a19c5b', ['3e88f0'], 'Cache assets by their content hash', 'Mei Lin', 5), side: 'right' },
];

/** The same walk as two pages: the first answers `more`. */
export const GIT_BRANCH_PARTED_PAGES: [LoggedCommit[], LoggedCommit[]] = [GIT_BRANCH_PARTED.slice(0, 2), GIT_BRANCH_PARTED.slice(2)];

/** A commit as `GIT_GRAPH` lists it: what the graph draws, with the author's address and the side of a two-sided walk. */
type LoggedCommit = GraphCommitRow & { parents: string[]; authorEmail: string; side: string | null };

/** A commit as a graph's page lists it, by short ids made whole, written by an author some hours ago. */
function logged(commit: string, parents: string[], subject: string, author: string, hours: number): LoggedCommit {
  const at = hoursAgo(hours);
  return {
    commit: id(commit), parents: parents.map(id), author, authorEmail: `${author.split(' ')[0]!.toLowerCase()}@example.invalid`,
    subject, authoredAt: at, committedAt: at, side: null,
  };
}
