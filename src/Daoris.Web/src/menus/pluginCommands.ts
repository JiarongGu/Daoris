import type { CommandDoors, CommandState, Translate } from './commands';

/** A plugin's command the palette offers: its plugin's id and name, the command's name, and its title in the viewer's language. */
export type PluginCommandRow = { plugin: string; pluginName: string; command: string; title: string };

/**
 * Each installed plugin's commands, the palette's alone, grouped by the plugin's name and found by its id and the command's name
 * too. A row opens the plugin's page at the command's round card, whichever door it came by.
 */
export const PLUGIN_COMMANDS = {
  family: 'run.pluginCommand', menu: 'run' as const, group: 'archive', after: 'run.archiveEnded',
  rows: (state: CommandState, t: Translate) => (state.pluginCommands ?? []).map((row) => ({
    id: `run.pluginCommand:${row.plugin}/${row.command}`, label: `${row.pluginName}: ${row.title}`,
    title: t('command.pluginCommand', { plugin: row.pluginName, command: row.title }), icon: 'plug' as const, shell: true as const,
    paletteOnly: true as const, keywords: `plugin 插件 ${row.plugin} ${row.command}`,
    run: (doors: CommandDoors) => doors.pluginCommand?.(row.plugin, row.command),
  })),
};
