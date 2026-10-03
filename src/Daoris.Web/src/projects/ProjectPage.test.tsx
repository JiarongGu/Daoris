import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { COUNTS, ENGINE, NEWBIE } from './fixtures';
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
