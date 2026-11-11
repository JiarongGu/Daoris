// A place's sessions work apart where the workflow it follows starts with an `isolate` step. This workflow does; it is
// saved into the home a config sits in through the workflow door, then used for a repository (or named as the home's
// default, for a config written whole).
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const APART_WORKFLOW = {
  id: 'apart',
  name: 'Apart',
  versions: [{
    version: 1, at: '2026-11-01T09:00:00Z', door: 'screen', schema: 2,
    graph: {
      schemaVersion: 2, entries: ['apart'], end: 'line',
      nodes: [
        { id: 'apart', kind: 'isolate', version: 1, reach: 'observed', options: { kind: 'worktree' } },
        { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
        { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
        { id: 'line', kind: 'line', version: 1, reach: 'observed' },
      ],
      wires: [
        { id: 'a', kind: 'after', from: 'apart', to: 'work' },
        { id: 'b', kind: 'after', from: 'work', to: 'landing' },
        { id: 'c', kind: 'after', from: 'landing', to: 'line' },
      ],
    },
  }],
};

/** `saveApart(config)`, `workApartOn(repository, config?)` and `workApartOff(repository, config?)` over the rehearsal's run seam. */
export const apartVerbs = ({ runOn, cliBin, scratch, person, defaultConfig }) => {
  const workflow = (config, words) => runOn(config, `node "${cliBin}" workflow ${words}`, person);
  const saveApart = (config) => {
    const file = join(scratch, 'apart-workflow.json');
    writeFileSync(file, `${JSON.stringify(APART_WORKFLOW, null, 2)}\n`);
    return workflow(config, `import "${file}"`);
  };
  return {
    /** Writes a whole config whose default workflow is this one, and saves the workflow in the home it sits in. */
    writeApartConfig: (file, config) => {
      writeFileSync(file, `${JSON.stringify({ ...config, defaultWorkflow: APART_WORKFLOW.id }, null, 2)}\n`);
      saveApart(file);
    },
    workApartOn: (repository, config = defaultConfig) => {
      saveApart(config);
      return workflow(config, `use apart --repository ${repository}`);
    },
    workApartOff: (repository, config = defaultConfig) => workflow(config, `use --clear --repository ${repository}`),
  };
};
