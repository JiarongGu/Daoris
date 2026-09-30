import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora } from '@shenora/react';
import { keys } from '../queries';
import { sentence } from '../format';

// Daoris's browser (MOD3): its settings, where the page's links open, and the window itself.

/**
 * Daoris's browser, as a Settings domain (CHR5, CHR7): its favorites, shown in a Daoris folder on its
 * bookmarks bar, and whether other software's Chrome extensions are offered or refused.
 *
 * @remarks
 * Shell-only, like every machine domain: the same files `daoris browser` edits and `daoris-browser`
 * reads each time it starts, which the service has no route onto (D47 §4).
 */
export type BrowserSettingsState = {
  favoritesPath: string;
  favorites: { url: string; title: string }[];
  /** Why the favorites file gave none, when it was there and could not be read. */
  favoritesProblem: string | null;
  settingsPath: string;
  extensions: 'offer' | 'refuse';
  /** Which browser sessions drive and the person opens (BRW12). */
  browser: 'daoris' | 'edge';
  /** Whether this machine has an Edge for that choice to start. */
  edgeFound: boolean;
  /** The profile of Daoris's that Edge runs on — never the person's default, which cannot be driven. */
  edgeProfile: string;
  /**
   * Where a link on the page opens (BRW7): the system's browser, or Daoris's — whichever `browser` is.
   * Absent on a shell older than it, which is the system's.
   */
  links?: 'system' | 'daoris';
  settingsProblem: string | null;
};

const callBrowser = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.BROWSER', type, payload ? { payload } : {});

export const useBrowserSettings = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.browserSettings,
    queryFn: () => callBrowser<BrowserSettingsState>('STATE'),
    enabled: isAvailable,
  });
};

/** An edit to the browser's files. Each answers with the whole state, as the remotes' do. */
function useBrowserChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => callBrowser<BrowserSettingsState>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.browserSettings, state),
  });
}

export const useAddFavorite = () => useBrowserChange<{ address: string; title?: string }>('ADD_FAVORITE');

export const useRemoveFavorite = () => useBrowserChange<{ address: string }>('REMOVE_FAVORITE');

export const useSetExtensions = () => useBrowserChange<{ extensions: 'offer' | 'refuse' }>('SET_EXTENSIONS');

export const useSetBrowser = () => useBrowserChange<{ browser: 'daoris' | 'edge' }>('SET_BROWSER');

export const useSetLinks = () => useBrowserChange<{ links: 'system' | 'daoris' }>('SET_LINKS');

/**
 * What opens a link on the page in Daoris's browser (BRW7), or null where links open as links do: a
 * browser, which has no bridge and no Daoris's browser to open, and a machine whose `links` setting is
 * the system's — or a shell too old to say. `ExternalLink` honours whatever this answers, through the
 * `LinkOpener` the application provides.
 *
 * @remarks
 * The setting is read from the browser's settings, the cache Settings → Browser fills, so a change made
 * there holds at the next click; one made from a terminal holds once the page asks again. A refusal —
 * an address the shell will not open — is said in the shell's sentence.
 */
export function useLinkOpener(notify: (text: string, kind?: 'ok' | 'error') => void): ((address: string) => void) | null {
  const { isAvailable } = useShenora();
  const settings = useBrowserSettings();
  const open = useMutation({
    mutationFn: (url: string) =>
      getBridge().invoke<{ opened: boolean; windows: string[] }>('DAORIS.WINDOWS', 'OPEN_BROWSER', { payload: { url } }),
  });
  const { mutate } = open;
  const routed = isAvailable && settings.data?.links === 'daoris';
  return useMemo(
    () => (routed
      ? (address: string) => mutate(address, { onError: (error) => notify(sentence(error), 'error') })
      : null),
    [routed, mutate, notify],
  );
}

/**
 * Daoris's own browser (D78): the window the person signs in to and watches an agent use. Not a route
 * into this bundle like the others — it shows pages that are not Daoris's, with no bridge.
 */
export const useOpenBrowser = () => useMutation({
  mutationFn: () =>
    getBridge().invoke<{ opened: boolean; windows: string[] }>('DAORIS.WINDOWS', 'OPEN_BROWSER', {}),
});
