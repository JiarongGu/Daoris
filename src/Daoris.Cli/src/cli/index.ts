// The `index` command, not a barrel: an ES module resolves no folder index, and nothing imports `cli/`.
import type { CliCommand } from '../types.ts';
import { commandIndex } from '../indexgen.ts';

export const command: CliCommand = {
  name: 'index',
  kind: 'doctrine',
  usage: [
    '  index                say where the roster went: the AGENTS.md region sync writes',
  ],
  run: commandIndex,
};
