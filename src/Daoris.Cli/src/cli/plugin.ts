import type { CliCommand } from '../platform/types.ts';
import { daorisHome } from '../platform/home.ts';
import { commandPlugin } from '../plugins/plugins.ts';
import { instanceDoor } from '../service/service.ts';

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
  // RPC1f: a change is the Plugins place's route while Daoris runs on this home, through the one module that reaches its pipe,
  // and this verb's own act under the home's lease when none does.
  run: (args) => commandPlugin(args, instanceDoor({ home: daorisHome(), write: args.write })),
};
