import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { HelpReceipt, HelpWired } from '../../bridge/help';
import { clockOf } from '../../locales/format';
import i18n from '../../locales/i18n';
import { WhatItHas } from './WhatItHas';

// HELPMODE1b (D183 point 4; the modes design §6): *What it has*, the newest receipt of what a start of the conversation in view
// handed its agent, each server's state, what restricted mode lacks with the door that grants each, the earlier starts behind a
// disclosure, and a conversation from before said as not recorded.

const RESTRICTED: HelpReceipt = {
  at: '2026-10-19T10:00:00+00:00', start: 'going-on', mode: 'restricted', harness: 'claude-code-acp', version: '0.9.1',
  account: 'personal', door: 'acp', posture: 'default', permissionMode: null, folder: 'C:/daoris/help/room',
  tools: ['mcp__daoris-knowledge__search', 'mcp__daoris-knowledge__read'],
  skills: [{ name: 'browser:look', state: 'unverified', why: null }, { name: 'notes:write', state: 'withheld', why: 'no-door' }],
  servers: [
    { name: 'daoris-knowledge', plugin: null, state: 'handed', tools: ['search', 'read'], why: null },
    { name: 'tracker', plugin: 'tracker', state: 'handed', tools: ['list_issues'], why: null },
    { name: 'browser', plugin: 'browser', state: 'withheld', tools: null, why: 'not-approved' },
    { name: 'pages', plugin: 'browser', state: 'withheld', tools: null, why: 'browser.no-host' },
  ],
  rules: { allow: ['Read', 'Grep'], ask: [], deny: ['Bash(git push:*)'] }, guarded: true,
  checkouts: [{ name: 'engine', workspace: 'aurora', path: 'C:/src/engine' }],
  lacks: [
    { kind: 'server', name: 'browser', door: 'plugins', why: 'not-approved', plugin: 'browser' },
    { kind: 'server', name: 'pages', door: 'host', why: 'browser.no-host', plugin: null },
    { kind: 'server', name: 'files', door: 'harness', why: 'no-door', plugin: null },
    { kind: 'shell', name: 'shell', door: 'mode', why: 'restricted', plugin: null },
    { kind: 'edits', name: 'edits', door: 'mode', why: 'restricted', plugin: null },
  ],
};

const FULL: HelpReceipt = {
  ...RESTRICTED, at: '2026-10-19T11:00:00+00:00', start: 'going-on', mode: 'full', account: null, tools: [], rules: null,
  guarded: false, checkouts: [], lacks: [],
  servers: [
    { name: 'daoris-knowledge', plugin: null, state: 'handed', tools: null, why: null },
    { name: 'browser', plugin: 'browser', state: 'full-access', tools: null, why: null },
  ],
};

const answer = (receipts: HelpReceipt[]): HelpWired => ({ session: 'h1', recorded: receipts.length > 0, receipts });

function draw(over: Partial<Parameters<typeof WhatItHas>[0]> = {}) {
  const acts = { onDoor: vi.fn(), onMode: vi.fn(), onClose: vi.fn(), onRetry: vi.fn() };
  render(
    <Tooltip.Provider>
      <WhatItHas wired={answer([RESTRICTED])} loading={false} error={null} {...acts} {...over} />
    </Tooltip.Provider>,
  );
  return acts;
}

const section = () => screen.getByRole('region', { name: i18n.t('help.wired.title') });
/** A field's value, by its term. */
const field = (name: string) => screen.getAllByRole('definition').find((value) =>
  value.previousElementSibling?.textContent === i18n.t(`help.wired.field.${name}`))!;

describe.each(['en', 'zh'])('What Ask Daoris has, in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('reads the newest start: its mode, agent, account, folder, Daoris’s tools, skills, rules and checkouts', async () => {
    await i18n.changeLanguage(language);
    draw();

    expect(section()).toBeInTheDocument();
    expect(field('mode')).toHaveTextContent(i18n.t('help.mode.restricted'));
    expect(field('agent')).toHaveTextContent('claude-code-acp 0.9.1');
    expect(field('account')).toHaveTextContent('personal');
    expect(field('folder')).toHaveTextContent('C:/daoris/help/room');
    // Daoris's own tools by their names, as its connector offers them.
    expect(within(field('tools')).getByText('search')).toBeInTheDocument();
    expect(within(field('tools')).getByText('read')).toBeInTheDocument();
    expect(within(field('skills')).getByText('browser:look').closest('li')).toHaveTextContent(i18n.t('help.wired.state.unverified'));
    expect(within(field('skills')).getByText('notes:write').closest('li')).toHaveTextContent(i18n.t('help.wired.why.no-door'));
    expect(field('rules')).toHaveTextContent(i18n.t('help.wired.rules.counts', { allow: 2, ask: 0, deny: 1 }));
    expect(field('rules')).toHaveTextContent(i18n.t('help.wired.guarded'));
    expect(within(field('checkouts')).getByText('engine').closest('li')).toHaveTextContent('aurora');
    // When it was handed, and whether as it started or as it went on.
    expect(section()).toHaveTextContent(i18n.t('help.wired.at.going-on', { at: clockOf(RESTRICTED.at) }));
  });

  it('says each server’s state: handed, withheld with why, and handed by full access without an approval', async () => {
    await i18n.changeLanguage(language);
    draw();
    const servers = field('servers');
    const row = (name: string) => within(servers).getByText(name).closest('li')!;
    expect(row('tracker')).toHaveTextContent(i18n.t('help.wired.state.handed'));
    expect(row('browser')).toHaveTextContent(i18n.t('help.wired.state.withheld'));
    expect(row('browser')).toHaveTextContent(i18n.t('help.wired.why.not-approved'));
    expect(row('pages')).toHaveTextContent(i18n.t('help.wired.why.browser.no-host'));

    cleanup();
    draw({ wired: answer([FULL]) });
    expect(field('mode')).toHaveTextContent(i18n.t('help.mode.full'));
    expect(within(field('servers')).getByText('browser').closest('li')).toHaveTextContent(i18n.t('help.wired.state.full-access'));
    expect(field('tools')).toHaveTextContent(i18n.t('help.wired.tools.full'));
    expect(field('account')).toHaveTextContent(i18n.t('help.wired.account.own'));
    expect(field('rules')).toHaveTextContent(i18n.t('help.wired.rules.none'));
    // Full access lacks nothing it could be granted here.
    expect(screen.queryByText(i18n.t('help.wired.field.lacks'))).toBeNull();
  });

  it('lists what restricted mode lacks, each with the door that grants it', async () => {
    await i18n.changeLanguage(language);
    const { onDoor, onMode } = draw();
    const lacks = field('lacks');
    const row = (text: string) => within(lacks).getByText(text).closest('li')!;

    await userEvent.click(within(row('browser')).getByRole('button', { name: i18n.t('help.door.plugins') }));
    expect(onDoor).toHaveBeenCalledWith({ view: 'plugins', item: 'browser' });
    await userEvent.click(within(row('pages')).getByRole('button', { name: i18n.t('help.door.browser') }));
    expect(onDoor).toHaveBeenCalledWith({ view: 'settings', section: 'browser' });
    await userEvent.click(within(row('files')).getByRole('button', { name: i18n.t('help.door.ai') }));
    expect(onDoor).toHaveBeenCalledWith({ view: 'settings', section: 'ai' });
    expect(row(i18n.t('help.wired.lack.shell'))).toHaveTextContent(i18n.t('help.wired.why.restricted'));
    await userEvent.click(within(row(i18n.t('help.wired.lack.edits'))).getByRole('button', { name: i18n.t('help.wired.door.mode') }));
    expect(onMode).toHaveBeenCalledOnce();
  });

  it('keeps the earlier starts behind a disclosure', async () => {
    await i18n.changeLanguage(language);
    draw({ wired: answer([FULL, RESTRICTED]) });

    expect(field('mode')).toHaveTextContent(i18n.t('help.mode.full'));
    const earlier = screen.getByText(i18n.t('help.wired.earlier', { count: 1 }));
    const kept = earlier.closest('details')!;
    expect(kept).not.toHaveAttribute('open');
    expect(within(kept).getByText('C:/src/engine')).not.toBeVisible();
    await userEvent.click(earlier);
    expect(within(kept).getByText('C:/src/engine')).toBeVisible();
  });

  it('says a conversation from before as not recorded, and what it is still reading or could not', async () => {
    await i18n.changeLanguage(language);
    draw({ wired: answer([]) });
    expect(screen.getByText(i18n.t('help.wired.notRecorded'))).toBeInTheDocument();

    cleanup();
    draw({ wired: undefined, loading: true });
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('help.wired.loading'));

    cleanup();
    const { onRetry, onClose } = draw({ wired: undefined, error: 'the driver is not running.' });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is not running.');
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.retry') }));
    expect(onRetry).toHaveBeenCalledOnce();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.wired.close') }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
