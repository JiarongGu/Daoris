import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ComponentProps, ReactNode } from 'react';

// The tree chip reads the checkout's branch through the shell's bridge (NEW1e-b): the shell is here, and its answers are the test's.
const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import i18n from '../i18n';
import { NewConversation, type StartRepository } from './NewConversation';

const inClient = (node: ReactNode) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{node}</QueryClientProvider>
);

/**
 * *New conversation* (NEW1b, D162 point 1, the entry design §1): the prompt first, then the repository, the tree and the
 * agent with its account, and *Start*. It replaced the *Start session* drawer, which started an empty conversation the person
 * typed into after.
 */
const REPOSITORIES: StartRepository[] = [
  { name: 'engine', workspace: 'aurora', checkout: true },
  { name: 'game', workspace: 'aurora', checkout: true },
  { name: 'tools', workspace: 'default', checkout: true },
  { name: 'portal', workspace: 'default', checkout: false },
];

const ACCOUNTS = {
  'claude-code': [{ name: 'personal', login: 'in' as const }],
  codex: [{ name: 'team', login: 'in' as const, account: 'team@example.com' }],
};

function show(over: Partial<ComponentProps<typeof NewConversation>> = {}) {
  const onStart = vi.fn();
  render(inClient(
    <Tooltip.Provider>
      <NewConversation
        repositories={REPOSITORIES}
        harnesses={['claude-code', 'codex']}
        defaultHarness="claude-code"
        names={{ 'claude-code': 'Claude Code', codex: 'Codex' }}
        accounts={ACCOUNTS}
        onStart={onStart}
        {...over}
      />
    </Tooltip.Provider>,
  ));
  return onStart;
}

const prompt = () => screen.getByRole('textbox', { name: 'What would you like to work on?' });
const start = () => screen.getByRole('button', { name: 'Start' });

/** A chip's menu, opened from the keyboard as the platform's other menus are in jsdom. */
async function chipMenu(name: RegExp) {
  screen.getByRole('button', { name }).focus();
  await userEvent.keyboard('{Enter}');
  return screen.findByRole('menu');
}

describe('a new conversation', () => {
  afterEach(async () => {
    cleanup();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('asks what to work on, then the repository, the tree and the agent with its account, and Start', () => {
    show();

    expect(prompt()).toBeInTheDocument();
    // The chips say what a start would use, each named for what it chooses.
    expect(screen.getByRole('button', { name: 'Repository: engine' })).toHaveTextContent('engine');
    expect(screen.getByRole('button', { name: "Tree: Driver's choice" })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Agent and account: Claude Code · driver's" })).toBeInTheDocument();
    // Nothing to start on yet: Start waits for words.
    expect(start()).toBeDisabled();
  });

  it('starts on Enter with the words, and Shift+Enter is a new line', async () => {
    const onStart = show();
    await userEvent.type(prompt(), 'Fix the flaky retry{Shift>}{Enter}{/Shift}in the sync loop');
    expect(onStart).not.toHaveBeenCalled();

    await userEvent.keyboard('{Enter}');

    expect(onStart).toHaveBeenCalledTimes(1);
    expect(onStart).toHaveBeenCalledWith({ repository: 'engine', text: 'Fix the flaky retry\nin the sync loop', files: [] });
  });

  /** IME1: an input method's Enter accepts a candidate; it never starts a conversation on half a sentence. */
  it('does nothing on an input method\'s composing Enter', async () => {
    const onStart = show();
    await userEvent.type(prompt(), '修复');

    fireEvent.keyDown(prompt(), { key: 'Enter', isComposing: true });
    fireEvent.keyDown(prompt(), { key: 'Enter', keyCode: 229 });

    expect(onStart).not.toHaveBeenCalled();
  });

  it('carries the files chosen beside the words', async () => {
    const onStart = show();
    const log = new File(['exit 3'], 'run.log', { type: 'text/plain' });
    await userEvent.upload(screen.getByLabelText('Choose files…'), log);
    await userEvent.type(prompt(), 'Why does this fail?');
    await userEvent.click(start());

    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ text: 'Why does this fail?', files: [log] }));
  });

  /**
   * D162 point 2: the tree choice means what it says. The driver's choice sends nothing (the repository's standing opt-in),
   * the checkout says false, and its own tree says true, with D51 §4's cost said where it is chosen.
   */
  it.each([
    [/^Driver's choice/, undefined],
    [/^The checkout/, false],
    [/^Its own tree/, true],
  ])('says the tree chosen as %s', async (row, ownTree) => {
    const onStart = show();
    const menu = await chipMenu(/^Tree:/);
    await userEvent.click(within(menu).getByRole('menuitemradio', { name: row }));
    await userEvent.type(prompt(), 'Tidy the build{Enter}');

    const sent = onStart.mock.calls[0]![0] as Record<string, unknown>;
    if (ownTree === undefined) expect(sent).not.toHaveProperty('ownTree');
    else expect(sent.ownTree).toBe(ownTree);
    expect(screen.queryByText(/holds only what git tracks/) !== null).toBe(ownTree === true);
  });

  /**
   * NEW1e-b (design §1): the checkout is never switched, so which branch a conversation in it starts on matters, and the chip says
   * it, only where the checkout is chosen. Driver's choice and its own tree name no branch.
   */
  describe('the checkout\'s branch on the tree chip', () => {
    const checkout = async () => {
      const menu = await chipMenu(/^Tree:/);
      await userEvent.click(within(menu).getByRole('menuitemradio', { name: /^The checkout/ }));
    };
    const asked = () => invoke.mock.calls.filter((call) => call[1] === 'CHECKOUT_BRANCH');

    it('names the branch the checkout is on, in the chip and the row\'s note', async () => {
      invoke.mockResolvedValue({ branch: 'main' });
      show();
      await waitFor(() => expect(asked()).toHaveLength(1));
      expect(asked()[0]![2]).toEqual({ payload: { repository: 'engine', workspace: 'aurora' } });
      await checkout();

      expect(screen.getByRole('button', { name: 'Tree: The checkout · main' })).toHaveTextContent('The checkout · main');
      const menu = await chipMenu(/^Tree:/);
      expect(within(menu).getByRole('menuitemradio', { name: /^The checkout.*main/ })).toBeInTheDocument();
    });

    it('says a detached HEAD, and in Chinese by the glossary\'s words', async () => {
      invoke.mockResolvedValue({ branch: null });
      show();
      await waitFor(() => expect(asked()).toHaveLength(1));
      await checkout();
      expect(screen.getByRole('button', { name: 'Tree: The checkout · detached HEAD' })).toBeInTheDocument();

      await i18n.changeLanguage('zh');
      expect(await screen.findByRole('button', { name: '工作树：现有检出 · 分离的 HEAD' })).toBeInTheDocument();
    });

    it('asks again when the menu opens, so a branch switched outside Daoris shows', async () => {
      invoke.mockResolvedValue({ branch: 'main' });
      show();
      await waitFor(() => expect(asked()).toHaveLength(1));
      await checkout();
      const before = asked().length;

      invoke.mockResolvedValue({ branch: 'release' });
      await chipMenu(/^Tree:/);

      await waitFor(() => expect(asked().length).toBeGreaterThan(before));
      await waitFor(() => expect(screen.getByRole('button', { name: 'Tree: The checkout · release' })).toBeInTheDocument());
    });

    it('says the checkout plain while the read is refused, and names no branch on the other two', async () => {
      invoke.mockRejectedValue(new Error('GIT_NO_CHECKOUT'));
      show();
      await waitFor(() => expect(asked()).toHaveLength(1));
      await checkout();
      expect(screen.getByRole('button', { name: 'Tree: The checkout' })).toBeInTheDocument();

      invoke.mockReset();
      invoke.mockResolvedValue({ branch: 'main' });
      const menu = await chipMenu(/^Tree:/);
      await userEvent.click(within(menu).getByRole('menuitemradio', { name: /^Its own tree/ }));
      expect(screen.getByRole('button', { name: 'Tree: Its own tree' })).toBeInTheDocument();
    });
  });

  it('lists the agents and their accounts, and a new agent clears an account it cannot use', async () => {
    const onStart = show();
    let menu = await chipMenu(/^Agent and account:/);
    await userEvent.click(within(menu).getByRole('menuitemradio', { name: 'personal' }));
    expect(screen.getByRole('button', { name: 'Agent and account: Claude Code · personal' })).toBeInTheDocument();

    menu = await chipMenu(/^Agent and account:/);
    // The account's name leads, and who signed in sits beside it where the two differ (D152 §4.2): codex's, once chosen.
    await userEvent.click(within(menu).getByRole('menuitemradio', { name: /^Codex/ }));
    expect(screen.getByRole('button', { name: "Agent and account: Codex · driver's" })).toBeInTheDocument();

    await userEvent.type(prompt(), 'Hello{Enter}');
    expect(onStart).toHaveBeenCalledWith({ repository: 'engine', text: 'Hello', files: [], adapter: 'codex' });
  });

  /**
   * D130 §3.2: the accounts the repository's workspace's list holds first, in the list's order, then every other under
   * *not in its list*, so a pick outside it is never made by accident.
   */
  it('lists the workspace’s accounts first, and every other under not in its list', async () => {
    show({
      harnesses: ['claude-code'],
      accounts: {
        'claude-code': [
          { name: 'account-1', login: 'in', account: 'you@work.example' },
          { name: 'account-2', login: 'in', account: 'you@home.example' },
          { name: 'account-3', login: 'out', account: 'spare@example.invalid' },
        ],
      },
      scopeOf: (repository, harness) => (repository === 'engine' && harness === 'claude-code'
        ? { workspace: 'aurora', list: ['account-2', 'account-1'] }
        : null),
    });

    const menu = await chipMenu(/^Agent and account:/);
    const accounts = within(within(menu).getByRole('group', { name: 'Account' })).getAllByRole('menuitemradio');
    expect(accounts.map((row) => row.textContent)).toEqual([
      "Driver's choice", 'account-2 · you@home.example', 'account-1 · you@work.example',
      'account-3 · spare@example.invalid (signed out)',
    ]);
    expect(within(menu).getByText("Not in aurora's list")).toBeInTheDocument();
  });

  /**
   * The design's picker: *Recent* first (this machine's last starts, each with its workspace), then each workspace's
   * repositories, none twice. One with no checkout here is listed, not choosable, and says why; a busy one is marked.
   */
  it('lists recent repositories first, then each workspace’s, marking a busy one and one with no checkout here', async () => {
    show({ recent: ['tools'], busy: { game: 's1a2b3c4' } });
    // It opens on the newest recent start.
    await userEvent.click(screen.getByRole('button', { name: 'Repository: tools' }));

    const list = screen.getByRole('listbox', { name: 'Repository' });
    const groups = within(list).getAllByRole('group');
    expect(groups.map((group) => group.getAttribute('aria-label'))).toEqual(['Recent', 'aurora', 'default']);
    expect(within(groups[0]!).getAllByRole('option').map((row) => row.textContent)).toEqual(['tools default']);
    expect(within(groups[1]!).getAllByRole('option').map((row) => row.textContent)).toEqual(['engine', 'game busy']);
    const portal = within(groups[2]!).getByRole('option', { name: /portal/ });
    expect(portal).toHaveAttribute('aria-disabled', 'true');
    expect(portal).toHaveTextContent('no checkout on this machine');

    await userEvent.click(portal);
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.click(within(groups[1]!).getByRole('option', { name: 'engine' }));
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(screen.getByRole('button', { name: 'Repository: engine' })).toHaveFocus();
  });

  it('finds a repository by what is typed, and takes it from the keyboard', async () => {
    const onChoose = vi.fn();
    show({ onChoose });
    await userEvent.click(screen.getByRole('button', { name: 'Repository: engine' }));

    const find = screen.getByRole('combobox', { name: 'find a repository' });
    expect(find).toHaveFocus();
    await userEvent.type(find, 'ga');
    expect(within(screen.getByRole('listbox')).getAllByRole('option').map((row) => row.textContent)).toEqual(['game']);
    await userEvent.keyboard('{Enter}');

    expect(onChoose).toHaveBeenCalledWith('game');
    expect(screen.getByRole('button', { name: 'Repository: game' })).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'Repository: game' }));
    await userEvent.type(screen.getByRole('combobox', { name: 'find a repository' }), 'zz');
    expect(screen.getByText('No repository matches zz.')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(screen.getByRole('button', { name: 'Repository: game' })).toHaveFocus();
  });

  it('opens on the repository last chosen, else a recent one, else one nothing works in', () => {
    show({ remembered: 'tools' });
    expect(screen.getByRole('button', { name: 'Repository: tools' })).toBeInTheDocument();
    cleanup();

    show({ recent: ['game', 'engine'], busy: { game: 's1a2b3c4' } });
    expect(screen.getByRole('button', { name: 'Repository: engine' })).toBeInTheDocument();
    cleanup();

    // NEW1f: where the person just was comes ahead of the one last chosen, busy or not; one with no checkout here is passed over.
    show({ attended: 'game', remembered: 'tools', busy: { game: 's1a2b3c4' } });
    expect(screen.getByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
    cleanup();

    show({ attended: 'portal', remembered: 'tools' });
    expect(screen.getByRole('button', { name: 'Repository: tools' })).toBeInTheDocument();
    cleanup();

    // A remembered one that has no checkout here any more is passed over.
    show({ remembered: 'portal', busy: { engine: 's1a2b3c4' } });
    expect(screen.getByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
  });

  /** UX5 U68 and the design's states: a busy checkout offers its own tree, or the session holding it; nothing is stopped. */
  it('says a busy checkout, and offers its own tree or the session holding it', async () => {
    const onOpenSession = vi.fn();
    const onStart = show({ busy: { engine: 's1a2b3c4' }, remembered: 'engine', onOpenSession });

    expect(screen.getByText(/engine has an active session in its checkout/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open that session' }));
    expect(onOpenSession).toHaveBeenCalledWith('s1a2b3c4');

    await userEvent.click(screen.getByRole('button', { name: 'Use its own tree' }));
    expect(screen.getByRole('button', { name: 'Tree: Its own tree' })).toBeInTheDocument();
    expect(screen.queryByText(/has an active session/)).toBeNull();
    await userEvent.type(prompt(), 'Beside it{Enter}');
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ repository: 'engine', ownTree: true }));
  });

  /** UX5 U68: a refused start is said whole where Start was pressed, its code marks drawn as code; the words stay. */
  it('says a refusal whole under Start, and keeps the words and the choices', async () => {
    const refusal = '`engine` already has an active session — `669b2930` (awaiting-person, a chat).';
    const onStart = show({ refusal });
    await userEvent.type(prompt(), 'Keep these words');
    await userEvent.click(start());

    expect(onStart).toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent('engine already has an active session — 669b2930 (awaiting-person, a chat).');
    expect(screen.getByText('669b2930').tagName).toBe('CODE');
    expect(prompt()).toHaveValue('Keep these words');
    expect(screen.getByRole('button', { name: 'Repository: engine' })).toBeInTheDocument();
  });

  it('presses once while a start is on its way', async () => {
    const onStart = show({ pending: true, draft: 'Already typed' });

    expect(screen.getByRole('button', { name: 'Starting…' })).toBeDisabled();
    fireEvent.keyDown(prompt(), { key: 'Enter' });
    expect(onStart).not.toHaveBeenCalled();
  });

  /** NEW1c, the design's first run: Start waits, and *Add repository…* is the primary press, with *Import a folder…* beside it. */
  it('takes words with no repository to start in, and offers to add one where it stands', async () => {
    const onAdd = vi.fn();
    const onImport = vi.fn();
    const onStart = show({ repositories: [{ name: 'portal', workspace: 'default', checkout: false }], onAdd, onImport });
    await userEvent.type(prompt(), 'Somewhere{Enter}');

    expect(onStart).not.toHaveBeenCalled();
    expect(start()).toBeDisabled();
    expect(screen.getByText(/No repository on this machine has a checkout to talk in yet/)).toBeInTheDocument();
    expect(screen.queryByText(/Add one from Repositories/)).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Import a folder…' }));
    expect(onImport).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Add repository…' }));
    expect(onAdd).toHaveBeenCalledTimes(1);
    // The words stay for the registering.
    expect(prompt()).toHaveValue('Somewhere');
  });

  /** NEW1c: the picker's foot holds the two presses, and a press puts the list down before its drawer opens. */
  it('offers Add repository… and Import a folder… at the picker’s foot, closing the list first', async () => {
    const onAdd = vi.fn();
    const onImport = vi.fn();
    show({ onAdd, onImport });

    await userEvent.click(screen.getByRole('button', { name: 'Repository: engine' }));
    await userEvent.click(within(screen.getByRole('listbox').parentElement!).getByRole('button', { name: 'Add repository…' }));
    expect(onAdd).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('listbox')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Repository: engine' }));
    await userEvent.click(screen.getByRole('button', { name: 'Import a folder…' }));
    expect(onImport).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  /** NEW1c: a registered repository is chosen once the registry lists it, and not before: the choice is kept pending. */
  it('chooses the repository just added once it is listed', () => {
    const onChoose = vi.fn();
    const client = new QueryClient();
    const view = (over: Partial<ComponentProps<typeof NewConversation>>) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <NewConversation
            repositories={REPOSITORIES} harnesses={['claude-code']} defaultHarness="claude-code" accounts={ACCOUNTS}
            onStart={vi.fn()} onChoose={onChoose} {...over}
          />
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(view({ pick: { repository: 'borealis', n: 1 } }));
    // Not listed yet: the opening choice stands.
    expect(screen.getByRole('button', { name: 'Repository: engine' })).toBeInTheDocument();

    rerender(view({
      pick: { repository: 'borealis', n: 1 },
      repositories: [...REPOSITORIES, { name: 'borealis', workspace: 'default', checkout: true }],
    }));
    expect(screen.getByRole('button', { name: 'Repository: borealis' })).toBeInTheDocument();
    expect(onChoose).toHaveBeenCalledWith('borealis');
  });

  /** NEW1c: several imported, none chosen for the person: the picker opens on the imported workspace's group. */
  it('opens the picker on a workspace’s group when asked to reveal it', () => {
    show({ reveal: { workspace: 'aurora', n: 1 } });

    const list = screen.getByRole('listbox', { name: 'Repository' });
    expect(within(list).getAllByRole('group').map((group) => group.getAttribute('aria-label'))).toEqual(['aurora']);
    expect(screen.getByRole('combobox', { name: 'find a repository' })).toHaveValue('aurora');
  });

  /**
   * NEW1d-b (D162): *Make it a task with Daoris…* is a quiet press beside *Start*, waiting for words, that hands the draft to Ask
   * Daoris with one line naming the repository chosen above it. It sends nothing and clears nothing: the composer keeps its draft.
   */
  it('hands the draft and the repository to Ask Daoris from a quiet press, and keeps the draft', async () => {
    const onMakeTask = vi.fn();
    const onStart = show({ onMakeTask, draft: '' });
    const press = screen.getByRole('button', { name: 'Make it a task with Daoris…' });
    expect(press).toBeDisabled();

    cleanup();
    const onDraft = vi.fn();
    show({ onMakeTask, onStart, draft: 'Fix the build', onDraft });
    await userEvent.click(screen.getByRole('button', { name: 'Repository: engine' }));
    await userEvent.click(await screen.findByRole('option', { name: /game/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Make it a task with Daoris…' }));

    expect(onMakeTask).toHaveBeenCalledWith('In game:\n\nFix the build');
    expect(onStart).not.toHaveBeenCalled();
    expect(onDraft).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'What would you like to work on?' })).toHaveValue('Fix the build');
  });

  it('offers the quiet press in Chinese, naming the repository with its glossary word', async () => {
    await i18n.changeLanguage('zh');
    const onMakeTask = vi.fn();
    show({ onMakeTask, draft: '修复构建' });
    await userEvent.click(screen.getByRole('button', { name: '请道衍把它整理成任务…' }));

    expect(onMakeTask).toHaveBeenCalledWith('在仓库 engine 中：\n\n修复构建');
  });

  it('offers no such press where nothing hands it on', () => {
    show({ draft: 'Fix the build' });

    expect(screen.queryByRole('button', { name: 'Make it a task with Daoris…' })).toBeNull();
  });

  /** A browser has no shell (D46 §7): no composer, and the place says where a conversation starts. */
  it('is absent in a browser, which says to open Daoris on this machine', () => {
    show({ attached: false });

    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.getByText('Open Daoris on this machine to start a conversation.')).toBeInTheDocument();
  });

  it('speaks Chinese, the repositories and the words as written', async () => {
    await i18n.changeLanguage('zh');
    show({ draft: 'Fix the build' });

    expect(screen.getByRole('textbox', { name: '想做些什么？' })).toHaveValue('Fix the build');
    expect(screen.getByRole('button', { name: '仓库：engine' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '工作树：由驱动决定' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '智能体与账户：Claude Code · 驱动默认' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '开启' })).toBeEnabled();
  });
});
