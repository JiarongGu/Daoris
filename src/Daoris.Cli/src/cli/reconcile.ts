import type { CliCommand } from '../platform/types.ts';
import { commandReconcile } from '../doctrine/reconcile.ts';

export const command: CliCommand = {
  name: 'reconcile',
  kind: 'doctrine',
  usage: [
    '  reconcile            a brief of every difference from the canon, for this',
    "                       repository's session to adopt, keep or propose upstream",
  ],
  run: commandReconcile,
};
