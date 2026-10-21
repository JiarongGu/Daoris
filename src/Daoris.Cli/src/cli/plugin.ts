import type { CliCommand } from '../platform/types.ts';
import { runDriverProgram } from '../platform/driverprogram.ts';

export const command: CliCommand = {
  name: 'plugin',
  kind: 'management',
  usage: [
    "  plugin [verb]        this machine's plugins ($DAORIS_HOME/plugins/<id>/plugin.json):",
    '                         list                      each one, what it declares and',
    '                                                   speaks, why a refused one does not',
    '                         add <folder>              copy one in under its id, and',
    '                                                   record where it came from',
    "                         add --offer <id>          install one of Daoris's own, which",
    '                                                   `list` shows this install offers',
    '                         update <id> [--yes]       what a newer copy at its source',
    '                                                   changes; --yes replaces it',
    '                         remove <id>               take it out; what it kept stays',
    '                         enable|disable <id>       a row, never a rename',
    '                         new, try                  the kit a plugin is made with is',
    '                                                   `daoris-driver plugins new|try`,',
    '                                                   which starts plugins as the driver does',
  ],
  // The family is Daoris's driver program's whole since PUSH1f2f (D194 point 1, the alone-path design §5): `daoris-driver plugin` with
  // the same words, every verb, the program saying its usage for one it does not have. This writes nothing before it starts. The
  // program is the verb: the running Daoris's client where a route takes the change, its own act under the home's lease where none
  // does or no Daoris runs, and an add over an installed plugin its replace alone.
  run: (args) => (args.driverProgram ?? ((family, words) => runDriverProgram(family, words)))('plugin', args.argv),
};
