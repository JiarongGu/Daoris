import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { WorkspaceScopeProvider } from '../data/scope';
import { MapView } from './MapView';

// MAPUX1f (the map UX design §8, §9): the Map's empty and failing cases, and *Which quests* first the last 30 days.

const DAY = 24 * 60 * 60 * 1000;
const ago = (days: number) => new Date(Date.now() - days * DAY).toISOString();
const quest = (id: string, from: string, to: string, status: string, updated: string) => ({ id, from, to, title: id, body: '', status, filed: updated, updated });
const repository = (name: string) => ({ repository: name, adopted: true, registered: true, summary: '', owns: [], accepts: [], packs: [], entries: 1 });

const world = vi.hoisted(() => ({
  registry: [] as unknown[], quests: [] as unknown[], down: [] as string[], codeMaps: {} as Record<string, unknown>,
}));

function respond(url: string): Response {
  const asked = ['/api/registry', '/api/quests', '/api/convergence', '/api/sessions'].find((path) => url.startsWith(path));
  if (asked && world.down.includes(asked)) return new Response('unavailable', { status: 503 });
  if (url.startsWith('/api/code-map/')) return Response.json(world.codeMaps[decodeURIComponent(url.slice('/api/code-map/'.length))]);
  if (url.startsWith('/api/registry')) return Response.json(world.registry);
  if (url.startsWith('/api/quests')) return Response.json(world.quests);
  if (url.startsWith('/api/convergence') || url.startsWith('/api/sessions')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show(props: { code?: string | null; onOpenRepositories?: () => void } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <MapView notify={() => {}} {...props} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const line = () => document.querySelector('[data-choice-line]')!;

beforeEach(() => {
  world.registry = [repository('engine'), repository('game')];
  world.quests = [];
  world.down = [];
  world.codeMaps = {};
  localStorage.clear();
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  return i18n.changeLanguage('en');
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); localStorage.clear(); });

describe('Which quests, first', () => {
  beforeEach(() => {
    world.quests = [quest('new', 'game', 'engine', 'Done', ago(3)), quest('old', 'engine', 'game', 'Done', ago(90))];
  });

  it('is the last 30 days for a viewer who has chosen nothing: the recent quest is drawn and the old one is not', async () => {
    show();
    expect(await screen.findByRole('button', { name: '1 quest from game to engine' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /from engine to game/ })).toBeNull();
    expect(screen.getByRole('button', { name: /^Connections: / })).toHaveTextContent('· 30 days');
  });

  it('keeps a choice already kept', async () => {
    localStorage.setItem('daoris.mapWhen', 'all');
    show();
    expect(await screen.findByRole('button', { name: '1 quest from engine to game' })).toBeInTheDocument();
  });
});

describe('the line under the drawing, with nothing chosen', () => {
  it('says no work has moved between the repositories, where there is no connection at all', async () => {
    show();
    await screen.findByRole('button', { name: /^engine, / });
    expect(line()).toHaveTextContent('No work has moved between these repositories yet.');
  });

  it('says no quest changed in the last 30 days, with what All of them would show, in English and Chinese', async () => {
    world.quests = [quest('old', 'game', 'engine', 'Done', ago(90)), quest('older', 'game', 'engine', 'Done', ago(120))];
    show();
    await screen.findByRole('button', { name: /^engine, / });
    expect(line()).toHaveTextContent('No quest changed in the last 30 days. All of them shows 2.');
    cleanup();
    await i18n.changeLanguage('zh');
    show();
    await screen.findByRole('button', { name: /^engine，/ });
    expect(line()).toHaveTextContent('最近 30 天没有委托变动。“全部”显示 2 个。');
  });

  it('says every kind is hidden, and that the menu shows them again, in English and Chinese', async () => {
    localStorage.setItem('daoris.mapLines', JSON.stringify(['quests', 'asks', 'chains', 'depends', 'knowledge']));
    show();
    await screen.findByRole('button', { name: /^engine, / });
    expect(screen.getByRole('button', { name: 'Connections: 0 of 5 kinds drawn' })).toHaveTextContent('0/5');
    expect(line()).toHaveTextContent('Every kind of connection is hidden; the Connections menu shows them again.');
    cleanup();
    await i18n.changeLanguage('zh');
    show();
    await screen.findByRole('button', { name: /^engine，/ });
    expect(line()).toHaveTextContent('所有连线都已隐藏');
  });
});

describe('empty and failing', () => {
  it('names a door to Repositories where the workspace has none', async () => {
    world.registry = [];
    const onOpenRepositories = vi.fn();
    show({ onOpenRepositories });
    await userEvent.click(await screen.findByRole('button', { name: 'Open Repositories' }));
    expect(onOpenRepositories).toHaveBeenCalledTimes(1);
  });

  it('says the map could not be read, not that no repository is in scope, when the service does not answer', async () => {
    world.down = ['/api/registry'];
    show();
    expect(await screen.findByText('The map could not be read')).toBeInTheDocument();
    expect(screen.queryByText(/No repository in/)).toBeNull();
  });

  it('keeps a map already drawn and says it could not be read again', async () => {
    world.down = ['/api/quests'];
    show();
    expect(await screen.findByRole('button', { name: /^engine, / })).toBeInTheDocument();
    expect(await screen.findByText('The service did not answer the last read, so this is what was read before.')).toBeInTheDocument();
  });

  it('says the card alone has no dependency recorded, on a code map of one module', async () => {
    world.codeMaps = { engine: { file: 'docs/code-map.json', modules: [{ id: 'solo', path: 'src/solo', summary: '' }], dependencies: [] } };
    show({ code: 'engine' });
    await screen.findByRole('button', { name: /^solo, / });
    expect(within(document.body).getByText('solo — no dependency recorded', { selector: '[data-choice-line]' })).toBeInTheDocument();
  });
});
