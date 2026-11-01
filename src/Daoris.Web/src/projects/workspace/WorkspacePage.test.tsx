import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import type { StartWiring } from '../../map/wiring';
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

  /** §4.3: Details, Branches, Workflow (WORKFLOW1b) and Setup, in that order, the chosen one its holder's to remember. */
  it('holds four tabs, Details, Branches, Workflow and Setup, and asks for another by its press', async () => {
    const onTab = vi.fn();
    render(<WorkspacePage workspace="aurora" repositories={2} tab="branches" onTab={onTab}>the clean-up</WorkspacePage>);

    const tabs = screen.getByRole('tablist', { name: "aurora's pages" });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'Workflow', 'Setup']);
    await userEvent.click(within(tabs).getByRole('tab', { name: 'Workflow' }));
    expect(onTab).toHaveBeenCalledWith('workflow');
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
          { agent: 'claude-code', product: 'Claude Code', accounts: ['work@example.invalid', 'home@example.invalid'] },
          { agent: 'codex', product: 'Codex', accounts: [] },
        ]}
        onOpenAgent={onOpenAgent}
      />,
    );

    const start = screen.getByRole('listitem', { name: 'a start in aurora' });
    expect(within(start).getByText('personal')).toBeInTheDocument();
    // POOL1c (D197): Daoris's accounts, one set for every workspace, in its order; never *its own* nor *this machine's*.
    expect(within(screen.getByRole('listitem', { name: 'Claude Code' }))
      .getByText("Daoris's accounts, in order: work@example.invalid, home@example.invalid")).toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'Codex' }))
      .getByText('No Daoris account is signed in for it; its starts wait until you sign in.')).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/this machine's|its own:/);
    await userEvent.click(screen.getByRole('button', { name: 'Open Codex' }));
    expect(onOpenAgent).toHaveBeenCalledWith('codex');
  });

  // SETUPUX1v: the remote's wiring is a row of the workspace's Details, where the Setup tab's Remote row went.
  describe('its Remote row', () => {
    const wiring = (over: Partial<NonNullable<NonNullable<Parameters<typeof WorkspaceDetails>[0]['remote']>['wiring']>> = {}) => ({
      remote: null, fromEnvironment: false, onWire: vi.fn(), onUnwire: vi.fn(), ...over,
    });

    it('wires the workspace from a form one press away, and lets the key go once it has landed', async () => {
      const w = wiring();
      render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} remote={{ workspace: 'aurora', wiring: w }} />);

      const region = screen.getByRole('region', { name: 'Remote' });
      expect(within(region).getByText('Local only: nothing is wired for this workspace on this machine.')).toBeInTheDocument();
      await userEvent.click(within(region).getByRole('button', { name: 'Wire to a remote…' }));
      await userEvent.type(within(region).getByLabelText('Deployment'), 'https://aurora.example.com');
      await userEvent.type(within(region).getByLabelText('Key'), 'dk_aurorakey0000');
      await userEvent.click(within(region).getByRole('button', { name: 'Wire' }));
      expect(w.onWire).toHaveBeenCalledWith('https://aurora.example.com', 'dk_aurorakey0000', expect.any(Function));
    });

    it("shows where it syncs with only the key's audit prefix, and unwires", async () => {
      const w = wiring({ remote: { url: 'https://aurora.example.com', key: 'dk_abcd1234…' } });
      render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} remote={{ workspace: 'aurora', wiring: w }} />);

      expect(screen.getByText('dk_abcd1234…')).toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Unwire' }));
      expect(w.onUnwire).toHaveBeenCalled();
    });

    it('opens the form where the header asked to wire it, and takes the focus where a door asked for the row', () => {
      render(
        <WorkspaceDetails
          repositories={['engine']} onOpen={() => {}}
          remote={{ workspace: 'aurora', wiring: wiring(), startOpen: true, focus: true }}
        />,
      );
      expect(screen.getByLabelText('Deployment')).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Remote' }).querySelector('[data-workspace-row="remote"]')).toHaveFocus();
    });

    it('is absent where nothing answered the wiring', () => {
      render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} />);
      expect(screen.queryByRole('region', { name: 'Remote' })).toBeNull();
    });

    it('says it in Chinese', async () => {
      const { default: i18n } = await import('../../locales/i18n');
      await i18n.changeLanguage('zh');
      try {
        render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} remote={{ workspace: 'aurora', wiring: wiring() }} />);
        expect(screen.getByRole('region', { name: '远端' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: '接线到远端…' })).toBeInTheDocument();
      } finally {
        await i18n.changeLanguage('en');
      }
    });
  });

  it('draws no start and no accounts where a shell did not answer them', () => {
    render(<WorkspaceDetails repositories={['engine']} onOpen={() => {}} />);

    expect(screen.queryByRole('heading', { name: 'What a start runs on' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Accounts its work may run on' })).toBeNull();
  });
});
