import type { SystemWiring, WiringFolder } from '../bridge/system';

// What the wiring reads back, for the stories and the tests of This Daoris (WIRING1c): neutral paths by convention, like the
// driver's fixtures, so a tracked file carries no machine path.

const HOME = 'D:/somewhere/Daoris';
const MEASURED = '2026-10-16T10:42:00Z';

/** One top folder, measured by default. */
export const folder = (id: WiringFolder['id'], extra: Partial<WiringFolder> = {}): WiringFolder => ({
  id, path: `${HOME}/${id === 'app' ? 'app' : id}`, pathWhy: null, present: true, bytes: 2_400_000, bytesWhy: null,
  measured: MEASURED, old: [], ...extra,
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
  files: [],
  variables: [],
  versions: {
    program: '0.21.0', build: 'abc1234', kit: null, cli: null, driver: null, service: null, connector: null, dotnet: null, node: null,
  },
  terminal: {
    launchers: [], launchersWhy: null, onAccountPath: null, onAccountPathWhy: null, onMachinePath: null, onMachinePathWhy: null,
    firstDaoris: { is: 'none', path: null }, firstDriver: { is: 'none', path: null },
    address: { present: false, url: null, pid: null, since: null, taken: null, why: null },
  },
  knowledge: { path: null, pathWhy: null, from: null, bytes: null, bytesWhy: null, familyRoot: null, familyRootWhy: null },
  logs: { path: null, pathWhy: null, bytes: null, bytesWhy: null, days: 0, newest: null, newestWhy: null, engineLog: false },
};

/** The same read while a walk is under way: no size yet. */
export const MEASURING: SystemWiring = {
  ...WIRED,
  measuring: true,
  measured: null,
  folders: WIRED.folders.map((each) => ({ ...each, bytes: null, bytesWhy: 'measuring', measured: null })),
  leftOver: WIRED.leftOver.map((each) => ({ ...each, bytes: null })),
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
  loop: {
    ...WIRED.loop, holder: { kind: 'headless', pid: 777, since: '2026-10-16T08:00:00Z', thisWindow: false },
    draining: true, holds: 2,
  },
};
