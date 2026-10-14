import type { CliCommand } from '../platform/types.ts';
import { commandInit } from '../doctrine/commands.ts';

export const command: CliCommand = {
  name: 'init',
  kind: 'doctrine',
  usage: [
    '  init                 detect what this repo has, write daoris.json',
  ],
  options: [
    '  --harness <name>     the layout init writes: claude-code (the default), or agents —',
    '                       knowledge and skills under .agents/, mirrored for Claude Code (init only)',
  ],
  run: commandInit,
};
