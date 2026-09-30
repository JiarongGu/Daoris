import type { CliCommand } from '../types.ts';
import { commandImport } from '../manage.ts';

export const command: CliCommand = {
  name: 'import',
  kind: 'management',
  usage: [
    "  import [folder]      register a folder's subdirectories in one go; safe to",
    "                       re-run, and it never re-points anyone's workspace —",
    '                       unless --workspace W names the workspace they land in',
  ],
  run: commandImport,
};
