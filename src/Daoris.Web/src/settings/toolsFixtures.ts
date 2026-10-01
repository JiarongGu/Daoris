import type { ToolListShown, ToolShown, ToolsShown } from './Tools';

// Settings → Tools (TOOLS7) in the shape the shell's `TOOLS_LIST` answers: the stories, the cards' tests and the
// domain's tests draw the same machine. Neutral by convention: a tracked file carries no machine path, not even a
// plausible-looking one.

export const GIT: ToolShown = {
  tool: 'git',
  name: 'Git',
  way: 'system',
  resolved: 'C:/somewhere/Git/cmd/git.exe',
  refused: false,
  system: 'C:/somewhere/Git/cmd/git.exe',
  asked: '2.47.1',
  downloaded: [{ version: '2.51.0', size: 37_180_000, inUse: false }],
  offered: [
    { version: '2.51.0', size: 37_180_000, archive: 'zip', lists: ['the built-in list'], hosts: ['github.com'], downloaded: true },
    { version: '2.50.1', size: 36_900_000, archive: 'zip', lists: ['the built-in list'], hosts: ['github.com'], downloaded: false },
  ],
  refusedVersions: [],
  newest: '2.51.0',
  source: 'https://github.com/git-for-windows/git/releases',
  licence: { id: 'GPL-2.0-only', url: 'https://github.com/git-for-windows/git/blob/main/COPYING' },
};

export const NODE: ToolShown = {
  tool: 'node',
  name: 'Node.js',
  way: 'managed',
  version: '22.11.0',
  resolved: 'C:/somewhere/data/tools/node/22.11.0/package/node-v22.11.0-win-x64/node.exe',
  refused: false,
  system: 'C:/somewhere/nodejs/node.exe',
  asked: '22.11.0',
  downloaded: [
    { version: '22.11.0', size: 32_500_000, inUse: true },
    { version: '20.18.0', size: 30_100_000, inUse: false },
  ],
  offered: [
    { version: '22.12.0', size: 32_700_000, archive: 'zip', lists: ['https://mirror.example/resources.json', 'the built-in list'], hosts: ['mirror.example', 'nodejs.org'], downloaded: false },
    { version: '22.11.0', size: 32_500_000, archive: 'zip', lists: ['the built-in list'], hosts: ['nodejs.org'], downloaded: true },
  ],
  refusedVersions: [],
  newest: '22.12.0',
  source: 'https://nodejs.org/dist',
  licence: { id: 'MIT', url: 'https://github.com/nodejs/node/blob/main/LICENSE' },
};

export const PWSH: ToolShown = {
  tool: 'pwsh',
  name: 'PowerShell',
  way: 'file',
  file: 'C:/somewhere/PowerShell/7/pwsh.exe',
  refused: true,
  problem: 'PowerShell runs the file C:/somewhere/PowerShell/7/pwsh.exe, and there is no file there — it never falls back to PATH. `daoris tool use pwsh file <path>` names another, and `daoris tool use pwsh system` runs the one on PATH',
  downloaded: [],
  offered: [],
  refusedVersions: [],
};

export const GH: ToolShown = {
  tool: 'gh',
  name: 'GitHub CLI',
  way: 'system',
  refused: false,
  problem: '`gh` is not on this machine\'s PATH. A tool is run as the system\'s, managed, or from a file you name: `daoris tool use gh file <path>` names one',
  downloaded: [],
  offered: [
    { version: '2.63.0', size: 12_400_000, archive: 'zip', lists: ['the built-in list'], hosts: ['github.com'], downloaded: false },
  ],
  refusedVersions: [],
  newest: '2.63.0',
  source: 'https://github.com/cli/cli/releases',
  licence: { id: 'MIT' },
};

export const AZ: ToolShown = {
  tool: 'az',
  name: 'Azure CLI',
  way: 'system',
  refused: false,
  downloaded: [],
  offered: [],
  refusedVersions: [],
};

export const MIRROR: ToolListShown = {
  address: 'https://mirror.example/resources.json',
  integrity: 'mirror.example',
  exists: true,
  fetched: new Date(Date.now() - 2 * 3600_000).toISOString(),
  sha256: 'a'.repeat(64),
  names: ['node 22.12.0'],
  notes: [],
};

export const BUILT_IN: ToolListShown = {
  integrity: 'built in',
  exists: true,
  sha256: 'b'.repeat(64),
  names: ['git 2.51.0', 'git 2.50.1', 'node 22.11.0', 'pwsh 7.4.6', 'gh 2.63.0'],
  notes: [],
};

export const TOOLS: ToolsShown = {
  file: 'C:/somewhere/data/tools.json',
  exists: true,
  tools: [GIT, NODE, PWSH, GH, AZ],
  locations: [MIRROR],
  builtIn: BUILT_IN,
};
