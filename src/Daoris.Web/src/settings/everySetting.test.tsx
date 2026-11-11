import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../locales/i18n';
import { SETTINGS } from '../bridge/generated/routes';
import { catalogues, en } from '../locales/index';
import { DriverCards } from './DriverCards';
import { settingOf } from './DriverDomain';

/**
 * The page's half of the settings census: the page reads the machine's settings from the one list the driver declares
 * (`SETTINGS`, written from it), so a setting has one key. Each key and route is listed once; each setting the Driver domain draws
 * is worded under its key in every catalogue, and drawn there by its label.
 */
describe('every machine setting', () => {
  const driver = SETTINGS.filter((setting) => setting.card === 'driver');

  it('is listed once, by one key and one route, and the page asks the list for it', () => {
    expect(new Set(SETTINGS.map((setting) => setting.key)).size).toBe(SETTINGS.length);
    expect(new Set(SETTINGS.map((setting) => setting.route)).size).toBe(SETTINGS.length);
    for (const setting of SETTINGS) {
      expect(setting.words).toBe(`settings.${setting.key}`);
      expect(setting.route).toBe(`SET_${setting.key.toUpperCase()}`);
    }
    expect(settingOf('quiet')).toMatchObject({ route: 'SET_QUIET', least: 5, most: 120 });
    expect(settingOf('manifest')).toMatchObject({ route: 'SET_MANIFEST', least: 1, most: 60 });
    expect(() => settingOf('timeout')).toThrow();
  });

  it('the Driver domain draws is worded under its key in every catalogue', () => {
    expect(driver.map((setting) => setting.key)).toEqual(expect.arrayContaining(['notify', 'cooloff', 'quiet', 'manifest', 'cap', 'terminal']));
    for (const [language, catalogue] of Object.entries(catalogues)) {
      for (const setting of driver) {
        for (const part of ['label', 'terminal', 'body']) {
          expect(catalogue[`${setting.words}.${part}`], `${language}: ${setting.words}.${part}`).toBeTruthy();
        }
      }
    }
  });

  it('the Driver domain draws is drawn there by its label', () => {
    render(
      <Tooltip.Provider>
        <DriverCards
          facts={{ pluginsFolder: null, notify: true, coolOff: 60, quiet: 10, manifestFiles: 250000, manifestMinutes: 5, cap: 2, terminal: 'ask' }}
          minutes={{ cooloff: settingOf('cooloff'), quiet: settingOf('quiet'), manifest: settingOf('manifest') }}
          busy={{ access: false }}
          onGo={vi.fn()}
          onNotify={vi.fn()}
          onCoolOff={vi.fn()}
          onQuiet={vi.fn()}
          onManifest={vi.fn()}
          onCap={vi.fn()}
          onTerminal={vi.fn()}
          onAccess={vi.fn()}
        />
      </Tooltip.Provider>,
    );

    for (const setting of driver) {
      expect(screen.queryAllByText(en[`${setting.words}.label`]!).length, setting.key).toBeGreaterThan(0);
    }
  });
});
