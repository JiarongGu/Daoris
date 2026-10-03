import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { type OfferShown, pluginGroups, type PluginShown } from './catalog';
import { PluginList, PluginStrip } from './PluginList';

// The Plugins view's list (PLUGUI1b, D119 §3.1): a molecule, every state reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const plugin = (over: Partial<PluginShown> = {}): PluginShown => ({
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
  enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider', 'session/ended'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  ...over,
});

const OFFER: OfferShown = {
  id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: 'Daoris\'s browser for every session.',
  problem: null, harnesses: [], points: [], servers: ['browser'], needs: [], installed: false,
};

const GROUPS = pluginGroups([
  plugin(),
  plugin({ id: 'future', name: 'Future', version: '', problem: 'needs plugin API 99', harnesses: [], points: [], running: false }),
  plugin({ id: 'quiet', name: 'Quiet hours', enabled: false, running: false, harnesses: [], points: ['quest/consider'] }),
], [OFFER]);

const headings = () => screen.getAllByRole('heading').map((heading) => heading.textContent);

const SVG = `data:image/svg+xml;base64,${btoa('<svg xmlns="http://www.w3.org/2000/svg"/>')}`;

describe('the plugin list', () => {
  /** A catalogue (D140 §2): the installed, waiting on you first, then on, then off; then Daoris's own not installed. */
  it('is a catalogue: the installed with their count, waiting on you first, then the offers', () => {
    render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(headings()).toEqual(['Installed (3)', "Daoris's own plugins (1)"]);
    const installed = within(screen.getByRole('heading', { name: 'Installed (3)' }).closest('section')!).getAllByRole('button');
    const names = ['Future', 'Acme gate', 'Quiet hours'];
    expect(installed.map((row) => names.find((name) => row.textContent?.includes(name)))).toEqual(names);
  });

  /**
   * NAME2: 关闭 is close's word (an ask's, a quest's 已关闭), so a plugin switched off read as closed. A
   * plugin is 启用 and 停用 in Chinese, wherever its state is said.
   */
  it('names the sections and the off word in 中文 by the switch, never by close; a plugin\'s words stay as declared', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<PluginList groups={pluginGroups([
        ...GROUPS.installed, plugin({ id: 'acme.offered', name: 'Offered', source: { kind: 'offer', offer: 'acme.offered' } }),
      ], [OFFER])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);
      expect(headings()).toEqual(['已安装（4）', 'Daoris 自带的插件（1）']);
      expect(within(screen.getByRole('button', { name: /Quiet hours/ })).getByText('已停用')).toBeInTheDocument();
      expect(screen.queryByText(/关闭|开启/)).toBeNull();
      // Chrome translates; content does not (translation parity): the description is the author's.
      expect(within(screen.getByRole('button', { name: /Offered/ })).getByText('Holds quests overnight.')).toBeInTheDocument();
      expect(within(screen.getByRole('button', { name: /Offered/ })).getByText(/Daoris 自带/)).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('leaves out a section with none', () => {
    render(<PluginList groups={pluginGroups([plugin()], [])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(headings()).toEqual(['Installed (1)']);
  });

  /** A row (D140 §2): its icon, name and version, its state; what it gives in its author's words; then its meta line. */
  it('reads each row as a catalogue\'s: what it gives, then where it came from and what it adds', () => {
    render(<PluginList groups={pluginGroups([
      plugin({ source: { kind: 'folder', folder: 'C:/somewhere/checkout/gate' } }),
      plugin({ id: 'acme.bare', name: 'Bare', description: '', source: { kind: 'none' } }),
    ], [])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    const row = screen.getByRole('button', { name: /Acme gate/ });
    expect(within(row).getByText('Holds quests overnight.')).toBeInTheDocument();
    expect(within(row).getByText('from a folder · 2 points · 1 agent')).toBeInTheDocument();
    // 🔴 Never the folder's path: the row says the kind of source, and the page says where.
    expect(row.textContent).not.toContain('somewhere');
    // With no description, what it adds takes the line, and the meta line keeps its source.
    const bare = screen.getByRole('button', { name: /Bare/ });
    expect(within(bare).getByText('2 points · 1 agent')).toBeInTheDocument();
    expect(within(bare).getByText('no source recorded')).toBeInTheDocument();
  });

  it('draws each plugin\'s icon: its own where one is handed, its monogram otherwise, faint where it is off', () => {
    const { container } = render(<PluginList groups={pluginGroups([
      plugin({ icon: SVG }),
      plugin({ id: 'quiet', name: 'Quiet hours', enabled: false, running: false }),
    ], [])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Acme gate/ }).querySelector('img')!.getAttribute('src')).toBe(SVG);
    const monogram = screen.getByRole('button', { name: /Quiet hours/ }).querySelector('[data-hue]')!;
    expect(monogram.textContent).toBe('Q');
    expect(monogram.className).toContain('size-8');
    expect(monogram.className).toContain('opacity-55');
    expect(container.querySelectorAll('img')).toHaveLength(1);
  });

  /** An update waits on nobody, so it is never a state (D119 §2): a neutral pill on the meta line. */
  it('says update available on the row where its source declares something different, and only there', () => {
    render(<PluginList groups={pluginGroups([
      plugin({ source: { kind: 'offer', offer: 'acme.gate' }, update: 'waits' }),
      plugin({ id: 'acme.same', name: 'Same', source: { kind: 'folder', folder: 'C:/x' }, update: 'current' }),
    ], [])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    const waits = within(screen.getByRole('button', { name: /Acme gate/ })).getByText('update available');
    expect(waits.className).toMatch(/\bborder-line\b/);
    expect(within(screen.getByRole('button', { name: /Acme gate/ })).getByText(/^Daoris's own/)).toBeInTheDocument();
    expect(within(screen.getByRole('button', { name: /Same/ })).queryByText('update available')).toBeNull();
  });

  it('draws an offer\'s icon and what it gives, and no source, since its section says it', () => {
    render(<PluginList groups={pluginGroups([], [OFFER])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    const row = screen.getByRole('button', { name: /^In-app browser/ });
    expect(row.querySelector('[data-hue]')!.textContent).toBe('I');
    expect(within(row).getByText('Daoris\'s browser for every session.')).toBeInTheDocument();
    expect(within(row).getByText('1 server')).toBeInTheDocument();
    expect(row.textContent).not.toMatch(/Daoris's own/);
  });

  it('says its state\'s word where it has one', () => {
    render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    const row = screen.getByRole('button', { name: /Acme gate/ });
    expect(within(row).getByText('2 points · 1 agent')).toBeInTheDocument();
    expect(within(row).getByText('1.2.0')).toBeInTheDocument();
    expect(within(row).getByText('running')).toBeInTheDocument();
    // A refused plugin waits on the person: its word wears the waiting hue, never an outcome's (D119 §2).
    const refused = within(screen.getByRole('button', { name: /Future/ })).getByText('refused');
    expect(refused.className).toMatch(/\bborder-st-open\b/);
    expect(within(screen.getByRole('button', { name: /Quiet hours/ })).getByText('off')).toBeInTheDocument();
  });

  /** PLUG10 (P9) carried over: running is a state, so its word is the quiet neutral, never done's green. */
  it('says running in the neutral tone', () => {
    render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    const running = within(screen.getByRole('button', { name: /Acme gate/ })).getByText('running');
    expect(running.className).toMatch(/\bborder-line\b/);
    expect(running.className).not.toMatch(/st-done|st-taken/);
  });

  it('chooses a plugin by its id, and an offer as one', async () => {
    const onChoose = vi.fn();
    render(<PluginList groups={GROUPS} chosen="acme.gate" onChoose={onChoose} onInstall={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Acme gate/ })).toHaveAttribute('aria-current', 'true');
    await userEvent.click(screen.getByRole('button', { name: /Quiet hours/ }));
    expect(onChoose).toHaveBeenLastCalledWith('quiet');
    await userEvent.click(screen.getByRole('button', { name: /In-app browser/ }));
    expect(onChoose).toHaveBeenLastCalledWith('offer:in-app-browser');
  });

  it('carries an offer\'s Install on its row, beside the row and not inside it', async () => {
    const onInstall = vi.fn();
    render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={onInstall} />);

    const install = screen.getByRole('button', { name: 'Install in-app-browser' });
    expect(install.closest('button[aria-current], button[data-row]')).toBeNull();
    await userEvent.click(install);
    expect(onInstall).toHaveBeenCalledWith('in-app-browser');
  });

  it('offers no Install for an offer that cannot be installed as it stands', () => {
    render(<PluginList groups={pluginGroups([], [{ ...OFFER, problem: 'needs a newer Daoris' }])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(screen.queryByRole('button', { name: 'Install in-app-browser' })).toBeNull();
  });

  it('says the sentence in place where it has never had an answer', () => {
    render(<PluginList groups={pluginGroups([], [])} chosen={null} unanswered="This machine did not answer in time." onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(screen.getByText('This machine did not answer in time.')).toBeInTheDocument();
  });

  it('marks each row of its list, so the list\'s arrows move along them', () => {
    const { container } = render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(container.querySelectorAll('[data-list-row]')).toHaveLength(4);
  });
});

describe("the plugin list's strip", () => {
  it('marks each installed plugin by its icon: waiting wears the waiting mark, off its icon faint, on none', () => {
    const { container } = render(<PluginStrip groups={GROUPS} chosen="quiet" onChoose={vi.fn()} />);

    const marks = screen.getAllByRole('button');
    expect(marks.map((mark) => mark.getAttribute('aria-label'))).toEqual(['Future · refused', 'Acme gate · running', 'Quiet hours · off']);
    expect(marks[0]!.querySelector('.bg-st-open')).not.toBeNull();
    expect(marks[1]!.querySelector('[class*="bg-"][aria-hidden].rounded-full')).toBeNull();
    // Each mark is the plugin's icon at the strip's size, its monogram here (D140 §2).
    expect(marks.map((mark) => mark.querySelector('[data-hue]')?.textContent)).toEqual(['F', 'A', 'Q']);
    expect(marks[1]!.querySelector('[data-hue]')!.className).toContain('size-5');
    expect(marks[2]!.querySelector('.opacity-55')).not.toBeNull();
    expect(marks[2]).toHaveAttribute('aria-current', 'true');
    // Offers are not on the strip: the strip holds what this machine has.
    expect(container.textContent).not.toMatch(/In-app/);
  });

  /** CTX1 (D138, design §4): a plugin's row offers what it does: opening it, an offer's Install, and its id. */
  it('offers Open, an offer’s Install and its id on a right-click', async () => {
    const copy = vi.fn();
    const onChoose = vi.fn();
    const onInstall = vi.fn();
    render(<PluginList groups={GROUPS} chosen={null} onChoose={onChoose} onInstall={onInstall} />);
    render(<ContextMenus doors={{ copy }} />);

    rightClick(screen.getByRole('button', { name: /Acme gate/ }));
    expect(await menuActs('Actions for Acme gate')).toEqual(['Open', 'Copy plugin ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Copy plugin ID' }));
    expect(copy).toHaveBeenCalledWith('acme.gate');

    rightClick(screen.getByRole('button', { name: /^In-app browser/ }));
    expect(await menuActs('Actions for In-app browser')).toEqual(['Open', 'Install', 'Copy plugin ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Install' }));
    expect(onInstall).toHaveBeenCalledWith('in-app-browser');

    rightClick(screen.getByRole('button', { name: /^In-app browser/ }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Open' }));
    expect(onChoose).toHaveBeenCalledWith('offer:in-app-browser');
  });

  it('chooses the plugin a mark names', async () => {
    const onChoose = vi.fn();
    render(<PluginStrip groups={GROUPS} chosen={null} onChoose={onChoose} />);

    await userEvent.click(screen.getByRole('button', { name: 'Acme gate · running' }));
    expect(onChoose).toHaveBeenCalledWith('acme.gate');
  });
});
