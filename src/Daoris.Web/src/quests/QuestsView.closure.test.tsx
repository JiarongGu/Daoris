import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { QuestsView } from '../test/questsView';
import { REOPENED, SEALED, STANDING } from './fixtures';

// FABRIC-CLOSE1c3 (§E5; D156): Quests hands its page the reopen where a live step stands on the quest's submission, sending the
// submission the page showed to the service's own door, and the go-on of a step its source's reopen held, once the source is ready.

const posted: { url: string; body: unknown }[] = [];

function show(quests: unknown[]) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (init?.method === 'POST') {
      posted.push({ url, body: JSON.parse(String(init.body ?? '{}')) });
      return Response.json({ quest: quests[0], message: 'Moved.' });
    }
    if (url.startsWith('/api/quests')) return Response.json(quests);
    if (url.startsWith('/api/sessions') || url.startsWith('/api/registry') || url.startsWith('/api/asks')) return Response.json([]);
    throw new Error(`unstubbed request: ${url}`);
  }));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider><QuestsView notify={() => {}} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Quests hands its page the reopen and the go-on', () => {
  beforeEach(() => { posted.length = 0; });
  afterEach(() => { vi.unstubAllGlobals(); });

  it('reopens the submission the page showed, at the service’s own door', async () => {
    show([SEALED, STANDING]);

    await userEvent.click(await screen.findByRole('button', { name: /Stream the tiles over the v3 bridge/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Reopen…' }));
    const ask = screen.getByRole('group', { name: 'reopen this submission' });
    expect(ask).toHaveTextContent(`#${STANDING.id}`);
    await userEvent.type(within(ask).getByRole('textbox'), 'It seams.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Reopen submission' }));

    await waitFor(() => expect(posted).toEqual([
      { url: `/api/quests/${SEALED.id}/reopen`, body: { submission: { machine: 'desk', sequence: 41 }, note: 'It seams.' } },
    ]));
  });

  it('goes on with a held step once its source has an unheld submission again', async () => {
    show([REOPENED, { ...SEALED, submission: { ...SEALED.submission!, sequence: 52 } }]);

    // Its row is the first press that names it.
    await userEvent.click((await screen.findAllByRole('button', { name: /Verify the tiles in a playtest/ }))[0]);
    await userEvent.click(await screen.findByRole('button', { name: 'Go on with it' }));

    await waitFor(() => expect(posted).toEqual([{ url: `/api/quests/${REOPENED.id}/continue`, body: {} }]));
  });
});
