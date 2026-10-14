import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import type { Ask } from '../api';
import i18n from '../i18n';
import type { Channel } from '../work/channel';
import { askLines } from '../work/entries';
import { EVIDENCE_ENTRIES } from '../work/entries.fixtures';
import { AskPage } from './AskPage';
import { PUBLISHED } from './fixtures';

// SESSONE2c (D167 points 6 to 8; the co-working design §2.4): an ask's page gains its Channel, where its sessions and the person
// talk, beside its sessions: shell-only, so a page given no door draws none.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const ASK: Ask = { ...PUBLISHED, id: 'c98a7e', quests: ['q0000a', 'q0000b'] };

const CHANNEL: Channel = {
  ask: 'c98a7e', entry: 'person', unread: 1, mark: '2026-10-14T09:00:00Z',
  items: [
    { kind: 'line', at: '2026-10-14T08:59:00Z', code: 'published', quest: 'q0000a', name: 'Read the figure', from: 'ask #c98a7e', to: 'db' },
    { kind: 'message', at: '2026-10-14T09:02:00Z', id: 'm1', person: false, session: 's1', repository: 'db', agent: 'claude-code', text: 'Which column?', question: true, open: true },
  ],
};

const page = (channel?: Parameters<typeof AskPage>[0]['channel']) => {
  const props = {
    ask: ASK, receivers: ['app'], questTitles: {}, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(), onOpenQuest: vi.fn(),
    lines: askLines(EVIDENCE_ENTRIES, ASK.id), channel,
  };
  render(<AskPage {...props} />);
  return props;
};

describe('an ask’s page and its channel', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws a Channel section with the channel, the repositories its lines of work are in, and its terminal twin', async () => {
    page({ channel: CHANNEL, onPost: vi.fn(), onRead: vi.fn() });

    const section = screen.getByRole('region', { name: 'Channel' });
    expect(within(section).getByText('Which column?')).toBeInTheDocument();
    expect(within(section).getByText('1 unread')).toBeInTheDocument();
    expect(section).toHaveTextContent('daoris-driver ask channel c98a7e');

    // The ask's own lines of work (app, db) are offered as recipients beside those the channel names.
    const user = userEvent.setup();
    within(section).getByRole('combobox', { name: 'To' }).focus();
    await user.keyboard('{Enter}');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual(['Everyone', 'app', 'db']);
  });

  it('opens a line’s quest through the page’s own door', async () => {
    const props = page({ channel: CHANNEL, onPost: vi.fn(), onRead: vi.fn() });

    await userEvent.click(within(screen.getByRole('region', { name: 'Channel' })).getByRole('button', { name: 'Read the figure published to db' }));

    expect(props.onOpenQuest).toHaveBeenCalledWith('q0000a');
  });

  it('moves the person’s mark once, through the newest item it showed', () => {
    const onRead = vi.fn();
    page({ channel: CHANNEL, onPost: vi.fn(), onRead });

    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onRead).toHaveBeenCalledWith('2026-10-14T09:02:00Z');
  });

  it('is read while the channel is on its way, and absent where no door is handed, as in a browser', () => {
    const { unmount } = rtlRender(<Tooltip.Provider><AskPage
      ask={ASK} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onDelete={vi.fn()} onOpenQuest={vi.fn()}
      channel={{ channel: null, onPost: vi.fn(), onRead: vi.fn() }}
    /></Tooltip.Provider>);
    expect(within(screen.getByRole('region', { name: 'Channel' })).getByText('Reading the channel…')).toBeInTheDocument();
    unmount();

    page(undefined);
    expect(screen.queryByRole('region', { name: 'Channel' })).toBeNull();
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page({ channel: CHANNEL, onPost: vi.fn(), onRead: vi.fn() });

    const section = screen.getByRole('region', { name: '频道' });
    expect(within(section).getByText('1 条未读')).toBeInTheDocument();
    expect(within(section).getByText('Read the figure 已发布给 db')).toBeInTheDocument();
  });
});
