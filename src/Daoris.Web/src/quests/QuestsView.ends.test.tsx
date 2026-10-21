import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, waitFor, within } from '@testing-library/react';

// FABRIC-CLOSE1j2 (§E5; D173's CLOSE1j and CLOSE1j2 notes): Quests reads the chosen quest's closure graph's ends from this machine's
// driver (`WORK_STATE`, by the quest's unit `quest:<id>`, which no milestone on the page carries) and its page says them, each in the
// reader's language; another unit's end is never this quest's. Over a mocked bridge, as the shell-attached half is held
// (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../locales/i18n';
import { chooseRow, QuestsView } from '../test/questsView';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';

/** The harness's quest, `abc123`, closed only in part: its close found itself lost with its take, and its stop is offered. */
const PART = {
  unit: 'quest:abc123', kind: 'partially-closed', at: '2026-10-21T09:00:00Z', nodes: [], quest: 'abc123', node: 'close', attempt: 2,
  nodeState: 'ready', resolution: null, words: ['stop'], word: null,
};
/** Another quest's graph ended by the person's own done: never said on this quest's page. */
const ELSEWHERE = {
  unit: 'quest:fff999', kind: 'overridden', at: '2026-10-21T08:00:00Z', nodes: ['deploy'], quest: null, node: null, attempt: null,
  nodeState: null, resolution: null, words: [], word: null,
};

describe('a quest’s closure graph’s end, read from the driver for its page', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/asks') ? Response.json([]) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (type === 'WORK_STATE') return { units: [], recovery: [], unread: [], held: [], unreadable: [], ends: [ELSEWHERE, PART] };
      return DRIVER_STATE;
    });
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    await i18n.changeLanguage('en');
  });

  it('asks for the quest’s own unit and says its end alone, in English and in 中文', async () => {
    show(<QuestsView notify={() => {}} />);

    const page = await chooseRow('Expose a streaming budget');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_STATE', { payload: { unit: 'quest:abc123' } }));
    expect(await within(page).findByText(/^Partially closed: #abc123 is no longer done/)).toBeInTheDocument();
    expect(within(page).getByText(/^Its closing step “close” waits again, and would close #abc123 over that take/)).toHaveClass('text-ink-danger');
    expect(page).not.toHaveTextContent('Closed by you before its end');

    await act(async () => { await i18n.changeLanguage('zh'); });
    expect(await within(page).findByText(/^部分关闭：#abc123 已不再是已完成/)).toBeInTheDocument();
    expect(within(page).getByText('关闭', { selector: 'dt' })).toBeInTheDocument();
    expect(page).not.toHaveTextContent('在结束前已由你关闭');
  });
});
