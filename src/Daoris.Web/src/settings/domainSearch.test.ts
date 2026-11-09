import { describe, expect, it, vi } from 'vitest';
import { type CommandDoors, type CommandState, commandTable, matching, paletteCommands } from '../menus/commands';
import { catalogues } from '../locales/index';
import { SETTINGS_DOMAINS } from './domains';

const state = (domains: CommandState['domains']): CommandState => ({
  attached: true, view: 'settings', list: null, panelShown: true, sideShown: false, moved: false, workspaces: [], scope: null,
  circle: null, wired: false, theme: 'system', language: 'en', agents: [], domains, knowledge: 'search', session: null, quest: null,
  record: false, find: false, field: false, refreshing: false, zoom: 100,
});

/**
 * Every row of Settings is reached by its own words: typed into the palette, a card's or a row's name in the reader's
 * language finds the command that opens the domain holding it. The machine log is Help's own *Machine log*, so it has no
 * rows of its own here.
 */
describe("Settings' rows, found by the palette", () => {
  for (const [language, catalogue] of Object.entries(catalogues)) {
    it(`open the domain each is on, by its words in ${language}`, () => {
      const t = (key: string, values?: Record<string, unknown>) =>
        (catalogue[key] ?? key).replace(/\{\{(\w+)\}\}/g, (_, name: string) => String(values?.[name] ?? ''));
      const settings = vi.fn();
      const doors = new Proxy({}, { get: (_, name) => (name === 'settings' ? settings : vi.fn()) }) as CommandDoors;
      const commands = paletteCommands(commandTable(state(SETTINGS_DOMAINS.map(({ id, label, rows }) => ({ id, label, rows }))), doors, t), t);

      for (const domain of SETTINGS_DOMAINS.filter((each) => each.id !== 'logs')) {
        expect(domain.rows.length, domain.id).toBeGreaterThan(0);
        for (const row of domain.rows) {
          expect(catalogue[row], `${language}: ${row}`).toBeTruthy();
          const found = matching(commands, catalogue[row]!).map((command) => command.id);
          expect(found, `${language}: ${domain.id} by ${catalogue[row]}`).toContain(`workspace.domain:${domain.id}`);
        }
        commands.find((command) => command.id === `workspace.domain:${domain.id}`)!.run();
        expect(settings).toHaveBeenLastCalledWith(domain.id);
      }
    });
  }
});
