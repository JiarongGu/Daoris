import type { CliCommand } from '../platform/types.ts';
import { commandDeclaration } from '../declaration/dryrun.ts';

export const command: CliCommand = {
  name: 'declaration',
  kind: 'doctrine',
  usage: [
    '  declaration split --dry-run',
    "                       convert daoris.gates.json to the daoris/ folder and the home's",
    '                       values in scratch, and prove they resolve to the old file;',
    '                       names each difference and each set held twice',
  ],
  options: [
    '  --out <folder>       where a dry run writes its scratch (declaration only; default _fixtures/declaration-split)',
  ],
  run: commandDeclaration,
};
