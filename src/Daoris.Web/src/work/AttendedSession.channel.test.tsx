import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import type { Session } from '../api';
import i18n from '../locales/i18n';
import type { Channel } from './channel';
import { AttendedSession } from './AttendedSession';

// SESSONE2c (D167 points 6 to 8; the co-working design §2.4): a session on an ask has its ask's channel as a panel under its head,
// kept to its line of work, with To starting at its repository. A session with none draws none.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const SESSION: Session = {
  id: 's1a2b3c4', quest: 'q1', repository: 'db', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z',
};

const CHANNEL: Channel = {
  ask: 'a1', entry: 'person', unread: 0, mark: null,
  items: [
    { kind: 'message', at: '2026-10-14T09:02:00Z', id: 'm1', person: false, session: 's9', repository: 'web', agent: 'codex', entry: 'ask:a1/web', to: 'db', text: 'Which column holds the figure?' },
    { kind: 'message', at: '2026-10-14T09:03:00Z', id: 'm2', person: false, session: 's8', repository: 'web', agent: 'codex', entry: 'ask:a1/web', to: 'api', text: 'Only for api.' },
  ],
};

describe('a session’s channel panel', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('is folded under the head, and opens to what concerns its repository, To starting there', async () => {
    const onPost = vi.fn();
    render(<AttendedSession session={SESSION} headed channel={{ channel: CHANNEL, only: { entry: 'ask:a1/db', repository: 'db' }, onPost }} />);

    const toggle = screen.getByRole('button', { name: /^Channel/ });
    expect(toggle).toHaveTextContent('Channel · 1');
    await userEvent.click(toggle);

    const panel = screen.getByRole('region', { name: 'Channel' });
    expect(within(panel).getByText('Which column holds the figure?')).toBeInTheDocument();
    expect(within(panel).queryByText('Only for api.')).toBeNull();
    expect(within(panel).getByRole('combobox', { name: 'To' })).toHaveTextContent('db');
  });

  it('is absent for a session with no ask’s channel', () => {
    render(<AttendedSession session={SESSION} headed />);

    expect(screen.queryByRole('region', { name: 'Channel' })).toBeNull();
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<AttendedSession session={SESSION} headed channel={{ channel: CHANNEL, only: { entry: 'ask:a1/db', repository: 'db' }, onPost: vi.fn() }} />);

    expect(screen.getByRole('button', { name: /^频道/ })).toHaveTextContent('频道 · 1');
  });
});
