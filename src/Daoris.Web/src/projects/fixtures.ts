import type { Registration, Repository } from '../api';
import type { RepositoryLine } from '../settings/Lines';

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
