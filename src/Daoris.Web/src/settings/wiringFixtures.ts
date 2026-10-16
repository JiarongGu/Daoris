import type { SystemWiring, WiringFile, WiringFolder, WiringVariable } from '../data/wiring';

// What the wiring reads back, for the stories and the tests of This Daoris (WIRING1c): neutral paths by convention, like the
// driver's fixtures, so a tracked file carries no machine path.

const HOME = 'D:/somewhere/Daoris';
const MEASURED = '2026-10-16T10:42:00Z';

/** One top folder, measured by default. */
export const folder = (id: WiringFolder['id'], extra: Partial<WiringFolder> = {}): WiringFolder => ({
  id, path: `${HOME}/${id === 'app' ? 'app' : id}`, pathWhy: null, present: true, bytes: 2_400_000, bytesWhy: null,
  measured: MEASURED, old: [], ...extra,
});

/** One settings file or record, there and reading by default. */
export const file = (id: string, extra: Partial<WiringFile> = {}): WiringFile => ({
  id, folder: 'data', path: `${HOME}/data/${id}.json`, pathWhy: null, present: true, bytes: 1_000, bytesWhy: null,
  written: '2026-10-15T18:00:00Z', reads: true, readsWhy: null, overriddenBy: null, door: null, record: false, ...extra,
});

/** One variable, not set by default. */
export const variable = (name: string, extra: Partial<WiringVariable> = {}): WiringVariable => ({
  name, set: false, value: null, valueWhy: 'not-set', secret: false, prefix: null, from: 'not-set',
  account: null, accountSet: false, machine: null, machineSet: false, instead: null, readBy: ['desktop', 'cli'], ...extra,
});

/** A whole read of an installed Daoris whose window hosts the service, sizes counted. */
export const WIRED: SystemWiring = {
  schema: 1,
  reader: 'window',
  measuring: false,
  measured: MEASURED,
  partial: false,
  partialWhy: null,
  folder: {
    path: HOME, pathWhy: null, kind: 'install',
    account: 'same', accountWhy: null, accountNames: 'root', accountNamesWhy: null,
    notice: null, noticeWhy: null, hostNotice: null, hostNoticeWhy: null,
  },
  folders: [
    folder('app', { bytes: 310_000_000 }),
    folder('update', { present: false, bytes: null, bytesWhy: 'absent', measured: null }),
    folder('data', { bytes: 18_500 }),
    folder('profiles', { bytes: 52_000_000 }),
    folder('work', {
      bytes: 1_200_000_000,
      old: [{ entry: 'trees', path: `${HOME}/trees`, bytes: 4_096, why: 'resumes' }],
    }),
    folder('cache'),
    folder('run', { bytes: 812 }),
    folder('logs', { bytes: 96_000 }),
  ],
  leftOver: [{ name: 'webview2', path: `${HOME}/webview2`, bytes: 7_300_000, why: 'earlier-build' }],
  service: {
    url: 'http://localhost:5177', urlWhy: null, from: 'default',
    held: 'hosted', heldWhy: null,
    answers: true, answersWhy: null, asked: '2026-10-16T10:41:30Z',
    tier: 'semantic', tierWhy: null, version: '0.21.0', versionWhy: null, proved: true, provedWhy: null,
    host: { path: `${HOME}/app/daoris-http/Daoris.Service.Http`, pathWhy: null, from: 'beside' },
    connector: { path: `${HOME}/app/daoris-knowledge/daoris-knowledge`, pathWhy: null, from: 'beside' },
  },
  remotes: [{ workspace: 'aurora', url: 'https://aurora.example.com', urlWhy: null, keySet: true, keyPrefix: 'dk_abcd1234', from: 'file' }],
  remotesWhy: null,
  loop: {
    holder: { kind: 'desktop', pid: 4242, since: '2026-10-16T09:00:00Z', thisWindow: true },
    looked: '2026-10-16T10:41:58Z', lookedWhy: null, ready: true, readyWhy: null, draining: false, drainingWhy: null,
    holds: 0, holdsWhy: null, pollSeconds: 30, pollSecondsWhy: null,
  },
  files: [
    file('driver', { bytes: 1_200, door: 'driver' }),
    file('harnesses', { bytes: 640, overriddenBy: 'DAORIS_HARNESS_CONFIG', door: 'agents' }),
    file('tools', { bytes: 90, reads: false, readsWhy: 'unparsed', door: 'tools' }),
    file('remotes', { folder: 'profiles', bytes: 230, door: 'workspace' }),
    file('browser-settings', { present: false, bytes: null, bytesWhy: 'absent', written: null, reads: null, readsWhy: 'absent', door: 'browser' }),
    file('usage', { bytes: 48_000, reads: null, readsWhy: 'record', record: true }),
    file('knowledge', { bytes: 9_400_000, reads: null, readsWhy: 'record', record: true }),
  ],
  variables: [
    variable('DAORIS_HOME', { set: true, value: HOME, from: 'set-by-daoris', account: 'D:/somewhere/first/data', accountSet: true, instead: null }),
    variable('DAORIS_SERVICE_URL', { set: true, value: 'http://localhost:5177', from: 'inherited', account: 'http://localhost:5177', accountSet: true, instead: null }),
    variable('DAORIS_SERVICE_KEY', { set: true, value: null, valueWhy: 'secret', secret: true, prefix: 'dk_abcd1234', from: 'this-start', instead: null }),
    variable('DAORIS_REMOTE_KEY', { secret: true, instead: 'remotes-file' }),
    variable('DAORIS_EMBED_MODEL', { instead: 'lexical' }),
    variable('DAORIS_EMBED_WINDOW', { instead: '2000' }),
  ],
  versions: {
    program: '0.21.0', build: 'abc1234', kit: 'Shenora 0.21.1', cli: '0.21.0', driver: '0.21.0', service: '0.21.0', connector: '0.21.0',
    dotnet: '10.0.1', node: null, nodeWhy: 'tools',
  },
  terminal: {
    launchers: [
      { name: 'daoris', present: true }, { name: 'daoris.cmd', present: true },
      { name: 'daoris-driver', present: true }, { name: 'daoris-driver.cmd', present: false },
    ],
    launchersWhy: null, onAccountPath: true, onAccountPathWhy: null, onMachinePath: false, onMachinePathWhy: null,
    firstDaoris: { is: 'this', path: `${HOME}/app/bin/daoris.cmd` },
    firstDriver: { is: 'another', path: 'D:/somewhere/else/bin/daoris-driver.cmd' },
    address: { present: true, url: 'http://localhost:5177', pid: 4242, since: '2026-10-16T09:00:00Z', taken: true, why: null },
  },
  knowledge: {
    path: `${HOME}/data/knowledge.db`, pathWhy: null, from: 'home', bytes: 9_400_000, bytesWhy: null,
    familyRoot: 'D:/somewhere/family', familyRootWhy: null,
  },
  logs: { path: `${HOME}/logs`, pathWhy: null, bytes: 96_000, bytesWhy: null, days: 30, newest: '2026-10-16T10:40:00Z', newestWhy: null, engineLog: true },
  toolHome: {
    path: 'D:/somewhere/person/.claude', pathWhy: null, from: 'default', present: true,
    why: null, caseInsensitive: true, ambiguous: 0, uncertain: 0,
    folders: [
      {
        name: 'D--somewhere-Daoris-trees-main-repo-s-1a2b3c4d', path: 'D:/somewhere/person/.claude/projects/D--somewhere-Daoris-trees-main-repo-s-1a2b3c4d',
        bytes: 2_400_000, bytesWhy: null, partial: false, newest: '2026-10-15T18:00:00Z', newestWhy: null,
        source: 'tree', by: ['q-1-s-1a2b3c4d'], keeps: null,
      },
      {
        name: 'D--somewhere-Daoris-help', path: 'D:/somewhere/person/.claude/projects/D--somewhere-Daoris-help',
        bytes: 90_000, bytesWhy: null, partial: false, newest: '2026-10-14T09:30:00Z', newestWhy: null,
        source: 'help', by: ['help-1', 'help-2'], keeps: 'resumable',
      },
    ],
  },
};

/** The same read while a walk is under way: no size yet. */
export const MEASURING: SystemWiring = {
  ...WIRED,
  measuring: true,
  measured: null,
  folders: WIRED.folders.map((each) => ({ ...each, bytes: null, bytesWhy: 'measuring', measured: null })),
  leftOver: WIRED.leftOver.map((each) => ({ ...each, bytes: null })),
  toolHome: {
    ...WIRED.toolHome!,
    folders: WIRED.toolHome!.folders.map((each) => ({ ...each, bytes: null, bytesWhy: 'measuring', newest: null, newestWhy: 'not-measured' })),
  },
};

/** A development run: the home named for this start, an account that names another, the notices, a service not answering. */
export const DEVELOPMENT: SystemWiring = {
  ...WIRED,
  folder: {
    ...WIRED.folder, kind: 'development', account: 'overridden', accountNames: 'data',
    notice: `Daoris home: ${HOME} — this install's own data folder, not D:/somewhere/first/data.`,
    hostNotice: 'the host at http://localhost:5177 was already running and serves a different page than this install carries.',
  },
  service: {
    ...WIRED.service, held: 'adopted', answers: false, answersWhy: 'foreign', tier: null, tierWhy: 'not-answering',
    version: null, versionWhy: 'not-answering', proved: null, provedWhy: 'not-asked',
    host: { path: null, pathWhy: 'not-found', from: null }, connector: { path: null, pathWhy: 'not-found', from: null },
  },
  remotes: [],
  files: [
    ...WIRED.files.slice(0, 2),
    file('tools', { bytes: 90, reads: false, readsWhy: 'unreadable', door: 'tools' }),
    file('plugin-state', { present: false, bytes: null, bytesWhy: 'absent', written: null, reads: null, readsWhy: 'absent', door: 'plugins' }),
  ],
  variables: [
    variable('DAORIS_KNOWLEDGE_ROOT', { valueWhy: 'adopted', from: 'unknown', accountSet: null, machineSet: null }),
    variable('DAORIS_SERVICE_URL', { set: true, value: 'http://localhost:5177', from: 'this-start', account: 'http://localhost:6000', accountSet: true, instead: null }),
  ],
  versions: {
    ...WIRED.versions, kit: null, kitWhy: 'window', cli: null, cliWhy: 'development', driver: null, driverWhy: 'development',
    service: null, serviceWhy: 'not-asked', connector: null, connectorWhy: 'not-found',
  },
  terminal: {
    launchers: [], launchersWhy: 'development', onAccountPath: null, onAccountPathWhy: 'development', onMachinePath: null, onMachinePathWhy: 'development',
    firstDaoris: { is: 'none', path: null }, firstDriver: { is: 'none', path: null },
    address: { present: true, url: 'http://localhost:5177', pid: 31, since: '2026-10-16T08:00:00Z', taken: false, why: 'ended' },
  },
  knowledge: {
    path: 'D:/somewhere/elsewhere/knowledge.db', pathWhy: null, from: 'variable', bytes: null, bytesWhy: 'absent',
    familyRoot: null, familyRootWhy: 'adopted',
  },
  logs: { ...WIRED.logs, bytes: null, bytesWhy: 'not-measured', newest: null, newestWhy: 'absent', engineLog: false },
  // The person's own tool home, with nothing of Daoris's in it (D175: new sessions run in Daoris's own space).
  toolHome: { ...WIRED.toolHome!, from: 'process', folders: [] },
  loop: {
    ...WIRED.loop, holder: { kind: 'headless', pid: 777, since: '2026-10-16T08:00:00Z', thisWindow: false },
    draining: true, holds: 2,
  },
};
