import type { Mock } from 'vitest';

// What the shell answers a release step's doors (`workflow/release/ReleaseStepDoors.tsx`) over a mocked bridge: Azure DevOps's plugin
// as `PLUGINS` lists it, the run's repository's origin as `GIT_BRANCHES` reads its remotes, and one round of `PLUGIN_COMMAND`.
// A suite mocks `@shenora/react` with its own `invoke` and wires it here.

/** The origin address of the run's repository (`GRAPH_RUN_APPROVAL`'s `portal`). */
export const ORIGIN = 'https://dev.azure.com/acme/Proj/_git/portal';

/** Azure DevOps's plugin as `PLUGINS` lists it: its `approve-release` the approver's act, with `tool` also an agent's tool where it holds the role. */
export const azurePlugin = (tool: boolean) => ({
  id: 'daoris-azure-devops', name: 'Azure DevOps', version: '4.6.0', description: 'Pull requests and releases on Azure DevOps.',
  enabled: true, problem: null, harnesses: [], points: ['work/perform'], running: true,
  folder: 'C:/somewhere/data/plugins/daoris-azure-devops', data: 'C:/somewhere/data/plugins/.data/daoris-azure-devops',
  interfaces: {
    commands: [{
      name: 'approve-release', title: 'Approve or reject a release stage', about: 'The approver’s own press, through their own sign-in.', writes: true,
      ...(tool ? { tool: true, role: 'approver' } : {}),
    }],
    storage: null, views: [], refused: [], unknown: [],
  },
  switches: [],
});

/** Answers `invoke` for a release step's doors; `origin` null for none; another `workspace` lists `aurora`'s same-named checkout first. */
export function wireRelease(invoke: Mock, { tool = false, origin = ORIGIN as string | null, workspace = 'aurora' } = {}) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'PLUGINS') return { folder: 'C:/somewhere/data/plugins', plugins: [azurePlugin(tool)], kit: { points: [] }, offers: [] };
    if (type === 'GIT_BRANCHES') {
      const remotes = origin ? [{ name: 'origin', address: origin, serves: 'own', passed: [] }] : [];
      const other = workspace === 'aurora' ? [] : [{ repository: 'portal', workspace: 'aurora', remotes: [] }];
      return { repositories: [...other, { repository: 'portal', workspace, remotes }] };
    }
    if (type === 'PLUGIN_COMMAND') return { run: 'r1', said: ['Prod of Release-90 approved.'], ask: null, open: null, code: null, then: null, data: null, exit: 0 };
    return {};
  });
}

/** The payloads `invoke` received for the route `type`, in order. */
export const sentTo = (invoke: Mock, type: string) =>
  invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload?: unknown } | undefined)?.payload);
