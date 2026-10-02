import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
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

describe('the plugin list', () => {
  it('groups by what each plugin needs from the person, each group with its count, then the offers', () => {
    render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(headings()).toEqual(['Waiting on you (1)', 'On (1)', 'Off (1)', "Daoris's own plugins (1)"]);
  });

  /**
   * NAME2: 关闭 is close's word (an ask's, a quest's 已关闭), so a plugin switched off read as closed. A
   * plugin is 启用 and 停用 in Chinese, wherever its state is said.
   */
  it('names the groups and the off word in 中文 by the switch, never by close', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<PluginList groups={GROUPS} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);
      expect(headings()).toEqual(['等你处理（1）', '已启用（1）', '已停用（1）', 'Daoris 自带的插件（1）']);
      expect(within(screen.getByRole('button', { name: /Quiet hours/ })).getByText('已停用')).toBeInTheDocument();
      expect(screen.queryByText(/关闭|开启/)).toBeNull();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('leaves out a group with none', () => {
    render(<PluginList groups={pluginGroups([plugin()], [])} chosen={null} onChoose={vi.fn()} onInstall={vi.fn()} />);

    expect(headings()).toEqual(['On (1)']);
  });

  it('says what each plugin adds as fragments, and its state\'s word where it has one', () => {
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
  it('marks each installed plugin by its initial: waiting wears the waiting mark, off its initial faint, on none', () => {
    const { container } = render(<PluginStrip groups={GROUPS} chosen="quiet" onChoose={vi.fn()} />);

    const marks = screen.getAllByRole('button');
    expect(marks.map((mark) => mark.getAttribute('aria-label'))).toEqual(['Future · refused', 'Acme gate · running', 'Quiet hours · off']);
    expect(marks[0]!.querySelector('.bg-st-open')).not.toBeNull();
    expect(marks[1]!.querySelector('[class*="bg-"][aria-hidden].rounded-full')).toBeNull();
    expect(marks[2]!.querySelector('.text-ink-faint')).not.toBeNull();
    expect(marks[2]).toHaveAttribute('aria-current', 'true');
    // Offers are not on the strip: the strip holds what this machine has.
    expect(container.textContent).not.toMatch(/In-app/);
  });

  it('chooses the plugin a mark names', async () => {
    const onChoose = vi.fn();
    render(<PluginStrip groups={GROUPS} chosen={null} onChoose={onChoose} />);

    await userEvent.click(screen.getByRole('button', { name: 'Acme gate · running' }));
    expect(onChoose).toHaveBeenCalledWith('acme.gate');
  });
});
