import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { COUNTS, ENGINE, GIT_BRANCHES_ENGINE, LINE, NEWBIE } from './fixtures';
import { ProjectPage } from './ProjectPage';
import { BranchesTab, HistoryTab } from './git/RepositoryGit';

// A repository's page on a right-click offers its header's acts, *Open code map* and *Manage*, as
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

// A repository's page is Details, and the tabs its holder hands bodies for. A browser gets Details alone, with no tab row;
// its line is read-only, with a door to the Agents place's page where it is set.
describe("a repository's tabs", () => {
  it("shows its line read-only on Details, with a door that opens the place's page", async () => {
    const onOpenSetup = vi.fn();
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} line={LINE} onOpenSetup={onOpenSetup} />);

    expect(screen.getByText('develop', { selector: 'code' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Change in Setup' }));
    expect(onOpenSetup).toHaveBeenCalledOnce();
  });

  it('is Details alone in a browser, with no tab row and no setting', () => {
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} />);
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(screen.getByText('Owns')).toBeInTheDocument();
    expect(screen.queryByLabelText('Drive on this machine')).toBeNull();
  });
});

// UX6h1: Git is inside Repositories, as a repository's Branches and History tabs after Details, a shell's
// like Setup. Each tab's body is its holder's, which reads only while the tab shows.
describe("a repository's Branches and History tabs", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const branches = <BranchesTab repository="engine" read={{ state: 'read', value: GIT_BRANCHES_ENGINE }} />;
  const history = <HistoryTab repository="engine" read={{ state: 'noCheckout' }} />;

  it('sit after Details, and show what their holder hands each', async () => {
    const onTab = vi.fn();
    const { rerender } = render(<ProjectPage registration={ENGINE} branches={branches} history={history} tab="details" onTab={onTab} />);
    const tabs = screen.getByRole('tablist', { name: "engine's pages" });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'History']);
    await userEvent.click(within(tabs).getByRole('tab', { name: 'History' }));
    expect(onTab).toHaveBeenLastCalledWith('history');

    rerender(
      <Tooltip.Provider>
        <ProjectPage registration={ENGINE} branches={branches} history={history} tab="branches" onTab={onTab} />
      </Tooltip.Provider>,
    );
    expect(within(screen.getByRole('tabpanel', { name: 'Branches' })).getByRole('region', { name: "engine's branches" })).toBeInTheDocument();

    rerender(
      <Tooltip.Provider>
        <ProjectPage registration={ENGINE} branches={branches} history={history} tab="history" onTab={onTab} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('tabpanel', { name: 'History' })).toHaveTextContent('No checkout on this machine');
  });

  it('shows Details for a remembered Branches or History tab on a page handed none, and has neither tab there', () => {
    const { rerender } = render(<ProjectPage registration={ENGINE} tab="branches" onTab={vi.fn()} />);
    expect(screen.queryByRole('tab')).toBeNull();
    expect(screen.getByText('Owns')).toBeInTheDocument();
    rerender(<Tooltip.Provider><ProjectPage registration={ENGINE} tab="history" onTab={vi.fn()} /></Tooltip.Provider>);
    expect(screen.getByText('Owns')).toBeInTheDocument();
  });

  it('are absent in a browser, which gets Details alone, wherever the view was left', () => {
    render(<ProjectPage registration={ENGINE} counts={COUNTS.engine} tab="history" />);
    expect(screen.queryByRole('tab')).toBeNull();
    expect(screen.queryByText('No checkout on this machine')).toBeNull();
    expect(screen.getByText('Owns')).toBeInTheDocument();
  });

  it('name the three tabs in 中文, each a short word', async () => {
    await i18n.changeLanguage('zh');
    render(<ProjectPage registration={ENGINE} branches={branches} history={history} tab="details" onTab={vi.fn()} />);
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['详情', '分支', '历史']);
  });
});
