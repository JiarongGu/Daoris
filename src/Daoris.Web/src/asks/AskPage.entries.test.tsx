import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import type { Ask } from '../api';
import i18n from '../locales/i18n';
import { askLines } from '../work/entries';
import { EVIDENCE_ENTRIES } from '../work/entries.fixtures';
import { AskPage } from './AskPage';
import { PUBLISHED } from './fixtures';

// SESSONE1c (D167 point 5): an ask's page lists its work by entry, one line per repository's line of work however many records
// it holds, each a door to its leading part; the ask's intake keeps its own section. Props in, presses out (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const ASK: Ask = { ...PUBLISHED, id: 'c98a7e', quests: ['q0000a', 'q0000b'] };

const page = (entries = EVIDENCE_ENTRIES, attend = true) => {
  const props = {
    ask: ASK, receivers: ['app'], questTitles: {}, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(), onOpenQuest: vi.fn(),
    onAttend: attend ? vi.fn() : undefined, lines: askLines(entries, ASK.id),
  };
  render(<AskPage {...props} />);
  return props;
};

describe("an ask's page lists its sessions by entry", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('lists one line per repository the ask worked in, with its parts and its word', () => {
    page();

    const section = screen.getByRole('region', { name: 'Sessions' });
    const lines = within(section).getAllByRole('listitem');
    expect(lines).toHaveLength(2);
    expect(lines[0]).toHaveTextContent(/app.*3 parts.*working/);
    expect(lines[1]).toHaveTextContent(/db.*3 parts.*working/);
  });

  it('leaves out another ask\'s lines of work and the intake, which has its own section', () => {
    const intake = {
      entry: 'ask:c98a7e/intake', origin: 'ask:c98a7e', repository: 'intake', group: 'ended' as const, leading: '1a000001',
      parts: [{ session: '1a000001', quest: null, shown: 'completed', group: 'ended' as const, began: 'opened', after: null, askedBy: null }],
    };
    expect(askLines([...EVIDENCE_ENTRIES, intake], 'c98a7e').map((line) => line.repository)).toEqual(['app', 'db']);
  });

  it('opens an entry at its leading part', async () => {
    const props = page();

    await userEvent.click(within(screen.getByRole('region', { name: 'Sessions' })).getByRole('button', { name: /^db/ }));
    expect(props.onAttend).toHaveBeenCalledWith('db000003');
  });

  it('names a line of work and opens nothing where Sessions is not here, as in a browser', () => {
    page(EVIDENCE_ENTRIES, false);

    expect(within(screen.getByRole('region', { name: 'Sessions' })).queryByRole('button')).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page();

    expect(within(screen.getByRole('region', { name: '会话' })).getAllByRole('listitem')[0]).toHaveTextContent('3 段');
  });

  it('draws no section for an ask no session has worked on', () => {
    render(<AskPage ask={ASK} receivers={['app']} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onDelete={vi.fn()} onOpenQuest={vi.fn()} lines={[]} />);

    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
  });
});
