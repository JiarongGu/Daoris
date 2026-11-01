import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import type { Ask } from '../data/api';
import i18n from '../locales/i18n';
import type { Channel } from '../work/channel';
import { ASK_EMPTY, ASK_SAID, ASK_TWO_ENTRIES } from '../work/atWork/atWork.fixtures';
import { AskPage } from './AskPage';
import { PUBLISHED } from './fixtures';

// COWORKVIEW1d (the cowork view design §3.5): an ask's page gains its At work section above its quests, replacing nothing, shell-only.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const ASK: Ask = { ...PUBLISHED, id: 'a1', quests: ['q0000a'] };
const CHANNEL: Channel = { ask: 'a1', entry: 'person', unread: 0, mark: null, items: ASK_SAID };

const page = (over: Partial<Parameters<typeof AskPage>[0]> = {}) => {
  const props = { ask: ASK, receivers: [], questTitles: { q0000a: 'Read the figure' }, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<AskPage {...props} />);
  return props;
};

describe('an ask’s page and its At work section', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the section above the quests it replaces nothing of, and opens a line of work’s own view', async () => {
    const open = vi.fn();
    page({ atWork: { held: ASK_TWO_ENTRIES, onOpenEntry: open } });

    const section = screen.getByRole('region', { name: 'At work' });
    const quests = screen.getByRole('region', { name: 'Became' });
    // Document order: the At work section comes before the quests' own.
    expect(section.compareDocumentPosition(quests) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(quests).getByRole('button', { name: /Read the figure/ })).toBeInTheDocument();

    await userEvent.click(within(section).getByRole('button', { name: /driver/ }));
    expect(open).toHaveBeenCalledWith('s2');
  });

  it('draws none in a browser, which has no driver, and says an ask with nothing at work is that', () => {
    page();
    expect(screen.queryByRole('region', { name: 'At work' })).toBeNull();
  });

  it('says nothing is at work on a held ask that has none', () => {
    page({ atWork: { held: ASK_EMPTY } });

    expect(within(screen.getByRole('region', { name: 'At work' })).getByText('Nothing is at work on this ask yet.')).toBeInTheDocument();
  });

  it('brings its own channel section into view on All messages, the channel being the page’s own read', async () => {
    page({
      atWork: { held: ASK_TWO_ENTRIES, words: ASK_SAID },
      channel: { channel: CHANNEL, onPost: vi.fn(), onRead: vi.fn() },
    });

    await userEvent.click(screen.getByRole('button', { name: 'All messages' }));
    expect(screen.getByRole('region', { name: 'Channel' })).toBeInTheDocument();
  });

  it('draws no channel block where the page has no channel', () => {
    page({ atWork: { held: ASK_TWO_ENTRIES, words: ASK_SAID } });

    expect(screen.queryByText('On the channel')).toBeNull();
  });
});
