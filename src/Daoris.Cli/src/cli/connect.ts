import type { CliCommand } from '../types.ts';
import { commandConnect } from '../connect.ts';

export const command: CliCommand = {
  name: 'connect',
  kind: 'management',
  usage: [
    '  connect              register this repo with a knowledge service: what it owns',
    '                       and what it accepts, so siblings know what to ask of it',
  ],
  options: [
    '  --workspace <name>   which workspace this repo shares with, on THIS machine',
    '                       (connect, import). Wiring, like a git remote: it is kept in',
    "                       the machine's registry and written into no tracked file.",
    '                       Omit to leave the existing wiring alone',
  ],
  run: commandConnect,
};
