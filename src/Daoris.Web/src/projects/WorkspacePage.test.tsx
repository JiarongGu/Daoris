import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import type { StartWiring } from '../map/wiring';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';

// A workspace's page as a molecule (UX6g, D150 §4.3): its header, its tabs, and its Details, each from props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const START: StartWiring = {
  job: 'work', workspace: 'aurora', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code', profile: 'personal',
  profileFrom: 'workspace', version: 'claude 9.9.9', versionFrom: 'unset', commanded: false, refusal: null,
};

describe("a workspace's page", () => {
  it('names the workspace, how many repositories it holds and where it syncs, with Sync now where it is wired', async () => {
    const onSync = vi.fn();
    render(<WorkspacePage workspace="aurora" repositories={3} remote={{ host: 'aurora.example.com' }} onSync={onSync} onTab={() => {}}>x</WorkspacePage>);

    expect(screen.getByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
    expect(screen.getByText('3 repositories · syncs with aurora.example.com')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Sync now' }));
    expect(onSync).toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Wire to a remote…' })).toBeNull();
  });

  it('says it is local only, and offers Wire to a remote… where it is not wired', async () => {
    const onWire = vi.fn();
    render(<WorkspacePage workspace="forge" repositories={1} remote={null} onWire={onWire} onTab={() => {}}>x</WorkspacePage>);

    expect(screen.getByText('1 repository · local only')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Wire to a remote…' }));
    expect(onWire).toHaveBeenCalled();
  });

  /** §4.3: Details, Branches and Setup, in that order, the chosen one its holder's to remember. */
  it('holds three tabs, Details, Branches and Setup, and asks for another by its press', async () => {
    const onTab = vi.fn();
    render(<WorkspacePage workspace="aurora" repositories={2} tab="branches" onTab={onTab}>the clean-up</WorkspacePage>);

    const tabs = screen.getByRole('tablist', { name: "aurora's pages" });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'Setup']);
    expect(within(tabs).getByRole('tab', { name: 'Branches' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel')).toHaveTextContent('the clean-up');
    await userEvent.click(within(tabs).getByRole('tab', { name: 'Setup' }));
    expect(onTab).toHaveBeenCalledWith('setup');
  });

  /** D47 §4, D48 §5: a browser is told the workspace syncs, never where, and has no tab row: one tab is no tabs. */
  it("gives a browser the page with what it may know: no tab row, no act, and no deployment's host", () => {
    render(<WorkspacePage workspace="aurora" repositories={2} remote={{}}>its repositories</WorkspacePage>);

    expect(screen.getByText('2 repositories · syncs with a deployment')).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Sync now' })).toBeNull();
    expect(screen.getByText('its repositories')).toBeInTheDocument();
  });
});

describe("a workspace's Details", () => {
  it('lists its repositories as doors to their pages', async () => {
    const onOpen = vi.fn();
    render(<WorkspaceDetails repositories={['engine', 'game']} onOpen={onOpen} />);

    await userEvent.click(screen.getByRole('button', { name: 'Open game' }));
    expect(onOpen).toHaveBeenCalledWith('game');
  });

  /** MAP1b, moved from Settings → Workspace: a row per job, each part with the setting that chose it. */
  it('says what a start in it runs on, and what accounts each agent may run here, each with a door to its agent', async () => {
    const onOpenAgent = vi.fn();
    render(
      <WorkspaceDetails
        repositories={['engine']}
        onOpen={() => {}}
        starts={{ list: [START], nameOf: (_owner, profile) => profile ?? 'own' }}
        accounts={[
          { agent: 'claude-code', product: 'Claude Code', own: true, accounts: ['work@example.invalid', 'home@example.invalid'] },
          { agent: 'codex', product: 'Codex', own: false, accounts: ['me@example.invalid'] },
        ]}
        onOpenAgent={onOpenAgent}
      />,
    );

    const start = screen.getByRole('listitem', { name: 'a start in aurora' });
    expect(within(start).getByText('personal')).toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'Claude Code' })).getByText('its own: work@example.invalid, home@example.invalid'))
      .toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'Codex' })).getByText("this machine's: me@example.invalid")).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open Codex' }));
    expect(onOpenAgent).toHaveBeenCalledWith('codex');
  });

  it('draws no start and no accounts where a shell did not answer them', () => {
    render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} />);

    expect(screen.queryByRole('heading', { name: 'What a start runs on' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Accounts its work may run on' })).toBeNull();
  });
});
