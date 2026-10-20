import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import type { PluginShown } from '../plugins/catalog';
import { PluginSettings } from './PluginSettings';

// PLUGSET1 (the modes design §4.1): Settings' Plugins section lists each installed plugin with what it holds set and leads to
// its page, where a plugin's settings are changed; it changes nothing itself.

const plugin = (extra: Partial<PluginShown> & Pick<PluginShown, 'id' | 'name'>): PluginShown => ({
  version: '1.0.0', description: '', enabled: true, problem: null, harnesses: [], points: [], running: false,
  folder: `C:/somewhere/plugins/${extra.id}`, data: `C:/somewhere/plugins/.data/${extra.id}`, ...extra,
});

const BROWSER = plugin({
  id: 'in-app-browser', name: 'In-app browser',
  helpServers: [{ name: 'browser', allow: ['browser_snapshot'], writes: ['browser_click', 'browser_type'], state: 'approved', accepted: ['browser_click'] }],
});
const LANDING = plugin({ id: 'github-pull-request', name: 'GitHub pull request', enabled: false });
const GATE = plugin({
  id: 'acme.gate', name: '', running: true,
  helpServers: [
    { name: 'gate', allow: ['look'], state: 'waits', changed: true },
    { name: 'notes', allow: [], state: 'waits' },
    { name: 'broken', allow: [], state: 'refused', refusal: 'its command is not found' },
  ],
});

const draw = (plugins: PluginShown[]) => {
  const onOpen = vi.fn();
  render(<Tooltip.Provider><PluginSettings plugins={plugins} onOpen={onOpen} /></Tooltip.Provider>);
  return { onOpen, section: within(screen.getByRole('region', { name: 'Installed plugins' })) };
};

describe("Settings' Plugins section", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says an install with none has none, and offers no door', () => {
    const { section } = draw([]);
    expect(section.getByText('No plugin is installed here.')).toBeTruthy();
    expect(section.queryByRole('button')).toBeNull();
  });

  it("names one plugin's approvals and the writes accepted, and its door opens its page", async () => {
    const { section, onOpen } = draw([BROWSER]);
    expect(section.getByText('In-app browser')).toBeTruthy();
    expect(section.getByText('browser', { selector: 'code' })).toBeTruthy();
    expect(section.getByText('approved for Ask Daoris')).toBeTruthy();
    // Only the writes the person accepted are named as accepted.
    expect(section.getByText('Writes outside Daoris with browser_click, accepted')).toBeTruthy();

    await userEvent.click(section.getByRole('button', { name: "Open In-app browser's page" }));
    expect(onOpen).toHaveBeenCalledWith('in-app-browser');
  });

  it('lists several, each with its own word: off, running, a server changed since approved, waiting and refused', async () => {
    const { section, onOpen } = draw([BROWSER, LANDING, GATE]);
    expect(section.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual([
      "Open In-app browser's page", "Open GitHub pull request's page", "Open acme.gate's page",
    ]);
    // A plugin with no server holds nothing set but its switch, and says so; off is its pill.
    expect(section.getByText('Holds nothing set but whether it is on.')).toBeTruthy();
    expect(section.getByText('off')).toBeTruthy();
    // A plugin with no name is named by its id.
    expect(section.getByText('acme.gate')).toBeTruthy();
    expect(section.getByText('running')).toBeTruthy();
    expect(section.getByText('changed since you approved it: asks again')).toBeTruthy();
    expect(section.getByText('not approved for Ask Daoris')).toBeTruthy();
    expect(section.getByText('refused for Ask Daoris')).toBeTruthy();

    await userEvent.click(section.getByRole('button', { name: "Open acme.gate's page" }));
    expect(onOpen).toHaveBeenCalledWith('acme.gate');
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<Tooltip.Provider><PluginSettings plugins={[BROWSER, LANDING]} onOpen={vi.fn()} /></Tooltip.Provider>);
    const section = within(screen.getByRole('region', { name: '已安装的插件' }));
    expect(section.getByText('已为问道衍批准')).toBeTruthy();
    expect(section.getByText('除是否开启外没有别的设置。')).toBeTruthy();
    expect(section.getByRole('button', { name: '打开 In-app browser 的页面' })).toBeTruthy();
  });

  it('says none in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<Tooltip.Provider><PluginSettings plugins={[]} onOpen={vi.fn()} /></Tooltip.Provider>);
    expect(screen.getByText('这里没有安装插件。')).toBeTruthy();
  });
});
