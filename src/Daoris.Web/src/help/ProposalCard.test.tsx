import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { type HelpProposal, ProposalCard } from './ProposalCard';

// HELP1c (D89): a change Ask Daoris proposes, and the person's two presses.

const LANDING = {
  id: 'p1a2b3c4', kind: 'setting' as const,
  describe: 'Land workspace `work`\'s accepted work on a branch `feature/{quest}-{slug}`.',
  terminal: 'daoris driver landing --workspace work branch feature/{quest}-{slug}',
  why: 'the person asked for feature branches',
};

describe('a proposal of Ask Daoris\'s', () => {
  it('says what it changes, the command that does the same, and why', () => {
    render(<ul><ProposalCard proposal={LANDING} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText('feature/{quest}-{slug}', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(LANDING.terminal, { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('Why: the person asked for feature branches')).toBeInTheDocument();
  });

  it('is applied or not by the person, and does neither while a press is on its way', async () => {
    const onApply = vi.fn();
    const onDismiss = vi.fn();
    const { rerender } = render(<ul><ProposalCard proposal={LANDING} onApply={onApply} onDismiss={onDismiss} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'apply' }));
    await userEvent.click(screen.getByRole('button', { name: 'not now' }));
    expect(onApply).toHaveBeenCalledWith('p1a2b3c4');
    expect(onDismiss).toHaveBeenCalledWith('p1a2b3c4');

    rerender(<ul><ProposalCard proposal={LANDING} pending onApply={onApply} onDismiss={onDismiss} /></ul>);
    expect(screen.getByRole('button', { name: 'apply' })).toBeDisabled();
  });
});

// HELP6: every door built since, a card of its own kind — each with Apply (or its kind's word for it) and
// Not now, since every one is the person's press.

const UPDATE: HelpProposal = {
  id: 'p2', kind: 'agent',
  describe: 'Update `claude-code-acp`: move its pin from 0.84.0 to the newest release, installed before the pin moves.',
  terminal: 'daoris agent update claude-code-acp',
  why: 'the person asked for the newest',
};

const ACCOUNT: HelpProposal = {
  id: 'p3', kind: 'account',
  describe: 'Set `claude-code` account `work`\'s model to `opus` and its effort to `high`.',
  terminal: 'daoris agent settings claude-code --account work model opus effort high',
  why: 'the person wants it to think harder',
};

const DELETE: HelpProposal = {
  id: 'p4', kind: 'delete',
  describe: 'Delete ask `#a1b2c3d4` “a test ask”, with the quest it became: `#q1a2b3c4` “Cap the chunk budget”.',
  terminal: 'daoris-driver ask --delete a1b2c3d4',
  why: 'it was a test',
};

const GO: HelpProposal = {
  id: 'p5', kind: 'go', describe: 'Open Settings → Get started at step 2, Daoris\'s own agent.', terminal: '',
  why: 'the person asked where to name its agent',
};

// PLUG9: a plugin that has landed, added from its folder, or one installed here switched on.
const PLUGIN_ADD: HelpProposal = {
  id: 'p6', kind: 'plugin',
  describe: 'Add plugin `acme.quiet-hours` (Quiet hours 1.0.0) from `quiet-hours` in `house-plugins`, copied into Daoris\'s home under its id.',
  terminal: 'daoris plugin add /checkouts/house-plugins/quiet-hours',
  why: 'the person wants quests held overnight',
  plugin: {
    id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.0.0',
    command: ['node', '${plugin}/hooks.mjs'], points: ['quest/consider', 'session/ended'],
    harnesses: [{ name: 'acme-agent', command: ['acme-agent', '--acp'] }],
    servers: [{ name: 'browser', command: ['npx', '-y', '@playwright/mcp@latest'] }],
    copied: true,
  },
};

// The bridge leaves a null out, so a switch's view arrives with no command and no `copied`.
const PLUGIN_ON: HelpProposal = {
  id: 'p7', kind: 'plugin', describe: 'Switch plugin `example.lands` on.', terminal: 'daoris plugin enable example.lands',
  why: 'the person wants it on',
  plugin: {
    id: 'example.lands', name: 'example.lands', version: '', points: [], harnesses: [], servers: [],
    problem: 'declares harness `claude-code`, which this build already carries.',
  },
};

// PLUG9 (d): one of the install's own plugins, by id, with what it needs; (c): an update, with what changes.
const PLUGIN_OFFER: HelpProposal = {
  id: 'p8', kind: 'plugin',
  describe: 'Install Daoris\'s own plugin `github-pull-request` (GitHub pull request 1.0.0), which this install offers.',
  terminal: 'daoris plugin add --offer github-pull-request',
  why: 'the person wants pull requests opened',
  plugin: {
    id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0',
    command: ['node', '${plugin}/land.mjs'], points: ['work/land'], harnesses: [], servers: [],
    copied: true, needs: ['gh, signed in: `gh auth login`.'],
  },
};

const PLUGIN_UPDATE: HelpProposal = {
  id: 'p9', kind: 'plugin',
  describe: 'Update plugin `acme.quiet-hours` from the folder it was added from.',
  terminal: 'daoris plugin update acme.quiet-hours --yes',
  why: 'a newer one has landed',
  plugin: {
    id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.1.0',
    command: ['node', '${plugin}/hooks.mjs'], points: ['quest/consider'], harnesses: [], servers: [],
    replaced: true, changes: [{ what: 'version', was: '1.0.0', now: '1.1.0' }, { what: 'servers', was: '', now: 'browser (npx -y @playwright/mcp@0.0.82)' }],
  },
};

const press = async (proposal: HelpProposal, apply: string) => {
  const onApply = vi.fn();
  const onDismiss = vi.fn();
  render(<ul><ProposalCard proposal={proposal} onApply={onApply} onDismiss={onDismiss} /></ul>);
  await userEvent.click(screen.getByRole('button', { name: apply }));
  await userEvent.click(screen.getByRole('button', { name: 'not now' }));
  expect(onApply).toHaveBeenCalledWith(proposal.id);
  expect(onDismiss).toHaveBeenCalledWith(proposal.id);
};

describe('the kinds that reach every door', () => {
  it('an agent\'s update says what moves and the command, with Apply and Not now', async () => {
    await press(UPDATE, 'apply');

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText('claude-code-acp', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(UPDATE.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('an account\'s model and effort say the values in the tool\'s own words, with Apply and Not now', async () => {
    await press(ACCOUNT, 'apply');

    expect(screen.getByText('opus', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('high', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(ACCOUNT.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('a delete says what goes and that it cannot be undone, and its Apply is a delete', async () => {
    await press(DELETE, 'delete');

    expect(screen.getByText('Ask Daoris proposes a delete')).toBeInTheDocument();
    expect(screen.getByText('#q1a2b3c4', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/cannot be undone/)).toBeInTheDocument();
    expect(screen.getByText(DELETE.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('a go names the place, carries no command since it changes nothing, and its Apply is a go', async () => {
    await press(GO, 'go there');

    expect(screen.getByText('Ask Daoris suggests a screen')).toBeInTheDocument();
    expect(screen.getByText(GO.describe)).toBeInTheDocument();
    expect(screen.getByText(/changes nothing/)).toBeInTheDocument();
    expect(screen.queryByText(/the same at a terminal/)).not.toBeInTheDocument();
  });

  it('a plugin to add shows what will run before Apply: its id, its command as written, its points, harnesses and servers', async () => {
    await press(PLUGIN_ADD, 'apply');

    expect(screen.getByText('Ask Daoris proposes a plugin')).toBeInTheDocument();
    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText('acme.quiet-hours', { selector: 'code' })).toBeInTheDocument();
    // `${plugin}` as the manifest writes it, never a path on this machine.
    expect(runs.getByText('node ${plugin}/hooks.mjs', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('quest/consider', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('session/ended', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('acme-agent', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('acme-agent --acp', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('browser', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('npx -y @playwright/mcp@latest', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/copies its folder into Daoris's home under its id/)).toBeInTheDocument();
    expect(screen.getByText(PLUGIN_ADD.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('a plugin to switch says it is not copied, and what keeps it from contributing in the driver\'s words', () => {
    render(<ul><ProposalCard proposal={PLUGIN_ON} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText('example.lands', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText(/runs no process of its own/)).toBeInTheDocument();
    expect(screen.queryByText(/copies its folder/)).not.toBeInTheDocument();
    expect(screen.getByText(/contributes nothing: declares harness/)).toBeInTheDocument();
  });

  // WSR5b: a branch a landing made, handed to a landing plugin — the one card whose Apply leads to a push.
  const HAND: HelpProposal = {
    id: 'p8', kind: 'hand',
    describe: 'Hand `feature/q2-second` (in `engine`) to plugin `example.lands`: it pushes the branch and opens the pull request against the line.',
    terminal: 'daoris-driver trees hand feature/q2-second --repository engine',
    why: 'the person wants its pull request opened',
  };

  it('a hand-off says a plugin pushes the branch, signed in as the person, before Apply', async () => {
    await press(HAND, 'apply');

    expect(screen.getByText('Ask Daoris proposes handing a branch on')).toBeInTheDocument();
    expect(screen.getByText('feature/q2-second', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/pushes the branch to its remote, signed in as you/)).toBeInTheDocument();
    expect(screen.getByText(HAND.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  // HELP10: Daoris's browser's settings, a card as a setting's is: what changes, and the command that does the same.
  const BROWSER: HelpProposal = {
    id: 'p10', kind: 'browser',
    describe: 'Links on the page open in Daoris\'s browser, from the next click. A sign-in link always opens in the system\'s browser.',
    terminal: 'daoris browser links daoris',
    why: 'the person wants tickets opened where their sign-ins are',
  };

  it('a browser setting says what changes and the command that does the same, with Apply and Not now', async () => {
    await press(BROWSER, 'apply');

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText(BROWSER.describe)).toBeInTheDocument();
    expect(screen.getByText(BROWSER.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  // HELP10: bringing up to date (WSR6, D109), the screen's two presses on one card: the look, which fetches and so is
  // the person's, then Apply on the rows the look listed.
  const SYNC: HelpProposal = {
    id: 'p11', kind: 'sync',
    describe: 'Bring `engine` up to date after a pull request merged. Look for updates first: Daoris fetches each line from `origin`, as you.',
    terminal: 'daoris-driver trees sync --repository engine',
    why: 'the person\'s pull request merged',
    sync: { looked: false, rows: [] },
  };
  const LOOKED: HelpProposal = {
    ...SYNC,
    describe: 'Bring `engine` up to date: 1 thing(s) change, only the rows below that move, each judged again right before it acts.',
    terminal: 'daoris-driver trees sync --repository engine --yes',
    sync: {
      looked: true,
      rows: [
        { key: 'engine:main', step: 'line', moves: true, says: 'engine  main  fast-forwards 1 commit(s) to `origin/main`' },
        { key: 'engine:daoris/s-busy', step: 'replay', moves: false, says: 'engine  daoris/s-busy  a session still running or waiting holds its tree' },
      ],
    },
  };

  it('bringing up to date asks the person to look first, the press that fetches, and lists nothing yet', async () => {
    await press(SYNC, 'look for updates');

    expect(screen.getByText('Ask Daoris proposes bringing repositories up to date')).toBeInTheDocument();
    expect(screen.getByText(/Looking fetches each line from origin, as you/)).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'what the press would do' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'apply' })).not.toBeInTheDocument();
  });

  it('once looked, it lists what the press would do in the terminal\'s words, and Apply does only what moves', async () => {
    await press(LOOKED, 'apply');

    const rows = within(screen.getByRole('list', { name: 'what the press would do' }));
    expect(rows.getByText('moves')).toBeInTheDocument();
    expect(rows.getByText('stays')).toBeInTheDocument();
    expect(rows.getByText('origin/main', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/Apply does only the rows that move/)).toBeInTheDocument();
    expect(screen.getByText(LOOKED.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  // LEFT3 b: what the rows do not say, said on the card itself and not only in the look's message (WSR7, D112).
  const unreadable = 'fatal: Could not read from remote repository.';
  const yesterday = new Date(Date.now() - 26 * 3_600_000).toISOString();
  const OFFLINE: HelpProposal = {
    ...LOOKED,
    sync: {
      ...LOOKED.sync!,
      rows: [
        ...LOOKED.sync!.rows,
        { key: 'game:main', step: 'line', moves: false, says: 'game  main  up to date with `origin/main` (not fetched)' },
        { key: 'tools:main', step: 'line', moves: false, says: 'tools  main  up to date with `origin/main` (not fetched)' },
      ],
      notFetched: [
        { repository: 'game', fetch: unreadable, lastFetch: yesterday, reach: 'ssh' },
        { repository: 'tools', fetch: unreadable, lastFetch: null, reach: 'ssh' },
      ],
      apart: ['docs', 'site'],
    },
  };

  it('once looked, says on the card what was not fetched, by reason and since when, and what git needs to reach origin', () => {
    render(<ul><ProposalCard proposal={OFFLINE} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    const note = screen.getByRole('note', { name: 'Not fetched' });
    const said = within(note);
    expect(said.getByText(/^2 of 3 repositories were not fetched, so each is judged against what origin said/)).toBeInTheDocument();
    expect(said.getByText(unreadable)).toBeInTheDocument();
    expect(said.getByText('game: last fetched 1d ago · tools: never fetched')).toBeInTheDocument();
    expect(note.textContent).toMatch(/needs a key its own ssh reads, or core\.sshCommand/);
    expect(note.textContent).not.toMatch(/credential helper/);
  });

  it('once looked, names on the card the repositories it left apart, and how a proposal includes one', () => {
    render(<ul><ProposalCard proposal={OFFLINE} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    const apart = within(screen.getByRole('group', { name: 'Repositories not looked at' }));
    expect(apart.getByText('2 other repositories with a checkout here hold no branch of Daoris\'s')).toBeInTheDocument();
    expect(apart.getByText('docs', { selector: 'code' })).toBeInTheDocument();
    expect(apart.getByText('site', { selector: 'code' })).toBeInTheDocument();
    expect(apart.getByText(/A proposal naming one looks at it/)).toBeInTheDocument();
  });

  it('says nothing besides its rows where every line was fetched and nothing was left apart, or before the look', () => {
    render(<ul><ProposalCard proposal={LOOKED} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={{ ...SYNC, sync: { ...OFFLINE.sync!, looked: false } }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.queryByRole('note', { name: 'Not fetched' })).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Repositories not looked at' })).not.toBeInTheDocument();
  });

  it('one of the install\'s own plugins says what it needs, in its README\'s words, before Apply', async () => {
    await press(PLUGIN_OFFER, 'apply');

    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText('gh auth login', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText(/^needs:/)).toBeInTheDocument();
    expect(screen.getByText(PLUGIN_OFFER.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('an update says what changes, as the manifests write it, and that its folder is replaced keeping what it kept', async () => {
    await press(PLUGIN_UPDATE, 'apply');

    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText('1.0.0', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText('1.1.0', { selector: 'code' })).toBeInTheDocument();
    expect(runs.getByText(/its servers/)).toBeInTheDocument();
    expect(screen.getByText(/replaces its folder from where it came from/)).toBeInTheDocument();
    expect(screen.queryByText(/copies its folder/)).not.toBeInTheDocument();
  });

  it('speaks 中文 for every kind, the driver\'s sentence left as it said it', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={DELETE} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={GO} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={PLUGIN_ADD} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍提议删除')).toBeInTheDocument();
      expect(screen.getByText('问道衍建议打开一个界面')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '删除' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '前往' })).toBeInTheDocument();
      expect(screen.getByText(GO.describe)).toBeInTheDocument();
      // PLUG9: the plugin card's chrome translates; the command stays as the manifest writes it.
      expect(screen.getByText('问道衍提议一个插件')).toBeInTheDocument();
      const runs = within(screen.getByRole('list', { name: '插件会运行什么' }));
      expect(runs.getByText('node ${plugin}/hooks.mjs', { selector: 'code' })).toBeInTheDocument();
      cleanup();
      // WSR5b: the hand-off's chrome translates too.
      render(<ul><ProposalCard proposal={HAND} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍提议交接一个分支')).toBeInTheDocument();
      expect(screen.getByText(/以你的身份把分支推送到它的远程/)).toBeInTheDocument();
      // PLUG9 (c) and (d): what an offer needs and what an update changes, the chrome in 中文.
      render(<ul><ProposalCard proposal={PLUGIN_OFFER} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={PLUGIN_UPDATE} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText(/^需要：/)).toBeInTheDocument();
      expect(screen.getByText(/它的版本/)).toBeInTheDocument();
      expect(screen.getByText(/从它的来处替换它的文件夹/)).toBeInTheDocument();
      cleanup();
      // HELP10: bringing up to date's chrome, the look's word the screen's own.
      render(<ul><ProposalCard proposal={SYNC} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍提议同步到最新')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '查看更新' })).toBeInTheDocument();
      cleanup();
      // LEFT3 b: what the look did not fetch and left apart, the chrome in 中文 and git's words as git said them.
      render(<ul><ProposalCard proposal={OFFLINE} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(within(screen.getByRole('note', { name: '未获取' })).getByText(unreadable)).toBeInTheDocument();
      expect(within(screen.getByRole('group', { name: '未查看的仓库' })).getByText(/点名其中一个的提议会查看它/)).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
