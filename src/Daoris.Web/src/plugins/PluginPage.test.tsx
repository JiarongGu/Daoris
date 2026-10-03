import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { code } from '../test/code';
import { cappedBlocks } from '../test/measure';
import type { PluginShown } from './catalog';
import { PluginMainNotice, PluginPage } from './PluginPage';

// A plugin's page (PLUGUI1b, D119 §3.2) on today's answers: a molecule, every state reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const PLUGIN: PluginShown = {
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
  enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider', 'session/ended'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  source: { kind: 'folder', folder: 'C:/somewhere/checkouts/plugins/acme.gate' },
};

const KIT = [
  { name: 'quest/consider', kind: 'decision' },
  { name: 'session/ended', kind: 'observation' },
  { name: 'work/land', kind: 'act' },
];

function page(over: Partial<Parameters<typeof PluginPage>[0]> = {}) {
  const calls = {
    onSwitch: vi.fn(), onTry: vi.fn(), onAskUpdate: vi.fn(), onApplyUpdate: vi.fn(), onCancelUpdate: vi.fn(),
    onAskRemove: vi.fn(), onRemove: vi.fn(), onCancelRemove: vi.fn(),
  };
  const view = render(<PluginPage plugin={PLUGIN} kitPoints={KIT} canTry {...calls} {...over} />);
  return { ...view, ...calls };
}

const header = () => screen.getByRole('heading', { level: 1 }).closest('header')!;
const acts = () => within(header()).getAllByRole('button').map((button) => button.textContent);

describe("a plugin's page", () => {
  it('heads with its name, its version, its state\'s word, its id and its one line', () => {
    page();

    expect(screen.getByRole('heading', { level: 1, name: 'Acme gate' })).toBeInTheDocument();
    expect(within(header()).getByText('1.2.0')).toBeInTheDocument();
    expect(within(header()).getByText('acme.gate')).toBeInTheDocument();
    const line = within(header()).getByText('Holds quests overnight.');
    // NAME2: its description is content, read whole; the install showed it cut to one line with an ellipsis. D140 §2:
    // it takes the pane's width, as every block of every page does (D141).
    expect(line).not.toHaveClass('truncate');
    expect(line.className).not.toMatch(/\bmax-w-/);
    expect(line).not.toHaveAttribute('title');
    // PLUG10 (P9), as D119 §2 gives it: running is the quiet neutral, never done's green.
    expect(within(header()).getByText('running').className).toMatch(/\bborder-line\b/);
  });

  /** D140 §2: the header leads with the plugin's icon at 48 px, its own or its monogram, beside the title. */
  it('leads its header with its icon: its own where handed, its monogram otherwise', () => {
    const svg = `data:image/svg+xml;base64,${btoa('<svg xmlns="http://www.w3.org/2000/svg"/>')}`;
    const { unmount } = page({ plugin: { ...PLUGIN, icon: svg } });
    expect(header().querySelector('img')!.getAttribute('src')).toBe(svg);
    expect(header().querySelector('img')!.className).toContain('size-12');
    unmount();

    page();
    const monogram = header().querySelector('[data-hue]')!;
    expect(monogram.textContent).toBe('A');
    expect(monogram.className).toContain('size-12');
    // Decoration: the title is the page's name, whole.
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Acme gate');
  });

  /**
   * D140 §2, from the install (LAYOUT11, D141): the detail's blocks share one width, the pane's. A paragraph capped at
   * 65ch beside a line that ran the pane's width wrapped the page at two edges, and in 中文 held about 32 glyphs. An
   * update's refused plan, drawn by Settings' `PluginUpdatePlan`, kept the cap after the page's own went.
   */
  it('lets every block of its detail take the pane\'s width: no paragraph keeps a measure of its own', () => {
    const { container } = page({
      plugin: { ...PLUGIN, problem: 'needs plugin API 99, and this build speaks 1.' },
      plan: { id: PLUGIN.id, applied: false, refusal: 'The source asks for plugin API 99, and this build speaks 1.', changes: [] },
    });

    expect(cappedBlocks(container)).toEqual([]);
  });

  /** An update waits on nobody, so it is a neutral pill beside the state's, never a state (D119 §2, D140 §2). */
  it('says update available beside its state where its source declares something different', () => {
    const { unmount } = page({ plugin: { ...PLUGIN, update: 'waits' } });
    expect(within(header()).getByText('update available').className).toMatch(/\bborder-line\b/);
    unmount();

    page({ plugin: { ...PLUGIN, update: 'current' } });
    expect(within(header()).queryByText('update available')).toBeNull();
  });

  /** D140 §3.1: an icon's problem never refuses the plugin; its Source says why, in the reader's sentence, verbatim. */
  it('says in its Source why its declared icon is not drawn, and nothing where it draws', () => {
    const problem = "`icon` `icon.svg` is not a file in the plugin's folder.";
    const { unmount } = page({ plugin: { ...PLUGIN, iconProblem: problem } });
    const source = screen.getByRole('region', { name: 'Source' });
    expect(source).toHaveTextContent("Its icon is not drawn: icon icon.svg is not a file in the plugin's folder.");
    expect(within(source).getByText('icon.svg').tagName).toBe('CODE');
    // Never a refusal: no warn-railed sentence leads the page.
    expect(document.querySelector('.border-warn')).toBeNull();
    unmount();

    page();
    expect(screen.getByRole('region', { name: 'Source' })).not.toHaveTextContent(/icon is not drawn/);
  });

  it('holds its four acts in order: the switch, Try, Update… and Remove…, which is the danger variant', () => {
    page();

    expect(acts()).toEqual(['Turn off', 'Try', 'Update…', 'Remove…']);
    expect(within(header()).getByRole('button', { name: 'Remove…' }).className).toMatch(/\btext-st-declined\b/);
    // None is loud by default: the plan's Update now is the one primary control.
    expect(within(header()).getAllByRole('button').some((button) => /\bbg-accent\b/.test(button.className))).toBe(false);
  });

  it('presses each act for its plugin', async () => {
    const { onSwitch, onTry, onAskUpdate, onAskRemove } = page();

    await userEvent.click(within(header()).getByRole('button', { name: 'Turn off' }));
    expect(onSwitch).toHaveBeenCalledWith('acme.gate', 'disable');
    await userEvent.click(within(header()).getByRole('button', { name: 'Try acme.gate' }));
    expect(onTry).toHaveBeenCalledWith('acme.gate');
    await userEvent.click(within(header()).getByRole('button', { name: 'Update acme.gate' }));
    expect(onAskUpdate).toHaveBeenCalledWith('acme.gate');
    await userEvent.click(within(header()).getByRole('button', { name: 'Remove…' }));
    expect(onAskRemove).toHaveBeenCalledWith('acme.gate');
  });

  /** An absent act is absent, never disabled (D119 §3.2). */
  it('offers Try only to a plugin that speaks, and Update… only to one with a source record', () => {
    page({ plugin: { ...PLUGIN, points: [], source: { kind: 'none' } } });
    expect(acts()).toEqual(['Turn off', 'Remove…']);
  });

  it('offers no Try where this shell has no kit', () => {
    page({ canTry: false });
    expect(acts()).toEqual(['Turn off', 'Update…', 'Remove…']);
  });

  it('turns on a plugin that is off', async () => {
    const { onSwitch } = page({ plugin: { ...PLUGIN, enabled: false, running: false } });
    expect(within(header()).getByText('off')).toBeInTheDocument();
    await userEvent.click(within(header()).getByRole('button', { name: 'Turn on' }));
    expect(onSwitch).toHaveBeenCalledWith('acme.gate', 'enable');
  });

  /**
   * NAME2, seen on the install in 中文: the switch read 关闭, which says *close* as much as *turn off*. Its
   * acts are 停用 and 启用, and its off state 已停用; the English pair stays *Turn off* and *Turn on*.
   */
  it('names its switch in 中文 by what it does to the plugin, never by close', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      const { unmount } = page();
      expect(acts()).toEqual(['停用', '试运行', '更新…', '移除…']);
      unmount();
      page({ plugin: { ...PLUGIN, enabled: false, running: false } });
      expect(acts()[0]).toBe('启用');
      expect(within(header()).getByText('已停用')).toBeInTheDocument();
      expect(within(header()).queryByText(/关闭|开启/)).toBeNull();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** A refused plugin's page: the driver's sentence leads, verbatim, and it has nothing to try (D119 §3.2). */
  it('leads with the driver\'s sentence for a refused plugin, on a warn rail', () => {
    page({ plugin: { ...PLUGIN, problem: 'needs plugin API 99, and this build speaks 1.', points: [], harnesses: [] } });

    const sentence = screen.getByText('needs plugin API 99, and this build speaks 1.');
    expect(sentence.closest('p')!.className).toMatch(/\bborder-warn\b/);
    expect(within(header()).getByText('refused').className).toMatch(/\bborder-st-open\b/);
    expect(acts()).not.toContain('Try');
  });

  describe('Remove…, which asks once (PLUG10, D41 §4)', () => {
    it('says what the second press does and where what it kept stays, beside Remove plugin and Never mind', async () => {
      const { onRemove, onCancelRemove } = page({ asking: true });

      const ask = screen.getByRole('group', { name: 'remove acme.gate' });
      expect(ask).toHaveTextContent('Removes its folder from this machine. What it kept stays at C:/somewhere/data/plugins/.data/acme.gate');
      // While it asks, the ask holds the move, so the first press is not offered twice.
      expect(acts()).not.toContain('Remove…');
      const moves = within(ask).getAllByRole('button');
      expect(moves.map((move) => move.textContent)).toEqual(['Remove plugin', 'Never mind']);
      expect(moves[0]!.className).toMatch(/\btext-st-declined\b/);
      await userEvent.click(moves[1]!);
      expect(onCancelRemove).toHaveBeenCalled();
      await userEvent.click(moves[0]!);
      expect(onRemove).toHaveBeenCalledWith('acme.gate');
    });
  });

  it("shows an update's plan under the header, with Update now and Not now", async () => {
    const { onApplyUpdate, onCancelUpdate } = page({
      plan: { id: 'acme.gate', applied: false, changes: [{ what: 'version', was: '1.2.0', now: '1.3.0' }] },
    });

    const plan = screen.getByRole('region', { name: 'What an update changes' });
    expect(plan).toHaveTextContent('1.2.0 → 1.3.0');
    await userEvent.click(within(plan).getByRole('button', { name: 'Update now' }));
    expect(onApplyUpdate).toHaveBeenCalledWith('acme.gate');
    await userEvent.click(within(plan).getByRole('button', { name: 'Not now' }));
    expect(onCancelUpdate).toHaveBeenCalled();
  });

  it('lists each declared point with what kind of question it is, and each agent by name', () => {
    page();

    const points = screen.getByRole('region', { name: 'Points' });
    expect(points).toHaveTextContent('quest/consider');
    expect(points).toHaveTextContent('A decision, before a planned start spends anything');
    expect(points).toHaveTextContent('An observation, after a session ends');
    expect(screen.getByRole('region', { name: 'Agents' })).toHaveTextContent('acme-agent');
  });

  /** Points and Agents appear only where the plugin declares that thing (D119 §3.2). */
  it('leaves out a section the plugin declares nothing for', () => {
    page({ plugin: { ...PLUGIN, harnesses: [] } });
    expect(screen.queryByRole('region', { name: 'Agents' })).toBeNull();
  });

  it("holds its last trial here, the trial's own sentence while it runs, and says when there is none", () => {
    const { rerender } = page();
    const tests = () => screen.getByRole('region', { name: 'Tests' });
    expect(tests()).toHaveTextContent('Not tried on this page since Daoris started.');

    const props = {
      plugin: PLUGIN, kitPoints: KIT, canTry: true, onSwitch: vi.fn(), onTry: vi.fn(), onAskUpdate: vi.fn(),
      onApplyUpdate: vi.fn(), onCancelUpdate: vi.fn(), onAskRemove: vi.fn(), onRemove: vi.fn(), onCancelRemove: vi.fn(),
    };
    rerender(<Tooltip.Provider><PluginPage {...props} trying /></Tooltip.Provider>);
    expect(tests()).toHaveTextContent(/Trying — as long as the driver would wait/);

    rerender((
      <Tooltip.Provider>
        <PluginPage
          {...props}
          trial={{
            plugin: 'acme.gate', folder: PLUGIN.folder, command: ['node', 'wire.mjs'], passed: false,
            summary: 'One check failed.', steps: [{ name: 'quest/consider', ok: false, sentence: 'It answered late.' }], said: [],
          }}
        />
      </Tooltip.Provider>
    ));
    expect(within(tests()).getByRole('region', { name: 'What try found' })).toHaveTextContent('It answered late.');
  });

  it('says a plugin that speaks at no point has nothing to try', () => {
    page({ plugin: { ...PLUGIN, points: [] } });
    expect(screen.getByRole('region', { name: 'Tests' })).toHaveTextContent('It speaks at no point, so there is nothing to try.');
  });

  it('names its data folder and where it came from, with its install folder', () => {
    page();

    expect(screen.getByRole('region', { name: 'Data folder' })).toHaveTextContent('C:/somewhere/data/plugins/.data/acme.gate');
    const source = screen.getByRole('region', { name: 'Source' });
    expect(source).toHaveTextContent('Added from C:/somewhere/checkouts/plugins/acme.gate');
    expect(source).toHaveTextContent('C:/somewhere/data/plugins/acme.gate');
  });

  it('says it has no record of where it came from, rather than guess one', () => {
    page({ plugin: { ...PLUGIN, source: { kind: 'none' } } });
    expect(screen.getByRole('region', { name: 'Source' })).toHaveTextContent('No record of where it came from');
  });

  /** The two doors stay in sight (D50): only the terminal's verbs that exist today. */
  it('ends with its terminal twins, naming it', () => {
    page();
    expect(screen.getByText(code(/daoris plugin enable\|disable\|update\|remove acme\.gate/))).toBeInTheDocument();
    expect(screen.getByText(code(/daoris-driver plugins try acme\.gate/))).toBeInTheDocument();
  });
});

describe("the plugins' main area without a page", () => {
  it('says how to choose with nothing chosen, offering what the list\'s ＋ offers', async () => {
    const onAct = vi.fn();
    render(<PluginMainNotice state="none" actions={[{ id: 'ask', label: 'Ask Daoris for a plugin' }, { id: 'make', label: 'Make a plugin…' }]} onAct={onAct} />);

    expect(screen.getByText('Choose a plugin')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Make a plugin…' }));
    expect(onAct).toHaveBeenCalledWith('make');
  });

  it('says a plugin that has gone is no longer here', () => {
    render(<PluginMainNotice state="gone" />);
    expect(screen.getByText('This plugin is no longer here')).toBeInTheDocument();
  });

  it('draws skeleton rows while it loads, never the empty state', () => {
    const { container } = render(<PluginMainNotice state="loading" />);
    expect(container.querySelector('[data-main-state="loading"]')).not.toBeNull();
    expect(screen.queryByText('Choose a plugin')).toBeNull();
  });

  it('says the sentence in place where the list has never had an answer', () => {
    render(<PluginMainNotice state="unanswered" sentence="This machine did not answer in time." />);
    expect(screen.getByText('This machine did not answer in time.')).toBeInTheDocument();
  });
});

/** CTX1 (D138, design §4): a plugin's page on a right-click offers its header's acts, as its buttons press them, then its id. */
describe("a plugin's page on a right-click", () => {
  it('offers its switch, Try, Update… and Remove…, then its id, each pressed as its button is', async () => {
    const copy = vi.fn();
    const calls = page();
    render(<ContextMenus doors={{ copy }} />);
    const onPage = () => rightClick(screen.getByRole('heading', { level: 1, name: 'Acme gate' }));

    onPage();
    expect(await menuActs('Actions for Acme gate')).toEqual(['Turn off', 'Try', 'Update…', 'Remove…', 'Copy plugin ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Turn off' }));
    expect(calls.onSwitch).toHaveBeenCalledWith('acme.gate', 'disable');

    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Remove…' }));
    expect(calls.onAskRemove).toHaveBeenCalledWith('acme.gate');

    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy plugin ID' }));
    expect(copy).toHaveBeenCalledWith('acme.gate');
  });

  it('offers no Remove… while its ask stands, and no Try where the shell has no kit', async () => {
    page({ asking: true, canTry: false, plugin: { ...PLUGIN, enabled: false, source: undefined } });
    render(<ContextMenus doors={{ copy: vi.fn() }} />);
    rightClick(screen.getByRole('heading', { level: 1, name: 'Acme gate' }));
    expect(await menuActs()).toEqual(['Turn on', 'Copy plugin ID']);
  });
});
