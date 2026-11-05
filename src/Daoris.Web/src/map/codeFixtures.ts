import type { CodeDependency, CodeModule } from '../data/api';

// The code map's fixtures (CANVAS1f; the canvas design §9): this repository's own map, synthetic ones at any size, a cycle and long ids.
// Stories, tests and the measured book draw these; nothing ships them.

export type CodeFixture = { modules: CodeModule[]; dependencies: CodeDependency[] };

const mod = (id: string, path: string, summary = ''): CodeModule => ({ id, path, summary });
const dep = (from: string, to: string, kind = 'project'): CodeDependency => ({ from, to, kind });

/** This repository's map as `docs/code-map.json` holds it: 20 modules, 20 dependencies. */
export const REPOSITORY: CodeFixture = {
  modules: [
    mod('daoris', 'src/Daoris.Cli', 'Engineering doctrine for coding agents'),
    mod('daoris-web', 'src/Daoris.Web', 'the platform, the only UI'),
    mod('Daoris.Desktop.App', 'src/Daoris.Desktop/Daoris.Desktop.App', 'the desktop window'),
    mod('Daoris.Desktop.Driver', 'src/Daoris.Desktop/Daoris.Desktop.Driver', 'the driver'),
    mod('Daoris.Desktop.Driver.Host', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Host', 'the driver\'s headless door'),
    mod('Daoris.Desktop.Driver.Tests', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests', 'the driver\'s tests'),
    mod('Daoris.Desktop.GitDoor', 'src/Daoris.Desktop/Daoris.Desktop.GitDoor', 'the git doors'),
    mod('Daoris.Desktop.Launcher', 'src/Daoris.Desktop/Daoris.Desktop.Launcher', 'Daoris'),
    mod('Daoris.Desktop.Modules', 'src/Daoris.Desktop/Daoris.Desktop.Modules', 'the window\'s modules'),
    mod('Daoris.Desktop.Modules.Tests', 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests', 'the modules\' tests'),
    mod('Daoris.Desktop.PieceStart', 'src/Daoris.Desktop/Daoris.Desktop.PieceStart', 'the piece start'),
    mod('Daoris.Devkit.Cli', 'src/Daoris.Devkit/Daoris.Devkit.Cli', 'the devkit\'s command line'),
    mod('Daoris.Devkit.Core', 'src/Daoris.Devkit/Daoris.Devkit.Core', 'the five universal gates'),
    mod('Daoris.Devkit.Tests', 'src/Daoris.Devkit/Daoris.Devkit.Tests', 'the devkit\'s tests'),
    mod('Daoris.Service.Core', 'src/Daoris.Service/Daoris.Service.Core', 'the family\'s index'),
    mod('Daoris.Service.Http', 'src/Daoris.Service/Daoris.Service.Http', 'the HTTP host'),
    mod('Daoris.Service.Http.Tests', 'src/Daoris.Service/Daoris.Service.Http.Tests', 'its tests'),
    mod('Daoris.Service.Mcp', 'src/Daoris.Service/Daoris.Service.Mcp', 'reachable over MCP'),
    mod('Daoris.Service.Tests', 'src/Daoris.Service/Daoris.Service.Tests', 'the service\'s tests'),
    mod('chromium-probe', 'tools/chromium-probe', 'a probe'),
  ],
  dependencies: [
    dep('Daoris.Desktop.App', 'Daoris.Desktop.Driver'),
    dep('Daoris.Desktop.App', 'Daoris.Desktop.Modules'),
    dep('Daoris.Desktop.Driver', 'Daoris.Service.Http', 'http'),
    dep('Daoris.Desktop.Driver.Host', 'Daoris.Desktop.Driver'),
    dep('Daoris.Desktop.Driver.Tests', 'Daoris.Desktop.Driver'),
    dep('Daoris.Desktop.Driver.Tests', 'Daoris.Desktop.Driver.Host'),
    dep('Daoris.Desktop.Driver.Tests', 'Daoris.Desktop.GitDoor'),
    dep('Daoris.Desktop.Driver.Tests', 'Daoris.Desktop.Launcher'),
    dep('Daoris.Desktop.Driver.Tests', 'Daoris.Desktop.PieceStart'),
    dep('Daoris.Desktop.Modules', 'Daoris.Desktop.Driver'),
    dep('Daoris.Desktop.Modules.Tests', 'Daoris.Desktop.Modules'),
    dep('Daoris.Devkit.Cli', 'Daoris.Devkit.Core'),
    dep('Daoris.Devkit.Tests', 'Daoris.Devkit.Core'),
    dep('Daoris.Service.Http', 'Daoris.Service.Core'),
    dep('Daoris.Service.Http.Tests', 'Daoris.Service.Http'),
    dep('Daoris.Service.Mcp', 'Daoris.Service.Core'),
    dep('Daoris.Service.Tests', 'Daoris.Service.Core'),
    dep('Daoris.Service.Tests', 'Daoris.Service.Mcp'),
    dep('daoris', 'Daoris.Service.Http', 'http'),
    dep('daoris-web', 'Daoris.Service.Http', 'http'),
  ],
};

/**
 * A repository of `count` modules in `areas` top folders of parts, each depending on two earlier ones (a few across folders), so the
 * graph is acyclic and the same on every call.
 */
export function syntheticMap(count: number, areas = Math.max(2, Math.round(Math.sqrt(count) / 4))): CodeFixture {
  let seed = 7;
  const next = (below: number) => { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return seed % below; };
  const modules: CodeModule[] = [];
  const dependencies: CodeDependency[] = [];
  const parts = Math.max(2, Math.round(count / areas / 12));
  for (let index = 0; index < count; index += 1) {
    const id = `module-${String(index).padStart(4, '0')}`;
    modules.push({ id, path: `area-${String(index % areas).padStart(2, '0')}/part-${String(Math.floor(index / areas) % parts).padStart(2, '0')}/${id}`, summary: '' });
    if (index === 0) continue;
    for (let each = 0; each < 2; each += 1) {
      const to = `module-${String(next(index)).padStart(4, '0')}`;
      if (!dependencies.some((d) => d.from === id && d.to === to)) dependencies.push(dep(id, to, each === 0 ? 'project' : 'http'));
    }
  }
  return { modules, dependencies };
}

/** Three modules of one folder that depend on one another in a ring, beside two that do not. */
export const WITH_A_CYCLE: CodeFixture = {
  modules: [
    mod('engine', 'src/core/engine'), mod('planner', 'src/core/planner'), mod('runner', 'src/core/runner'),
    mod('web', 'src/web/web'), mod('store', 'src/data/store'),
  ],
  dependencies: [
    dep('engine', 'planner', 'calls'), dep('planner', 'runner', 'calls'), dep('runner', 'engine', 'callback'),
    dep('web', 'engine', 'http'), dep('runner', 'store', 'project'),
  ],
};

/** A cycle that crosses three folders, held by the folder above them, beside modules of those folders that are in no cycle. */
export const CYCLE_ACROSS_FOLDERS: CodeFixture = {
  modules: [
    mod('session', 'src/core/session'), mod('rules', 'src/core/rules'), mod('panel', 'src/web/panel'), mod('board', 'src/web/board'),
    mod('ledger', 'src/data/ledger'), mod('cache', 'src/data/cache'), mod('cli', 'tools/cli'),
  ],
  dependencies: [
    dep('session', 'ledger', 'calls'), dep('ledger', 'panel', 'notifies'), dep('panel', 'session', 'calls'),
    dep('session', 'rules'), dep('board', 'panel'), dep('ledger', 'cache'), dep('cli', 'session', 'http'),
  ],
};

/** A stack of layers whose top uses what is several layers down: the lines that skip a layer are routed around the cards between. */
export const SKIPPING_LAYERS: CodeFixture = {
  modules: [
    mod('app', 'src/app/app'), mod('screens', 'src/app/screens'), mod('widgets', 'src/app/widgets'), mod('core', 'src/app/core'),
    mod('store', 'src/app/store'), mod('log', 'src/app/log'),
  ],
  dependencies: [
    dep('app', 'screens'), dep('screens', 'widgets'), dep('widgets', 'core'), dep('core', 'store'), dep('store', 'log'),
    dep('app', 'core', 'calls'), dep('app', 'log', 'calls'), dep('screens', 'store', 'reads'),
  ],
};

/** Ids longer than a card holds, cut on it and whole in the detail. */
export const LONG_IDS: CodeFixture = {
  modules: [
    mod('Daoris.Desktop.Driver.Host.Integration', 'src/Desktop/Host/Daoris.Desktop.Driver.Host.Integration', 'the headless driver'),
    mod('Daoris.Desktop.Modules.Bridge.Contracts', 'src/Desktop/Modules/Daoris.Desktop.Modules.Bridge.Contracts', 'the IPC surfaces'),
  ],
  dependencies: [dep('Daoris.Desktop.Driver.Host.Integration', 'Daoris.Desktop.Modules.Bridge.Contracts')],
};
