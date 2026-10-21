import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import i18n from '../locales/i18n';
import type { Channel, ElsewhereAsk } from '../work/channel';
import { TeammateAskPage } from './TeammateAskPage';

// SESSONE2h (D167; the co-working design §2.4): a teammate's ask has no record here, so its page is the channel the sync mirrored,
// read-only, under what the messages carry (circle, machines, moment); none of the ask page's request, quest or work sections.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const ASK: ElsewhereAsk = { id: '7e6f51', workspace: 'default', origins: ['key-a', 'key-b'], latest: '2026-10-15T09:05:00Z', messages: 2, unread: 1 };

const CHANNEL: Channel = {
  ask: '7e6f51', entry: 'person', unread: 1, mark: '2026-10-15T09:01:00Z',
  items: [
    { kind: 'message', at: '2026-10-15T09:00:00Z', id: 'm1', person: false, session: 'key-a/s1', repository: 'db', agent: 'claude-code', text: 'Which column?', origin: 'key-a' },
    { kind: 'message', at: '2026-10-15T09:05:00Z', id: 'm2', person: false, session: 'key-b/s2', repository: 'api', agent: 'codex', text: 'The v3 one.', origin: 'key-b' },
  ],
};

describe('a teammate’s ask’s page', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the ask by its id, says it is a teammate’s and where it is held, and reads its channel with no composer', () => {
    render(<TeammateAskPage ask={ASK} channel={CHANNEL} />);

    expect(screen.getByRole('heading', { name: /teammate’s ask #7e6f51/ })).toBeInTheDocument();
    expect(screen.getByText(/held on their machine/)).toBeInTheDocument();
    expect(screen.getByText('default')).toBeInTheDocument();
    expect(screen.getByText('key-a, key-b')).toBeInTheDocument();
    const channel = screen.getByRole('list', { name: 'Channel' });
    expect(within(channel).getAllByRole('listitem')).toHaveLength(2);
    expect(within(channel).getByText('Which column?')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  it('draws none of an ask’s own sections: no proposal, no quests, no publish, no close', () => {
    render(<TeammateAskPage ask={ASK} channel={CHANNEL} />);

    for (const name of ['Proposal', 'Quests', 'Review', 'Sessions', 'Files', 'Links']) {
      expect(screen.queryByRole('region', { name })).toBeNull();
    }
    expect(screen.queryByRole('button', { name: /Publish|Close ask|Delete/ })).toBeNull();
  });

  /** NOTIFY2g: the service keeps the person's mark on a teammate's ask, so reading its page clears its item in Messages. */
  it('moves the person’s mark through the newest item once what was unread is on the page', () => {
    const onRead = vi.fn();
    render(<TeammateAskPage ask={ASK} channel={CHANNEL} onRead={onRead} />);
    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onRead).toHaveBeenCalledWith('2026-10-15T09:05:00Z');
  });

  it('says it is reading while the channel is on its way', () => {
    render(<TeammateAskPage ask={ASK} channel={null} />);
    expect(screen.getByText('Reading the channel…')).toBeInTheDocument();
  });

  it('is worded in Chinese with the glossary’s names', async () => {
    await i18n.changeLanguage('zh');
    render(<TeammateAskPage ask={ASK} channel={CHANNEL} />);

    expect(screen.getByRole('heading', { name: /队友的需求 #7e6f51/ })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: '频道' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
  });
});
