import type { CliCommand } from '../platform/types.ts';
import { commandUpstream } from '../doctrine/upstream.ts';

export const command: CliCommand = {
  name: 'upstream',
  kind: 'doctrine',
  usage: [
    '  upstream <file>      propose a locally-edited canonical file to the canon: the',
    "                       request's words, written into the canon only in its own",
    '                       checkout',
  ],
  options: [
    '  --all                every drifted file (upstream only)',
  ],
  run: commandUpstream,
};
