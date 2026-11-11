import { useSyncExternalStore } from 'react';
import { authoredText } from '../locales/authored';
import type { PluginCommandRow } from '../menus/pluginCommands';
import { interfacesOf, switchedOn, switchesOf } from './interfaces';

/**
 * The commands the palette offers from a `PLUGINS` answer: each of a plugin that is on and sound, whose commands are on at this
 * machine, titled in the viewer's language.
 */
export function pluginCommandRows(catalog: { plugins?: unknown } | undefined, language: string): PluginCommandRow[] {
  const plugins = Array.isArray(catalog?.plugins) ? catalog.plugins as Record<string, unknown>[] : [];
  return plugins
    .filter((plugin) => typeof plugin?.id === 'string' && plugin.enabled === true && !plugin.problem
      && switchedOn(switchesOf(plugin.switches), 'commands', 'machine'))
    .flatMap((plugin) => interfacesOf(plugin.interfaces).commands.map((command) => ({
      plugin: plugin.id as string,
      pluginName: typeof plugin.name === 'string' && plugin.name ? plugin.name : plugin.id as string,
      command: command.name,
      title: authoredText(command.title, language),
    })));
}

/** A command a door outside the Plugins view asked to run: the plugin's id, the command's name, and the ask's number. */
export type CommandAsked = { plugin: string; command: string; n: number };

let asked: CommandAsked | null = null;
let count = 0;
const listeners = new Set<() => void>();

/**
 * The palette's door onto a plugin's command: the ask is kept for the Plugins view, which opens the plugin's page and its round
 * card there, since the card belongs to the page whatever door opened it.
 */
export function askCommand(plugin: string, command: string): void {
  count += 1;
  asked = { plugin, command, n: count };
  listeners.forEach((listener) => listener());
}

/** The view took the ask, so a later look at the view does not run it twice. */
export function tookCommand(n: number): void {
  if (asked?.n !== n) return;
  asked = null;
  listeners.forEach((listener) => listener());
}

export const useAskedCommand = (): CommandAsked | null => useSyncExternalStore(
  (listener) => {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  () => asked,
);
