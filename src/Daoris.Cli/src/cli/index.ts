// The `index` command, not a barrel: an ES module resolves no folder index, and nothing imports `cli/`.
import type { CliCommand } from '../platform/types.ts';
import { commandIndex } from '../doctrine/indexgen.ts';

export const command: CliCommand = {
  name: 'index',
  kind: 'doctrine',
  usage: [
    '  index                say where the roster is: the rules in AGENTS.md, the',
    '                       knowledge and skills in the INDEX.md sync writes',
  ],
  run: commandIndex,
};
