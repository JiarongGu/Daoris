import type { CliCommand } from '../platform/types.ts';
import { commandSync } from '../doctrine/materialize.ts';

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
