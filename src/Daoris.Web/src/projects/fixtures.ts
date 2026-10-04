import type { Registration, Repository } from '../api';
import type { RepositoryLine } from '../settings/Lines';
import type { RepositorySetupProps } from './RepositorySetup';

// Repositories as the registry and the index answer them (FRAME1e), for the list's and the page's stories: the
// example family's two adopters, a teammate's registration with no checkout here, one that declared nothing, two not
// adopted (one with a root here, one without), and a name in 中文.

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

/** A teammate's registration, mirrored from a remote: no checkout on this machine (D47 §9). */
export const MIRRORED: Registration = {
  repository: 'studio-tools', adopted: true, registered: true, workspace: 'studio',
  summary: 'Build and release tooling the studio shares.', owns: ['the build'], accepts: ['a broken pipeline'], packs: [], entries: 6,
};

/** Adopted, and declared nothing: addressable, and an asker would be guessing (D34). */
export const UNDECLARED: Registration = {
  repository: 'sandbox', adopted: true, registered: true, workspace: 'default', root: 'C:/somewhere/sandbox',
  owns: [], accepts: [], packs: [], entries: 0,
};

/** Not adopted, with a root here: drivable over the protocol door (INT3c, D70). */
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

/** The engine's line, set for the repository itself (WSR2). */
export const LINE: RepositoryLine = { repository: 'engine', workspace: 'default', branch: 'develop', source: 'repository' };

const nothing = () => {};

/** The driver's table of session languages (LANG1c), as it answers it. */
export const LANGUAGES = [{ code: 'en', name: 'English' }, { code: 'zh', name: 'Simplified Chinese (简体中文)' }];

/**
 * The engine's Setup as Daoris decides it (UX6f): driven here with a tree per session, its line its workspace's, its work
 * merged into its line, no session language, no standing answer, read by agents outside it, writing into nothing else
 * and no rule of its own. Every section folds.
 */
export const SETUP_DEFAULTS: RepositorySetupProps = {
  repository: 'engine',
  driving: { drivable: true, held: false, ownTree: true, onDrive: nothing, onHold: nothing, onTrees: nothing },
  work: {
    line: { repository: 'engine', workspace: 'default', branch: 'main', source: 'workspace' },
    onLine: nothing,
    landing: { repository: 'engine', workspace: 'default', form: 'merge', source: 'default' },
    landingAbove: { form: 'merge' },
    landers: ['github-pull-request'],
    onLanding: nothing,
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
 * The engine's Setup with the person's own values (UX6f): its own line, work put on a branch a plugin pushes and accepts
 * automatically, its own session language, a standing answer, read by nobody outside it, writing into the game, and two
 * rules of its own. Line and landing, Sessions and Reach open; Driving folds.
 */
export const SETUP_OWN: RepositorySetupProps = {
  ...SETUP_DEFAULTS,
  work: {
    ...SETUP_DEFAULTS.work,
    line: { repository: 'engine', workspace: 'default', branch: 'develop', source: 'repository' },
    landing: {
      repository: 'engine', workspace: 'default', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request',
      autoAccept: true, source: 'repository',
    },
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
