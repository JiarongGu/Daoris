import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, useEffect, useState } from 'react';
import { WorkspaceScopeProvider } from '../data/scope';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { MapView } from './MapView';

// The Map's empty and failing cases (MAPUX1f; the map UX design §8, §9), each in English and 中文: the service not answering, a
// workspace with no repository, repositories with no connection, *Which quests* leaving none, every kind hidden, a map drawn that could
// not be read again, and a code map of one module. The service is a stand-in `fetch` set for the story's lifetime, and what the viewer
// kept is set the same way.

const DAY = 24 * 60 * 60 * 1000;
const ago = (days: number) => new Date(Date.now() - days * DAY).toISOString();
const repository = (name: string) => ({ repository: name, adopted: true, registered: true, summary: '', owns: [], accepts: [], packs: [], entries: 1 });
const quest = (id: string, from: string, to: string, days: number) => ({
  id, from, to, title: id, body: '', status: 'Done', filed: ago(days), updated: ago(days),
});

type World = { registry: unknown[]; quests: unknown[]; down?: string[]; codeMaps?: Record<string, unknown> };
type Kept = Record<string, string>;

function serve(world: World): typeof fetch {
  return async (input) => {
    const url = String(input);
    const asked = ['/api/registry', '/api/quests'].find((path) => url.startsWith(path));
    if (asked && world.down?.includes(asked)) return new Response('unavailable', { status: 503 });
    if (url.startsWith('/api/code-map/')) return Response.json(world.codeMaps?.[decodeURIComponent(url.slice('/api/code-map/'.length))]);
    if (url.startsWith('/api/registry')) return Response.json(world.registry);
    if (url.startsWith('/api/quests')) return Response.json(world.quests);
    return Response.json([]);
  };
}

/** The service and the viewer's kept choices stood in for, put back when the story goes. */
function Served({ world, kept, children }: { world: World; kept: Kept; children: ReactNode }) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: false } } }));
  const [real] = useState(() => {
    const was = window.fetch;
    window.fetch = serve(world);
    for (const [key, value] of Object.entries(kept)) localStorage.setItem(key, value);
    return was;
  });
  useEffect(() => () => {
    window.fetch = real;
    for (const key of Object.keys(kept)) localStorage.removeItem(key);
  }, [real, kept]);
  return (
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>{children}</WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>
  );
}

const served = (world: World, kept: Kept = {}): Decorator => (Story) => <Served world={world} kept={kept}><Story /></Served>;

const meta: Meta<typeof MapView> = {
  title: 'Map/Cases',
  component: MapView,
  args: { notify: () => {} },
  decorators: [storyWidths.wide],
};
export default meta;
type Story = StoryObj<typeof MapView>;

const PAIR = [repository('engine'), repository('game')];

/** The service does not answer, and nothing was read before: the map says it could not be read, and the notice stands. */
export const NotRead: Story = { decorators: [served({ registry: [], quests: [], down: ['/api/registry'] }), storyWidths.wide] };
export const NotReadChinese: Story = { decorators: [served({ registry: [], quests: [], down: ['/api/registry'] }), storyWidths.wide, chinese] };

/** No repository in scope: the empty state, with its door to Repositories. */
export const NoRepository: Story = { args: { onOpenRepositories: () => {} }, decorators: [served({ registry: [], quests: [] }), storyWidths.wide] };
export const NoRepositoryChinese: Story = { args: { onOpenRepositories: () => {} }, decorators: [served({ registry: [], quests: [] }), storyWidths.wide, chinese] };

/** Repositories and no connection yet: the line under the drawing says no work has moved between them. */
export const NoConnection: Story = { decorators: [served({ registry: PAIR, quests: [] }), storyWidths.wide] };
export const NoConnectionChinese: Story = { decorators: [served({ registry: PAIR, quests: [] }), storyWidths.wide, chinese] };

/** *Which quests* at its first, the last 30 days, leaves none: the line says so, with what *All of them* would show. */
const OLD = { registry: PAIR, quests: [quest('a', 'game', 'engine', 90), quest('b', 'engine', 'game', 120)] };
export const NoQuestInThirtyDays: Story = { decorators: [served(OLD), storyWidths.wide] };
export const NoQuestInThirtyDaysChinese: Story = { decorators: [served(OLD), storyWidths.wide, chinese] };

/** Every kind of connection hidden: the toolbar says 0 of 5 and the line says where to show them again. */
const HIDDEN = { 'daoris.mapLines': JSON.stringify(['quests', 'asks', 'chains', 'depends', 'knowledge']) };
export const EveryKindHidden: Story = { decorators: [served({ registry: PAIR, quests: [quest('a', 'game', 'engine', 2)] }, HIDDEN), storyWidths.wide] };
export const EveryKindHiddenChinese: Story = { decorators: [served({ registry: PAIR, quests: [quest('a', 'game', 'engine', 2)] }, HIDDEN), storyWidths.wide, chinese] };

/** A map already drawn stays when the quests cannot be read again, and says so under the drawing. */
const STALE = { registry: PAIR, quests: [], down: ['/api/quests'] };
export const DrawnButNotReadAgain: Story = { decorators: [served(STALE), storyWidths.wide] };
export const DrawnButNotReadAgainChinese: Story = { decorators: [served(STALE), storyWidths.wide, chinese] };

/** A code map of one module and no dependency: the card alone, and the line says no dependency is recorded. */
const ONE = { registry: PAIR, quests: [], codeMaps: { engine: { file: 'docs/code-map.json', modules: [{ id: 'solo', path: 'src/solo', summary: '' }], dependencies: [] } } };
export const OneModule: Story = { args: { code: 'engine' }, decorators: [served(ONE), storyWidths.wide] };
export const OneModuleChinese: Story = { args: { code: 'engine' }, decorators: [served(ONE), storyWidths.wide, chinese] };
