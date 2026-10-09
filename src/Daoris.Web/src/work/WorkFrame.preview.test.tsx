import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// PREVIEW1 (D111): a file a tool card or the review named, read in the side bar's preview. The Work frame
// as a page over a mocked bridge, as the rest of its suite runs: the doors, the tab, the host's answer.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../i18n';
import { WorkFrame } from './WorkFrame';

const ROOT = 'C:/somewhere/engine';
const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: ROOT,
}];

/** A conversation in the repository's checkout: its record names the checkout as its tree (D51). */
const CHAT = {
  id: 'c0ffee11', quest: null, repository: 'engine', adapter: 'stub', state: 'working', tree: ROOT,
  kind: 'chat', created: '2026-09-30T10:00:00Z', updated: '2026-09-30T10:05:00Z',
};

/** A driven session in a tree of its own, whose review lists what it changed. */
const DRIVEN = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code', state: 'completed',
  tree: 'C:/somewhere/data/trees/default/engine/daoris-s1a2b3c4',
  kind: 'driven', created: '2026-09-30T09:00:00Z', updated: '2026-09-30T09:30:00Z',
};

const TEXT = Array.from({ length: 30 }, (_, index) => `const line${index + 1} = ${index + 1};`).join('\n') + '\n';

const FILE = { session: 'c0ffee11', path: 'src/chunk.ts', size: TEXT.length, binary: false, text: TEXT, truncated: false };

const HISTORY = {
  session: 'c0ffee11', earlier: false, latest: 2,
  events: [
    { seq: 1, at: '2026-09-30T10:00:00Z', kind: 'user', origin: 'person', text: 'read the chunk' },
    {
      seq: 2, at: '2026-09-30T10:00:02Z', kind: 'tool', id: 'toolu_1', title: `Read ${ROOT}/src/chunk.ts`,
      toolKind: 'read', status: 'completed', locations: [`${ROOT}/src/chunk.ts`],
      input: JSON.stringify({ file_path: `${ROOT}/src/chunk.ts`, offset: 10, limit: 10 }),
    },
  ],
};

const DIFF = {
  session: 's1a2b3c4', base: 'abc1234567890', truncated: null,
  files: [
    { path: 'src/chunk.ts', status: 'modified', added: 1, removed: 1, patch: '@@ -1 +1 @@\n-const line1 = 0;\n+const line1 = 1;' },
    { path: 'src/legacy.ts', status: 'deleted', added: 0, removed: 12, patch: '@@ -1 +0,0 @@\n-gone' },
  ],
};

let SESSIONS: unknown[] = [];

function frame(selected: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={vi.fn()} notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const asked = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

describe('a file\'s preview in the side bar (PREVIEW1)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
      if (url.startsWith('/api/quests')) return Response.json([]);
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      throw new Error(`unstubbed request: ${url}`);
    }));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { path?: string } }) => {
      if (module !== 'DAORIS.DRIVER') return {};
      if (type === 'SESSION_HISTORY') return HISTORY;
      if (type === 'SESSION_FILE') return { ...FILE, path: request?.payload?.path ?? FILE.path };
      if (type === 'SESSION_DIFF') return DIFF;
      return { drivable: [], holds: [], trees: [], running: [] };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('opens a file a tool card read in the side bar, with the lines the read named marked', async () => {
    SESSIONS = [CHAT];
    frame('c0ffee11');

    // The dock opens on demand (UX5 U7): closed until the door is pressed.
    await userEvent.click(await screen.findByRole('button', { name: 'preview src/chunk.ts, lines 10–19' }));

    const side = screen.getByRole('complementary', { name: 'right side bar' });
    expect(within(side).getByRole('tab', { name: 'Preview: chunk.ts', selected: true })).toBeInTheDocument();
    await waitFor(() => expect(asked('SESSION_FILE')).toEqual([
      ['DAORIS.DRIVER', 'SESSION_FILE', { payload: { id: 'c0ffee11', path: 'src/chunk.ts' } }],
    ]));

    const preview = await within(side).findByRole('region', { name: 'preview of src/chunk.ts' });
    const rows = await within(preview).findAllByRole('row');
    expect(rows).toHaveLength(30);
    expect(rows.flatMap((row, index) => (row.dataset.marked === 'true' ? [index + 1] : [])))
      .toEqual([10, 11, 12, 13, 14, 15, 16, 17, 18, 19]);
    expect(within(preview).getByText('lines 10–19, as the call named them')).toBeInTheDocument();
    // The review has not been asked for, so the preview says nothing of changes and asks git for none.
    expect(within(preview).queryByRole('radiogroup', { name: 'Show' })).toBeNull();
    expect(asked('SESSION_DIFF')).toEqual([]);
  });

  it('closes back to the tab it covered', async () => {
    SESSIONS = [CHAT];
    frame('c0ffee11');
    await userEvent.click(await screen.findByRole('button', { name: 'preview src/chunk.ts, lines 10–19' }));
    const side = screen.getByRole('complementary', { name: 'right side bar' });

    await userEvent.click(within(side).getByRole('button', { name: 'Close the preview' }));

    expect(within(side).queryByRole('tab', { name: 'Preview: chunk.ts' })).toBeNull();
    expect(within(side).getByRole('tab', { name: 'Timeline', selected: true })).toBeInTheDocument();
  });

  /**
   * With every view moved to the panel the side bar is not drawn; a preview draws it with its one tab, and
   * closing it leaves nothing there, so the side bar closes, as one a moved view leaves does (DOCK1b).
   */
  it('opens a side bar that holds nothing else, and closes it again with the preview', async () => {
    SESSIONS = [CHAT];
    // Every view the side bar holds by default moved to the panel, the workflow (WORKFLOW1c) among them.
    window.localStorage.setItem('daoris.viewPlaces', JSON.stringify({ timeline: 'panel', review: 'panel', workflow: 'panel' }));
    frame('c0ffee11');
    const door = await screen.findByRole('button', { name: 'preview src/chunk.ts, lines 10–19' });
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();

    await userEvent.click(door);
    const side = screen.getByRole('complementary', { name: 'right side bar' });
    expect(within(side).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'))).toEqual(['Preview: chunk.ts']);

    await userEvent.click(within(side).getByRole('button', { name: 'Close the preview' }));
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();
  });

  /** The review's own answer: its patch for the file, one press away, and git not asked again. */
  it('opens a file from the review\'s list, with its changes from the review\'s own patch', async () => {
    SESSIONS = [DRIVEN];
    window.localStorage.setItem('daoris.dockClosed', '0');
    frame('s1a2b3c4');
    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    await userEvent.click(await within(side).findByRole('tab', { name: 'Review' }));

    // A deleted file is not in the tree any more, so it offers no preview (UX5 U66).
    await within(side).findByText('src/legacy.ts');
    expect(within(side).queryByRole('button', { name: 'preview src/legacy.ts' })).toBeNull();

    await userEvent.click(within(side).getByRole('button', { name: 'preview src/chunk.ts' }));
    expect(within(side).getByRole('tab', { name: 'Preview: chunk.ts', selected: true })).toBeInTheDocument();
    const preview = await within(side).findByRole('region', { name: 'preview of src/chunk.ts' });
    await within(preview).findAllByRole('row');

    await userEvent.click(within(preview).getByRole('radio', { name: 'Changes' }));
    expect(within(preview).getByText('@@ -1 +1 @@')).toBeInTheDocument();
    expect(asked('SESSION_DIFF')).toHaveLength(1);

    // And back to the review it covered.
    await userEvent.click(within(side).getByRole('button', { name: 'Close the preview' }));
    expect(within(side).getByRole('tab', { name: 'Review', selected: true })).toBeInTheDocument();
  });

  /** The host's refusal is its sentence, in the preview that asked, with the path as it was asked for. */
  it('says the host\'s refusal where the file would have been', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return {};
      if (type === 'SESSION_HISTORY') return HISTORY;
      if (type === 'SESSION_FILE') {
        throw Object.assign(new Error('fallback'), {
          code: 'PREVIEW_LINK_LEAVES_TREE', parameters: { session: 'c0ffee11', path: 'src/chunk.ts' },
        });
      }
      return { drivable: [], holds: [], trees: [], running: [] };
    });
    frame('c0ffee11');

    await userEvent.click(await screen.findByRole('button', { name: 'preview src/chunk.ts, lines 10–19' }));

    const preview = await screen.findByRole('region', { name: 'preview of src/chunk.ts' });
    expect(await within(preview).findByText(/goes through a link that leads out of this session's tree/)).toBeInTheDocument();
    expect(within(preview).queryAllByRole('row')).toHaveLength(0);
  });
});
