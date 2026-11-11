import { createContext, useContext } from 'react';
import type { MenuAct } from '../design/ui';
import { authoredText } from '../locales/authored';
import { askCommand } from './commandAsks';
import { interfacesOf, switchedOn, switchesOf } from './interfaces';

/** The kinds of record a command's `on` names, as a menu is opened on one. */
export type RecordKind = 'session' | 'quest' | 'run' | 'repository' | 'workspace';

/**
 * The acts a plugin's commands add to a record's menu, from a `PLUGINS` answer: each command of a plugin that is on and sound, whose
 * commands are on at this machine, that declares the record's kind in its `on`, headed by its plugin's name. Choosing one opens the
 * plugin's page at the command's round card with the record's id in the argument named for its kind, where the command declares one.
 */
export function recordCommandActs(catalog: { plugins?: unknown } | undefined, kind: RecordKind, id: string, language: string): MenuAct[] {
  const plugins = Array.isArray(catalog?.plugins) ? catalog.plugins as Record<string, unknown>[] : [];
  return plugins
    .filter((plugin) => typeof plugin?.id === 'string' && plugin.enabled === true && !plugin.problem
      && switchedOn(switchesOf(plugin.switches), 'commands', 'machine'))
    .flatMap((plugin) => interfacesOf(plugin.interfaces).commands
      .filter((command) => command.on.includes(kind))
      .map((command): MenuAct => {
        const name = typeof plugin.name === 'string' && plugin.name ? plugin.name : plugin.id as string;
        const given = command.args.some((arg) => arg.name === kind) ? { [kind]: id } : undefined;
        return {
          id: `plugin:${plugin.id as string}/${command.name}`,
          label: `${name}: ${authoredText(command.title, language)}`,
          icon: 'plug',
          onSelect: () => askCommand(plugin.id as string, command.name, given),
        };
      }));
}

/** The window's source of those acts: held once where the catalogue is read, so a row asks and draws without its own query. */
export const PluginRecordActs = createContext<(kind: RecordKind, id: string) => MenuAct[]>(() => []);

/** The plugin acts for one record's menu; none where the window offers none. */
export const usePluginRecordActs = (kind: RecordKind, id: string): MenuAct[] => useContext(PluginRecordActs)(kind, id);
