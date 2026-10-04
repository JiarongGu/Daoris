import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { COUNTS, ENGINE, LINE, NEWBIE, SETUP_DEFAULTS } from './fixtures';
import { ProjectPage } from './ProjectPage';

// CTX1 (D138, design §4): a repository's page on a right-click offers its header's acts, *Open code map* and *Manage*, as
// its buttons press them, then its name. A molecule, so each is reached by passing it.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe("a repository's page on a right-click", () => {
  afterEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('en');
  });

  it('offers Open code map and Manage, as its header does, then its name', async () => {
    const copy = vi.fn();
    const onOpenCode = vi.fn();
    const onManage = vi.fn();
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} onOpenCode={onOpenCode} onManage={onManage} />);
    render(<ContextMenus doors={{ copy }} />);

    rightClick(screen.getByRole('heading', { level: 1, name: 'engine' }));
    expect(await menuActs('Actions for engine')).toEqual(['Open code map', 'Manage', 'Copy repository name']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Open code map' }));
    expect(onOpenCode).toHaveBeenCalledOnce();

    rightClick(screen.getByRole('heading', { level: 1, name: 'engine' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Manage' }));
    expect(onManage).toHaveBeenCalledOnce();

    rightClick(screen.getByRole('heading', { level: 1, name: 'engine' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy repository name' }));
    expect(copy).toHaveBeenCalledWith('engine');
  });

  it('offers only its name where its header offers nothing (a browser, one not adopted)', async () => {
    render(<ProjectPage registration={NEWBIE} />);
    render(<ContextMenus doors={{ copy: vi.fn() }} />);
    rightClick(screen.getByRole('heading', { level: 1 }));
    expect(await menuActs()).toEqual(['Copy repository name']);
  });

  it('names them in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} onOpenCode={vi.fn()} onManage={vi.fn()} />);
    render(<ContextMenus doors={{ copy: vi.fn() }} />);
    rightClick(screen.getByRole('heading', { level: 1, name: 'engine' }));
    expect(await menuActs()).toEqual(['打开代码地图', '管理', '复制仓库名称']);
  });
});

// UX6f (D150 §4.2): a repository's page is Details and Setup, the tab shown its holder's to remember. A browser gets
// Details alone, with no tab row (D47 §4); a setting shown on Details is read-only, with a door to its home on Setup.
describe("a repository's tabs", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('shows Details, then Setup on its tab, each the tab its holder chose', async () => {
    const onTab = vi.fn();
    const { rerender } = render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} line={LINE} setup={SETUP_DEFAULTS} tab="details" onTab={onTab} />);

    const tabs = screen.getByRole('tablist', { name: "engine's pages" });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Setup']);
    expect(screen.getByRole('tabpanel', { name: 'Details' })).toHaveTextContent('Owns');
    await userEvent.click(within(tabs).getByRole('tab', { name: 'Setup' }));
    expect(onTab).toHaveBeenLastCalledWith('setup');

    rerender(<Tooltip.Provider><ProjectPage registration={ENGINE} counts={COUNTS.engine} line={LINE} setup={SETUP_DEFAULTS} tab="setup" onTab={onTab} /></Tooltip.Provider>);
    const panel = screen.getByRole('tabpanel', { name: 'Setup' });
    expect(within(panel).getByRole('button', { name: 'Line and landing' })).toBeInTheDocument();
    expect(panel).not.toHaveTextContent('Owns');
  });

  it("shows its line read-only on Details, with a door that opens Setup at Line and landing", async () => {
    const onTab = vi.fn();
    const { rerender } = render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} line={LINE} setup={SETUP_DEFAULTS} tab="details" onTab={onTab} />);

    const details = screen.getByRole('tabpanel', { name: 'Details' });
    expect(within(details).getByText('develop', { selector: 'code' })).toBeInTheDocument();
    expect(within(details).queryByRole('textbox')).toBeNull();
    await userEvent.click(within(details).getByRole('button', { name: 'Change in Setup' }));
    expect(onTab).toHaveBeenLastCalledWith('setup');

    rerender(<Tooltip.Provider><ProjectPage registration={ENGINE} counts={COUNTS.engine} line={LINE} setup={SETUP_DEFAULTS} tab="setup" onTab={onTab} /></Tooltip.Provider>);
    expect(screen.getByRole('button', { name: 'Line and landing' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('keeps the section its holder asked open, where no door on Details asked for another', () => {
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} setup={{ ...SETUP_DEFAULTS, open: 'reach' }} tab="setup" onTab={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Reach' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('is Details alone in a browser, with no tab row and no setting', () => {
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} />);
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(screen.getByText('Owns')).toBeInTheDocument();
    expect(screen.queryByLabelText('Drive on this machine')).toBeNull();
  });

  it('names its tabs in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} setup={SETUP_DEFAULTS} tab="details" onTab={vi.fn()} />);
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['详情', '配置']);
  });
});
