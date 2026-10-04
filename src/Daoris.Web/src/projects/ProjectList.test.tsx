import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Registration } from '../api';
import '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { CJK, ELSEWHERE, ENGINE, GAME, MIRRORED, NEWBIE, UNDECLARED } from './fixtures';
import { FILTER_PAST, ProjectList, type WorkspaceGroup } from './ProjectList';

// Repositories' list as a molecule (FRAME1e, D118 §2; UX6g, D150 §4.1): a group per workspace, its head a door to the
// workspace's page, its rows arriving with what each says, and every press going out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const DEFAULT: WorkspaceGroup = {
  workspace: 'default',
  adopted: [{ registration: ENGINE }, { registration: GAME }],
  outside: [{ registration: NEWBIE, entries: 3 }, { registration: ELSEWHERE, entries: null }],
};
const STUDIO: WorkspaceGroup = { workspace: 'studio', adopted: [{ registration: MIRRORED }], outside: [] };

const list = (over: Partial<Parameters<typeof ProjectList>[0]> = {}) => {
  const onChoose = vi.fn();
  const onChooseWorkspace = vi.fn();
  render(
    <ProjectList groups={[DEFAULT, STUDIO]} chosen={null} onChoose={onChoose} onChooseWorkspace={onChooseWorkspace} {...over} />,
  );
  return { onChoose, onChooseWorkspace };
};

/** A workspace's group, by its name. */
const group = (workspace: string) => screen.getByRole('region', { name: workspace });

/** Twenty-nine repositories in two workspaces, as the owner's install holds them (§0). */
const many = (): WorkspaceGroup[] => {
  const made = (name: string, adopted: boolean, workspace: string): Registration => ({
    repository: name, adopted, registered: true, workspace, owns: [], accepts: [], packs: [], entries: 0,
  });
  return [
    {
      workspace: 'forge',
      adopted: [],
      outside: [{ registration: made('anvil', false, 'forge') }, { registration: made('bellows', false, 'forge') }],
    },
    {
      workspace: 'work',
      adopted: Array.from({ length: 7 }, (_, at) => ({ registration: made(`service-${at + 1}`, true, 'work') })),
      outside: Array.from({ length: 20 }, (_, at) => ({ registration: made(`tool-${at + 1}`, false, 'work') })),
    },
  ];
};

describe("Repositories' list", () => {
  it('groups by workspace, each headed by its row, then its adopted repositories and those registered, not adopted', () => {
    list();

    expect(screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      'default 4 repositories', 'studio 1 repository',
    ]);
    const shared = within(group('default'));
    expect(shared.getAllByRole('listitem').map((item) => item.getAttribute('aria-label'))).toEqual(['engine', 'game', 'newbie', 'elsewhere']);
    expect(shared.getByRole('heading', { level: 4 })).toHaveTextContent('Registered, not adopted (2)');
    // Every row is a row of its list, a workspace's head among them, so ↑ and ↓ move along all of them.
    for (const item of screen.getAllByRole('listitem')) expect(item).toHaveAttribute('data-list-row');
    expect(screen.getAllByRole('heading', { level: 3 }).every((heading) => heading.hasAttribute('data-list-row'))).toBe(true);
  });

  /** §4.1: a workspace's row heads its group and opens the workspace's page. */
  it("opens a workspace's page from its head, and wears the choice there", async () => {
    const { onChooseWorkspace } = list({ chosenWorkspace: 'studio' });

    await userEvent.click(within(group('default')).getByRole('button', { name: 'default 4 repositories' }));
    expect(onChooseWorkspace).toHaveBeenCalledWith('default');
    expect(within(group('studio')).getByRole('button', { name: 'studio 1 repository' })).toHaveAttribute('aria-current', 'true');
  });

  it('folds a group by its chevron, its head still a door', async () => {
    list();

    await userEvent.click(screen.getByRole('button', { name: 'Fold default' }));
    expect(within(group('default')).queryByRole('listitem')).toBeNull();
    expect(screen.getByRole('button', { name: 'Unfold default' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(group('studio')).getByRole('listitem', { name: 'studio-tools' })).toBeInTheDocument();
  });

  it('chooses a repository by its name, and wears the choice on its row', async () => {
    const { onChoose } = list({ chosen: 'game' });

    await userEvent.click(within(screen.getByRole('listitem', { name: 'newbie' })).getByRole('button'));
    expect(onChoose).toHaveBeenCalledWith('newbie');
    expect(within(screen.getByRole('listitem', { name: 'game' })).getByRole('button')).toHaveAttribute('aria-current', 'true');
  });

  /** D34: adopted and undeclared is addressable, and an asker would be guessing, so its row says so. */
  it("says an adopter's summary as it wrote it, and that one declared nothing", () => {
    list({ groups: [{ workspace: 'default', adopted: [{ registration: ENGINE }, { registration: UNDECLARED }], outside: [] }] });

    expect(within(screen.getByRole('listitem', { name: 'engine' })).getByText(ENGINE.summary!)).toHaveAttribute('title', ENGINE.summary);
    expect(within(screen.getByRole('listitem', { name: 'sandbox' })).getByText('no domain declared yet')).toBeInTheDocument();
    // A group with no row is absent, never an empty heading.
    expect(screen.queryByRole('heading', { name: /Registered, not adopted/ })).toBeNull();
  });

  it('says what the index reads of one not adopted, in one sentence for nothing', () => {
    list();

    expect(within(screen.getByRole('listitem', { name: 'newbie' })).getByText('3 entries indexed read-only')).toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'elsewhere' })).getByText('nothing indexed yet')).toBeInTheDocument();
  });

  it('says its standing as the session list does: held outranks drives here', () => {
    list({
      groups: [{ workspace: 'default', adopted: [{ registration: ENGINE, drivable: true }, { registration: GAME, drivable: true, held: true }], outside: [] }],
    });

    expect(within(screen.getByRole('listitem', { name: 'engine' })).getByText('drives here')).toBeInTheDocument();
    const game = within(screen.getByRole('listitem', { name: 'game' }));
    expect(game.getByText('held')).toBeInTheDocument();
    expect(game.queryByText('drives here')).toBeNull();
  });

  /** §4.1: a row that holds Daoris's branches says how many; none, or no driver to say, says nothing. */
  it("says how many of Daoris's branches a repository holds", () => {
    list({
      groups: [{ workspace: 'default', adopted: [{ registration: ENGINE, branches: 3 }, { registration: GAME, branches: 0 }, { registration: UNDECLARED }], outside: [] }],
    });

    expect(within(screen.getByRole('listitem', { name: 'engine' })).getByText("3 branches of Daoris's")).toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: 'game' })).not.toHaveTextContent(/of Daoris's/);
    expect(screen.getByRole('listitem', { name: 'sandbox' })).not.toHaveTextContent(/of Daoris's/);
  });

  /** A repository's name and summary are content: shown as they are, never translated. */
  it('shows a 中文 name as it is', () => {
    list({ groups: [{ workspace: 'default', adopted: [{ registration: CJK }], outside: [] }] });

    expect(screen.getByRole('listitem', { name: '渲染管线' })).toHaveTextContent('渲染管线');
  });

  /**
   * §4.1: past twelve repositories the list gains a filter, as the map gained a search past twelve (MAP4a), and *Registered,
   * not adopted* starts folded under a group that has adopters. A filter shows the rows whose name holds it, and a
   * workspace whose name does, whole.
   */
  it('offers a filter past twelve repositories, and folds those not adopted until it is used', async () => {
    expect(FILTER_PAST).toBe(12);
    list({ groups: many() });

    const work = within(group('work'));
    expect(work.getAllByRole('listitem')).toHaveLength(7);
    expect(work.getByRole('button', { name: 'Registered, not adopted (20)' })).toHaveAttribute('aria-expanded', 'false');
    // A group of none adopted shows those not adopted: there is nothing else to show in it.
    expect(within(group('forge')).getByRole('listitem', { name: 'bellows' })).toBeInTheDocument();

    await userEvent.type(screen.getByRole('searchbox', { name: 'filter repositories' }), 'tool-1');
    expect(within(group('work')).getAllByRole('listitem').map((item) => item.getAttribute('aria-label')))
      .toEqual(['tool-1', 'tool-10', 'tool-11', 'tool-12', 'tool-13', 'tool-14', 'tool-15', 'tool-16', 'tool-17', 'tool-18', 'tool-19']);
    expect(screen.queryByRole('region', { name: 'forge' })).toBeNull();

    await userEvent.clear(screen.getByRole('searchbox', { name: 'filter repositories' }));
    await userEvent.type(screen.getByRole('searchbox', { name: 'filter repositories' }), 'forge');
    expect(within(group('forge')).getAllByRole('listitem')).toHaveLength(2);
    expect(screen.queryByRole('region', { name: 'work' })).toBeNull();

    await userEvent.type(screen.getByRole('searchbox', { name: 'filter repositories' }), 'zzz');
    expect(screen.getByText('No repository or workspace matches “forgezzz”.')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('searchbox', { name: 'filter repositories' })).toHaveValue('');
  });

  it('offers no filter at twelve or fewer', () => {
    list();
    expect(screen.queryByRole('searchbox')).toBeNull();
  });

  /** CTX1 (D138, design §4): a repository's row offers what it does on a right-click, opening it and its name. */
  it('offers Open and its name on a right-click', async () => {
    const copy = vi.fn();
    const { onChoose } = list();
    render(<ContextMenus doors={{ copy }} />);

    rightClick(screen.getByRole('listitem', { name: 'game' }).querySelector('button')!);
    expect(await menuActs('Actions for game')).toEqual(['Open', 'Copy repository name']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Open' }));
    expect(onChoose).toHaveBeenCalledWith('game');

    rightClick(screen.getByRole('listitem', { name: 'engine' }).querySelector('button')!);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy repository name' }));
    expect(copy).toHaveBeenCalledWith('engine');
  });

  /** D118 §3h: a list that never had an answer says the sentence in place, and is never blank. */
  it('says the sentence of a registry that never answered, in place', () => {
    list({ groups: [], unanswered: 'Daoris could not reach this machine\'s host.' });

    expect(screen.getByText('Daoris could not reach this machine\'s host.')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).toBeNull();
  });
});
