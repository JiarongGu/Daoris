import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { MapView } from './MapView';
import { MapDetail } from './map/MapDetail';
import { buildTopology } from './map/topology';
import { WorkspaceScopeProvider } from './scope';

// MAP2 (D67 §3): the workspace map, over a stubbed service — a view of its own, the owner's choice.

let REGISTRY: unknown[] = [];
const QUESTS = [
  { id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: '', status: 'Open', filed: '', updated: '' },
  { id: 'q2', from: 'game', to: 'engine', title: 'Fix the loader', body: '', status: 'Done', filed: '', updated: '' },
  { id: 'q3', from: 'ask #abc', to: 'game', title: 'From an ask', body: '', status: 'Open', filed: '', updated: '' },
];
const CONVERGENCE = [
  { method: 'Convergent', similarity: 0.8, repositories: ['engine', 'game'], entries: [], suggestion: '' },
];
const SESSIONS = [
  { id: 's1', repository: 'engine', adapter: 'stub', state: 'working', created: '', updated: '' },
];

// MAP3a: what `/api/code-map/{repository}` answers — the engine keeps a map, the game keeps none.
let CODE_MAPS: Record<string, unknown> = {};

function respond(url: string): Response {
  if (url.startsWith('/api/code-map/')) return Response.json(CODE_MAPS[decodeURIComponent(url.slice('/api/code-map/'.length))]);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/convergence')) return Response.json(CONVERGENCE);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  throw new Error(`unstubbed request: ${url}`);
}

function show(onOpenConvergence = () => {}, scope: string | null = null, onOpenQuest = (_id: string) => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={scope}>
          <MapView notify={() => {}} onOpenConvergence={onOpenConvergence} onOpenQuest={onOpenQuest} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the workspace map', () => {
  beforeEach(() => {
    REGISTRY = [
      { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: ['rendering'], accepts: ['bugs'], packs: [], entries: 1 },
      { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1 },
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => vi.unstubAllGlobals());

  it('draws each repository, the quests between them, and what they learned alike', async () => {
    show();

    expect(await screen.findByRole('button', { name: 'engine, 1 open, a session working now' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'game, 1 open' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '2 quests from game to engine' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'engine and game learned the same thing once' })).toBeTruthy();
    // The ask is not a repository: it is not invented as a node, and it is still counted.
    expect(screen.getByText(/1 quest comes from, or goes to, somewhere off this map/)).toBeTruthy();
    // The key says each line's meaning in words, not by hue alone (D41).
    expect(screen.getByText(/dashed line: the same thing learned in both/)).toBeTruthy();
    // 🔴 Seen on the window (POLISH4): the number inside a repository was in no key.
    expect(screen.getByText('the number in a repository: quests to it still open')).toBeTruthy();
  });

  it('says a repository whose session is parked is waiting on the person, not working', async () => {
    SESSIONS[0] = { ...SESSIONS[0]!, state: 'awaiting-person' };
    try {
      show();
      expect(await screen.findByRole('button', { name: 'engine, 1 open, a session waiting on you' })).toBeTruthy();
      const word = screen.getByText('waiting on you');
      expect(screen.queryByText('working now')).toBeNull();
      // UX5 U1: the hue the rail's dot and the band's card give the same fact, not the notice tone.
      expect(word.getAttribute('class')).toContain('fill-st-open');
      expect(word.closest('g')!.querySelector('circle[stroke-dasharray]')!.getAttribute('class'))
        .toContain('stroke-st-open');
    } finally {
      SESSIONS[0] = { ...SESSIONS[0]!, state: 'working' };
    }
  });

  it('a repository shows what it declares and the quests to and from it', async () => {
    show();

    await userEvent.click(await screen.findByRole('button', { name: /^engine, 1 open/ }));

    const detail = screen.getByText('quests to it').closest('div')!.parentElement!;
    expect(within(detail).getByText('rendering')).toBeTruthy();
    expect(within(detail).getByText('bugs')).toBeTruthy();
    expect(within(detail).getByText('Expose a streaming budget')).toBeTruthy();
    expect(within(detail).getByText('Fix the loader')).toBeTruthy();
    expect(within(detail).getByText('working now')).toBeTruthy();
  });

  /**
   * UX5 U45: the detail's marks are the canvas's, in the status hues the rail gives the same facts —
   * never the accent, which is not a status (§3). They were accent chips beside the canvas's amber.
   */
  it('says a session there in the hue the canvas and the rail give it', async () => {
    show();
    await userEvent.click(await screen.findByRole('button', { name: /^engine, 1 open/ }));
    const detail = screen.getByText('quests to it').closest('div')!.parentElement!;
    expect(within(detail).getByText('working now').getAttribute('class')).toContain('text-st-taken');

    SESSIONS[0] = { ...SESSIONS[0]!, state: 'awaiting-person' };
    try {
      cleanup();
      show();
      await userEvent.click(await screen.findByRole('button', { name: /^engine, 1 open/ }));
      const parked = screen.getByText('quests to it').closest('div')!.parentElement!;
      const word = within(parked).getByText('waiting on you');
      expect(word.getAttribute('class')).toContain('text-st-open');
      expect(word.getAttribute('class')).not.toContain('accent');
    } finally {
      SESSIONS[0] = { ...SESSIONS[0]!, state: 'working' };
    }
  });

  /**
   * UX5 U46: a quest named in the detail is a door to its drawer, where acting on it lives (design
   * §1). It was text, so a person read a quest here and had to find it again on Quests (U26's rule).
   */
  it('opens a quest named in the detail in its drawer', async () => {
    const open = vi.fn();
    show(() => {}, null, open);

    await userEvent.click(await screen.findByRole('button', { name: /^engine, 1 open/ }));
    await userEvent.click(screen.getByRole('button', { name: /Fix the loader/ }));
    expect(open).toHaveBeenCalledWith('q2');

    await userEvent.click(screen.getByRole('button', { name: '2 quests from game to engine' }));
    await userEvent.click(screen.getByRole('button', { name: /Expose a streaming budget/ }));
    expect(open).toHaveBeenLastCalledWith('q1');
  });

  it('a quest line lists the quests that went that way, each with its state in words', async () => {
    show();

    await userEvent.click(await screen.findByRole('button', { name: '2 quests from game to engine' }));

    expect(screen.getByText('game → engine')).toBeTruthy();
    expect(screen.getByText('Expose a streaming budget')).toBeTruthy();
    expect(screen.getByText('Done')).toBeTruthy();
  });

  it('a shared finding leads to Convergence, where the findings are', async () => {
    const open = vi.fn();
    show(open);

    await userEvent.click(await screen.findByRole('button', { name: 'engine and game learned the same thing once' }));
    await userEvent.click(screen.getByRole('button', { name: 'See them in Convergence' }));

    expect(open).toHaveBeenCalledOnce();
  });

  it('a keyboard reaches every part of it', async () => {
    show();
    const node = await screen.findByRole('button', { name: 'game, 1 open' });

    node.focus();
    await userEvent.keyboard('{Enter}');

    expect(node.getAttribute('aria-pressed')).toBe('true');
  });

  /**
   * UX5 U47: a choice is a toggle, as its `aria-pressed` says — a second press releases it, and so
   * does Escape — and the detail goes back to saying what a choice would show.
   */
  it('releases a choice on a second press, and on Escape', async () => {
    show();
    const node = await screen.findByRole('button', { name: 'game, 1 open' });

    await userEvent.click(node);
    expect(node.getAttribute('aria-pressed')).toBe('true');
    await userEvent.click(node);
    expect(node.getAttribute('aria-pressed')).toBe('false');
    expect(screen.getByText('Choose a repository or a line to see what it carries.')).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: '2 quests from game to engine' }));
    expect(screen.getByText('game → engine')).toBeTruthy();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByText('game → engine')).toBeNull();
    expect(screen.getByText('Choose a repository or a line to see what it carries.')).toBeTruthy();
  });

  /** U47: a keyboard moving through the nodes lights their lines as a pointer over them does. */
  it('lights the lines of a focused repository, as hover does', async () => {
    REGISTRY = [...REGISTRY, { repository: 'tools', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0 }];
    show();
    const tools = await screen.findByRole('button', { name: 'tools, 0 open' });
    const line = screen.getByRole('button', { name: '2 quests from game to engine' });
    expect(line.getAttribute('class')).not.toContain('opacity-20');

    act(() => tools.focus());
    expect(line.getAttribute('class')).toContain('opacity-20');
    act(() => tools.blur());
    expect(line.getAttribute('class')).not.toContain('opacity-20');
  });

  /**
   * U47: a chosen line stands forward and the others step back. It was told apart only by two more
   * units on a stroke already four and a half wide.
   */
  it('steps back every other line while one is chosen', async () => {
    show();
    const quests = await screen.findByRole('button', { name: '2 quests from game to engine' });
    const knowledge = screen.getByRole('button', { name: 'engine and game learned the same thing once' });

    await userEvent.click(quests);

    expect(quests.getAttribute('class')).not.toContain('opacity-20');
    expect(knowledge.getAttribute('class')).toContain('opacity-20');
  });

  /**
   * UX5 U44: the drawing is framed on what it draws and drawn one unit a pixel, centred. It was a
   * fixed 600-unit square: a blank lower third, the card's left edge, and names scaled with it.
   */
  it('is drawn at its own size, framed on what it draws', async () => {
    show();
    const map = await screen.findByRole('group', { name: 'the workspace map' });
    const [, , width, height] = map.getAttribute('viewBox')!.split(' ').map(Number);

    expect(map.getAttribute('width')).toBe(String(width));
    expect(height).toBeLessThan(600);
    expect(map.getAttribute('class')).toContain('mx-auto');
  });

  /**
   * UX5 U49: scoped to every workspace, the map says so, as its empty state already did, and a
   * repository's detail names its circle. It said "this workspace" over every circle's repositories.
   */
  it('says it spans every workspace, and names a repository’s own', async () => {
    REGISTRY = [
      { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, workspace: 'default' },
      { repository: 'studio-tools', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0, workspace: 'studio' },
    ];
    show();

    expect(await screen.findByText(/^The repositories in every workspace/)).toBeTruthy();
    await userEvent.click(await screen.findByRole('button', { name: /^studio-tools, 0 open/ }));
    expect(screen.getByText('in workspace studio')).toBeTruthy();
  });

  it('speaks of this workspace, and names none, when it holds one', async () => {
    show(() => {}, 'default');

    expect(await screen.findByText(/^The repositories in this workspace/)).toBeTruthy();
    await userEvent.click(await screen.findByRole('button', { name: /^game, 1 open/ }));
    expect(screen.queryByText(/^in workspace/)).toBeNull();
  });

  it('says so when the workspace holds no repository', async () => {
    REGISTRY = [];
    show(() => {}, 'aurora');

    expect(await screen.findByText('No repositories in this workspace')).toBeTruthy();
  });

  /** Seen on the installed window, 2026-09-24: scoped to every workspace, it spoke of "this one". */
  it('says no workspace holds one when the page is scoped to every workspace', async () => {
    REGISTRY = [];
    show();

    expect(await screen.findByText('No repository in any workspace yet')).toBeTruthy();
  });

  /** U47: a choice the data no longer holds (the scope moved, a quest closed away) is no choice. */
  it('says what a choice would show when the chosen part has left the map', () => {
    const topology = buildTopology([], [], [], []);
    render(<MapDetail topology={topology} selected={{ kind: 'node', id: 'gone' }} />);
    expect(screen.getByText('Choose a repository or a line to see what it carries.')).toBeTruthy();
    cleanup();
    render(<MapDetail topology={topology} selected={{ kind: 'quests', from: 'a', to: 'b' }} />);
    expect(screen.getByText('Choose a repository or a line to see what it carries.')).toBeTruthy();
  });

  // ——— One level in: a repository's own code map (MAP3a).

  const openCode = async (repository: 'engine' | 'game') => {
    await userEvent.click(await screen.findByRole('button', { name: new RegExp(`^${repository}, 1 open`) }));
    await userEvent.click(screen.getByRole('button', { name: 'Open its code map' }));
  };

  it('opens a repository\'s code map from its node, and goes back', async () => {
    CODE_MAPS = {
      engine: {
        repository: 'engine', file: 'docs/code-map.json', problem: null,
        modules: [
          { id: 'renderer', path: 'src/renderer', summary: 'draws frames' },
          { id: 'chunks', path: 'src/chunks', summary: 'streams the world' },
        ],
        dependencies: [{ from: 'renderer', to: 'chunks', kind: 'imports' }],
      },
    };
    show();
    await openCode('engine');

    expect(await screen.findByRole('heading', { name: 'engine: its code' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'chunks, depends on 0' }));
    expect(screen.getByText('streams the world')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'renderer' })).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Back to the workspace' }));
    expect(await screen.findByRole('heading', { name: 'Map' })).toBeTruthy();
  });

  /** 🔴 As the host answers it: a null field is LEFT OUT, and `=== null` once drew nothing at all. */
  it('says a repository keeps no code map, and where one would go', async () => {
    CODE_MAPS = { game: { repository: 'game', modules: [], dependencies: [] } };
    show();
    await openCode('game');

    expect(await screen.findByText('game keeps no code map')).toBeTruthy();
    expect(screen.getByText(/docs\/code-map\.json/)).toBeTruthy();
  });

  /** MAP3e: a teammate's commit that keeps no map says so by that commit, not with the advice for a checkout here. */
  it('says a teammate\'s held commit keeps no code map', async () => {
    CODE_MAPS = {
      game: {
        repository: 'game', modules: [], dependencies: [],
        fed: { commit: 'feedfeedfeed', shortCommit: 'feedfeed', committedAt: new Date().toISOString(), branch: 'main' },
      },
    };
    show();
    await openCode('game');

    expect(await screen.findByText('game keeps no code map')).toBeTruthy();
    expect(screen.getByText(/feedfeed/)).toBeTruthy();
  });

  /** Judged whole: the service's sentence, verbatim, and nothing of the file drawn. */
  it('shows a refused file in the service\'s own words and draws none of it', async () => {
    const problem = '`docs/code-map.json` has a dependency naming `ghost`, which is not a module in the file.';
    CODE_MAPS = { engine: { repository: 'engine', file: 'docs/code-map.json', problem, modules: [], dependencies: [] } };
    show();
    await openCode('engine');

    expect(await screen.findByText(problem)).toBeTruthy();
    expect(screen.queryByRole('group', { name: 'the code map of engine' })).toBeNull();
  });
});
