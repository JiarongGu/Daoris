import type { GitActs, GitIdentityShown, GitPageShown, GitTestAnswer } from './Git';
import type { GitLibraryReadShown } from './GitReads';

// Settings → Git in the shape the shell's `TOOLS_LIST` answers as `identities`: the stories, the organisms' tests and the
// domain's tests draw the same machine. Neutral by convention: a host is an example's, a key a made-up one, and no path appears.

const KEY = 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIExampleExampleExampleExampleExampleExample daoris-work';

export const WORK: GitIdentityShown = {
  id: 'gi-00000000000a',
  label: 'Work',
  host: 'github.com',
  way: 'ssh',
  user: 'octo-work',
  keyType: 'ed25519',
  fingerprint: 'SHA256:q3VfExampleExampleExampleExampleExampleE',
  readOnly: false,
  default: true,
  commitsName: 'Work Name',
  commitsEmail: 'work@example.invalid',
  test: { result: 'reached', user: 'octo-work', at: new Date(Date.now() - 2 * 60_000).toISOString() },
  lastUsed: new Date(Date.now() - 5 * 60_000).toISOString(),
  publicKey: KEY,
  keyPage: 'https://github.com/settings/keys',
  repositories: 12,
  workspaces: 0,
  pointedAt: ["engine's origin", "workspace games's github.com"],
};

export const PERSONAL: GitIdentityShown = {
  id: 'gi-00000000000b',
  label: 'Personal',
  host: 'github.com',
  way: 'https',
  user: 'octo-me',
  readOnly: false,
  default: true,
  test: { result: 'reached', user: 'octo-me', at: new Date(Date.now() - 26 * 3_600_000).toISOString() },
  lastUsed: new Date(Date.now() - 26 * 3_600_000).toISOString(),
  repositories: 0,
  workspaces: 1,
  pointedAt: ['workspace games\'s github.com'],
};

export const DEPLOY: GitIdentityShown = {
  id: 'gi-00000000000c',
  label: 'Org A deploy',
  host: 'dev.azure.com',
  way: 'ssh',
  keyType: 'rsa-4096',
  fingerprint: 'SHA256:Zk1pExampleExampleExampleExampleExampleEx',
  readOnly: true,
  default: true,
  test: { result: 'refused', at: new Date(Date.now() - 3_600_000).toISOString() },
  publicKey: KEY.replace('ed25519', 'rsa'),
  repositories: 1,
  workspaces: 0,
  pointedAt: [],
};

export const READS: GitLibraryReadShown[] = [
  { repository: 'engine', library: 412, git: 9, reasons: [{ reason: 'unanswered', count: 7 }, { reason: 'replacements', count: 2 }], last: new Date(Date.now() - 4 * 60_000).toISOString() },
  { repository: 'archive', library: 0, git: 131, reasons: [{ reason: 'reftable', count: 131 }], last: new Date(Date.now() - 3 * 3_600_000).toISOString() },
  { repository: 'site', library: 58, git: 0, reasons: [], last: new Date(Date.now() - 26 * 3_600_000).toISOString() },
];

export const PAGE: GitPageShown = {
  identities: [WORK, PERSONAL, DEPLOY], own: ['gitlab.com'], hosts: ['dev.azure.com', 'github.com', 'gitlab.com'], reads: READS,
};

export const EMPTY_PAGE: GitPageShown = { identities: [], own: [], hosts: [] };

export const UNKNOWN_HOST: GitTestAnswer = {
  result: 'host-unknown',
  hostKeys: [
    { algorithm: 'ssh-ed25519', fingerprint: 'SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU', published: true },
    { algorithm: 'ssh-rsa', fingerprint: 'SHA256:ExampleNotPublishedExampleNotPublishedEx', published: false },
  ],
};

/** The presses as a story or a test stands them in: each answers at once; a test replaces the one it watches. */
export const ACTS: GitActs = {
  add: async (input) => ({ identity: 'gi-00000000000d', label: input.label, host: input.host, way: input.way, publicKey: input.way === 'ssh' ? KEY : null }),
  test: async () => ({ result: 'reached', user: 'octo-work', hostKeys: [] }),
  rename: async () => ({}),
  commitsAs: async () => ({}),
  readOnly: async () => ({}),
  apiToken: async () => ({}),
  remove: async () => ({ pointing: [] }),
  makeDefault: async () => ({}),
  own: async () => ({}),
  copy: () => {},
};
