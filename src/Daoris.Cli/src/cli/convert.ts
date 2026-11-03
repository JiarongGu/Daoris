import type { CliCommand } from '../platform/types.ts';
import { runDriverProgram } from '../platform/driverprogram.ts';

export const command: CliCommand = {
  name: 'convert',
  kind: 'management',
  usage: [
    "  convert [verb]       this home's old records converted, every area's part in one",
    '                       plan, through the running Daoris:',
    '                         --plan                    what each part changes; writes nothing',
    '                         --apply <digest>          your press, confirmed in its window; a',
    '                                                   failed part is pressed again',
    '                         --apply <digest> --leave <part>',
    '                                                   a failed part left for a later plan',
    '                         --undo [<digest>]         the latest conversion taken back',
  ],
  // ONEWAY1ae (the oneway design §1.1, §11): the home's one conversion door is Daoris's driver program's, `daoris-driver convert` with the
  // same words, every one of them; this writes nothing before it starts, and the program goes through the running Daoris alone.
  run: (args) => (args.driverProgram ?? ((family, words) => runDriverProgram(family, words)))('convert', args.argv),
};
