import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { type QueryKey, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The remaining presses (STATEHUB1h, D169): each hook below asked again by hand, after its press, what its route now declares it
// changes. The route publishes it, and the window's state hub asks it again, as every other window's does; the hook asks
// nothing again itself. The mocked shell publishes what each press declares, as `StatePublisher` does (`test/publishing.ts`).

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear: what the mocked shell publishes after a press reaches it, as the kit's pump would.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { keys } from '../../data/queries';
import { useAccountUse } from '../agents/accounts';
import { useHarnessAction, useRefreshHarnesses, useSetAgentSettings } from '../agents/agents';
import { useSetReadAcross, useSetWriteAcross } from '../repositories/across';
import { channelKey, usePostChannel } from '../sessions/asks';
import { useAddFavorite, useRemoveFavorite, useSetBrowser, useSetExtensions, useSetLinks } from '../browser';
import { useParkGoAhead } from '../sessions/conversation';
import { useSetActor, useTrustFolder } from '../driver';
import { HELP_HISTORY, useHelpStartFrom, usePinHelp, useRenameHelp, useStartHelp } from '../help';
import { useSetCommits, useSetHelpers, useSetLanguage, useSetLine, useSetOpinion, useSetReview } from '../repositories/lines';
import { useStateHub } from './stateHub';
import { ALL_TOOLS, useToolDelete, useToolDownload, useToolLocation, useToolsLook, useToolUse } from '../agents/tools';

type Press = { mutateAsync: (variables: never) => Promise<unknown> };

/** Each hook, what it is pressed with, and the family it used to ask again by hand. */
const presses: [hook: string, usePress: () => Press, variables: unknown, family: QueryKey[]][] = [
  ['useSetLine', useSetLine, { repository: 'engine', branch: 'main' }, [keys.lines]],
  ['useSetLanguage', useSetLanguage, { repository: 'engine', language: 'en' }, [keys.lines]],
  ['useSetReview', useSetReview, { repository: 'engine' }, [keys.lines]],
  ['useSetOpinion', useSetOpinion, { repository: 'engine' }, [keys.lines]],
  ['useSetCommits', useSetCommits, { repository: 'engine' }, [keys.lines]],
  ['useSetHelpers', useSetHelpers, { repository: 'engine' }, [keys.lines]],
  ['useSetReadAcross', useSetReadAcross, { repository: 'engine', read: true }, [keys.across]],
  ['useSetWriteAcross', useSetWriteAcross, { repository: 'engine', to: 'game', allow: true }, [keys.across]],
  // MODELROLE1d: Intake's order moves what an intake in each circle runs on, as SET_INTAKE's did.
  ['useSetActor', useSetActor, { actor: 'intake', choices: [{ agent: 'claude-code' }] }, [keys.allStarts]],
  ['useTrustFolder', useTrustFolder, { folder: 'engine', trustFile: 'trust.json' }, [keys.driver]],
  ['useAccountUse', useAccountUse, { harness: 'claude-code', action: 'use' }, [keys.accounts, keys.harnesses]],
  ['useHarnessAction', useHarnessAction, { harness: 'claude-code', action: 'profile-rename', profile: 'work' },
    [keys.harnesses, keys.accounts]],
  ['useSetAgentSettings', useSetAgentSettings, { harness: 'claude-code', profile: 'work', model: 'opus' }, [keys.harnesses]],
  // HARNESSREFRESH1: *Read again* moved the roster, the starts an account's reading lets through and the accounts, which sit under it.
  ['useRefreshHarnesses', useRefreshHarnesses, { agent: 'claude-code', profile: 'work' }, [keys.harnesses, keys.allStarts, keys.accounts]],
  ['usePostChannel', usePostChannel, { ask: 'a1', post: { text: 'hi' } }, [channelKey('a1')]],
  ['useParkGoAhead', useParkGoAhead, { id: 's1', ask: 'a1', number: 1, approved: true }, [keys.allSessions, keys.allAsks]],
  ['useToolUse', useToolUse, { tool: 'git', action: 'system' }, [[...ALL_TOOLS, 'list', false]]],
  ['useToolDownload', useToolDownload, { tool: 'git', version: '2.47.0' }, [[...ALL_TOOLS, 'list', false]]],
  ['useToolDelete', useToolDelete, { tool: 'git', version: '2.46.0' }, [[...ALL_TOOLS, 'list', false]]],
  ['useToolsLook', useToolsLook, 1, [[...ALL_TOOLS, 'list', true]]],
  ['useToolLocation', useToolLocation, { action: 'add', address: 'https://tools.example' }, [[...ALL_TOOLS, 'list', false]]],
  // BROWSERPUBLISH1: the browser module's own presses (`DAORIS.BROWSER`), each answering the whole state it also declares.
  ['useAddFavorite', useAddFavorite, { address: 'https://example.com' }, [keys.browserSettings]],
  ['useRemoveFavorite', useRemoveFavorite, { address: 'https://example.com' }, [keys.browserSettings]],
  ['useSetExtensions', useSetExtensions, { extensions: 'refuse' }, [keys.browserSettings]],
  ['useSetBrowser', useSetBrowser, { browser: 'edge' }, [keys.browserSettings]],
  ['useSetLinks', useSetLinks, { links: 'daoris' }, [keys.browserSettings]],
  ['useStartHelp', useStartHelp, undefined, [keys.allSessions]],
  ['useRenameHelp', useRenameHelp, { id: 'h1', name: 'Lines' }, [[...HELP_HISTORY, ''], keys.allSessions]],
  ['usePinHelp', usePinHelp, { id: 'h1', pinned: true }, [[...HELP_HISTORY, ''], keys.allSessions]],
  ['useHelpStartFrom', useHelpStartFrom, { id: 'h1', workspace: 'games' }, [[...HELP_HISTORY, '*', ''], keys.allSessions]],
];

/** A client holding an answer under each key of `family`, and the hook rendered in it, with the window's hub or without. */
function pressing(usePress: () => Press, family: QueryKey[], hub: boolean) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  for (const key of family) client.setQueryData(key, { held: true });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  const { result } = renderHook(() => {
    if (hub) useStateHub();
    return usePress();
  }, { wrapper });
  return { result, invalid: (key: QueryKey) => client.getQueryState(key)?.isInvalidated ?? false };
}

describe('the remaining presses, heard through the state hub (STATEHUB1h)', () => {
  afterEach(() => {
    invoke.mockReset();
    heard.clear();
  });

  it.each(presses)('%s: the hub asks again what its route published', async (_hook, usePress, variables, family) => {
    invoke.mockImplementation(async () => ({}));
    const { result, invalid } = pressing(usePress, family, true);

    await act(async () => { await result.current.mutateAsync(variables as never); });

    await waitFor(() => { for (const key of family) expect(invalid(key)).toBe(true); });
  });

  it.each(presses)('%s: asks nothing again itself', async (_hook, usePress, variables, family) => {
    invoke.mockImplementation(async () => ({}));
    const { result, invalid } = pressing(usePress, family, false);

    await act(async () => { await result.current.mutateAsync(variables as never); });
    await act(() => new Promise((settled) => { setTimeout(settled, 10); }));

    for (const key of family) expect(invalid(key)).toBe(false);
  });

  /**
   * A go-ahead refused as an error publishes nothing (D169 point 5), yet the park may not have gone on after its go-ahead was
   * answered: the window that pressed asks the sessions and the asks again itself.
   */
  it('asks the sessions and the asks again itself after a go-ahead refused as an error', async () => {
    invoke.mockImplementation(async () => { throw new Error('the park could not be sent on'); });
    const family = [keys.allSessions, keys.allAsks];
    const { result, invalid } = pressing(useParkGoAhead, family, false);

    await act(async () => {
      await result.current.mutateAsync({ id: 's1', ask: 'a1', number: 1, approved: true } as never).catch(() => undefined);
    });

    await waitFor(() => { for (const key of family) expect(invalid(key)).toBe(true); });
  });
});
