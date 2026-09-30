import type { CliCommand } from '../types.ts';
import { commandSync } from '../materialize.ts';

export const command: CliCommand = {
  name: 'sync',
  kind: 'doctrine',
  usage: [
    "  sync                 materialize the manifest's packs; write daoris.lock",
  ],
  options: [
    '  --force              overwrite locally-drifted files (sync only)',
  ],
  run: commandSync,
};
