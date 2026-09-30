import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useAddFavorite, useBrowserSettings, useRemoveFavorite, useSetBrowser, useSetExtensions, useSetLinks,
} from '../shell';
import {
  Button, Card, failure, Icon, type Notify, PathText, Prose, SectionTitle, Segmented, SettingRow, useErrorNotify,
} from '../ui';
import type { BrowserDriver } from '../work/browserDrivers';

/**
 * Daoris's browser (CHR5, CHR7): its favorites, which it shows in a Daoris folder on its bookmarks
 * bar, and whether other software's Chrome extensions are offered or refused. The same two files
 * `daoris browser` edits (D50), which `daoris-browser` reads each time it starts, so the page says
 * that an edit shows at the next start rather than implying it shows now — except where the page's
 * links open (BRW7), which the page itself reads at each click, and says so. Who is driving it (BRW8)
 * leads the domain, as it does beside the strip's door.
 */
export function BrowserDomain({ notify, drivers, onAttend }: {
  notify: Notify;
  drivers: readonly BrowserDriver[];
  onAttend?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const state = useBrowserSettings();
  const add = useAddFavorite();
  const remove = useRemoveFavorite();
  const setExtensions = useSetExtensions();
  const setBrowser = useSetBrowser();
  const setLinks = useSetLinks();
  useErrorNotify(state.error, notify);
  const onError = failure(notify);

  const [address, setAddress] = useState('');
  const [title, setTitle] = useState('');
  const data = state.data;

  const keep = () => add.mutate(
    { address: address.trim(), ...(title.trim() ? { title: title.trim() } : {}) },
    {
      onSuccess: () => {
        notify(t('settings.browser.favorites.added', { title: title.trim() || address.trim() }));
        setAddress('');
        setTitle('');
      },
      onError,
    });

  return (
    <>
      <Prose className="mb-3">{t('settings.browser.nextStart')}</Prose>

      {/* Which browser (BRW12): Daoris's own, or the person's Edge on a profile of Daoris's. */}
      <Card id="settings-which-browser" className="scroll-mt-3">
        <SectionTitle>{t('settings.browser.which.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.which.label')}
          hint={data?.browser === 'edge'
            ? t('settings.browser.which.hintEdge', { profile: data.edgeProfile })
            : t('settings.browser.which.hintDaoris')}
          control={data && (
            <Segmented
              label={t('settings.browser.which.label')}
              value={data.browser}
              options={[
                { value: 'daoris', label: t('settings.browser.which.daoris') },
                { value: 'edge', label: t('settings.browser.which.edge') },
              ]}
              onChange={(browser) => setBrowser.mutate({ browser }, {
                onSuccess: () => notify(t('settings.browser.which.set', {
                  choice: t(browser === 'edge' ? 'settings.browser.which.edge' : 'settings.browser.which.daoris'),
                })),
                onError,
              })}
            />
          )}
        />
        {/* Whose hands are on it (BRW8): the running sessions handed it, each a door into Sessions. */}
        <SettingRow
          label={t('settings.browser.driving.label')}
          hint={t('settings.browser.driving.hint')}
          why={t('browser.driving.tip')}
          control={drivers.length === 0
            ? <span className="text-small text-ink-faint">{t('settings.browser.driving.none')}</span>
            : (
              <span className="flex min-w-0 flex-wrap justify-end gap-1.5">
                {drivers.map((driver) => (onAttend
                  ? (
                    <Button
                      key={driver.id}
                      variant="ghost"
                      aria-label={t('settings.browser.driving.open', { name: driver.name })}
                      onClick={() => onAttend(driver.id)}
                    >
                      <Icon name="frameWork" size={12} />
                      {driver.name}
                    </Button>
                  )
                  : <span key={driver.id} className="text-small text-ink">{driver.name}</span>))}
              </span>
            )}
        />
        {data && !data.edgeFound && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.which.noEdge')}
          </p>
        )}
        {data?.browser === 'edge' && (
          <Prose className="mt-3 text-small">{t('settings.browser.ownOnly')}</Prose>
        )}
      </Card>

      {/* Where the page's links open (BRW7): the system's browser, or the one chosen above. The page
          reads this at each click, so unlike the rest of the domain it holds at once. */}
      <Card id="settings-links" className="mt-3.5 scroll-mt-3">
        <SettingRow
          label={t('settings.browser.links.label')}
          hint={t('settings.browser.links.hint')}
          why={t('settings.browser.links.why')}
          control={data && (
            <Segmented
              label={t('settings.browser.links.label')}
              value={data.links ?? 'system'}
              options={[
                { value: 'system', label: t('settings.browser.links.system') },
                { value: 'daoris', label: t('settings.browser.links.daoris') },
              ]}
              onChange={(links) => setLinks.mutate({ links }, {
                onSuccess: () => notify(t(links === 'daoris' ? 'settings.browser.links.setDaoris' : 'settings.browser.links.setSystem')),
                onError,
              })}
            />
          )}
        />
      </Card>

      <Card id="settings-favorites" className="mt-3.5 scroll-mt-3">
        <SectionTitle>{t('settings.browser.favorites.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.favorites.label')}
          hint={t('settings.browser.favorites.hint')}
          control={data && <PathText path={data.favoritesPath} className="text-small text-ink-faint" />}
        />
        {data?.favoritesProblem && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.unreadable', { file: data.favoritesPath, problem: data.favoritesProblem })}
          </p>
        )}
        {data && data.favorites.length === 0 && !data.favoritesProblem && (
          <Prose className="mt-3">{t('settings.browser.favorites.none')}</Prose>
        )}
        {data && data.favorites.length > 0 && (
          <ul className="m-0 mt-3 list-none p-0">
            {data.favorites.map((favorite) => (
              <li
                key={favorite.url}
                className="flex flex-wrap items-baseline gap-3 border-t border-line py-2 first:border-t-0"
              >
                <span className="text-body text-ink">{favorite.title}</span>
                <PathText path={favorite.url} className="text-small text-ink-faint" />
                <Button
                  variant="ghost"
                  className="ml-auto"
                  disabled={remove.isPending}
                  onClick={() => remove.mutate({ address: favorite.url }, {
                    onSuccess: () => notify(t('settings.browser.favorites.removed', { url: favorite.url })),
                    onError,
                  })}
                >
                  {t('settings.browser.favorites.remove')}
                </Button>
              </li>
            ))}
          </ul>
        )}
        {/* The rule runs the card's width, as the list's rows do; the fields are sized to what they
            hold, an address long and a title short. */}
        <div className="mt-3 border-t border-line pt-3">
        <div className="grid max-w-[48rem] items-end gap-2 md:grid-cols-[minmax(0,1fr)_12rem_auto]">
          <label className="grid gap-1 text-small text-ink-faint">
            {t('settings.browser.favorites.address')}
            <input
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder="https://…"
              className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
            />
          </label>
          <label className="grid gap-1 text-small text-ink-faint">
            {t('settings.browser.favorites.titleField')}
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder={t('settings.browser.favorites.titlePlaceholder')}
              className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
            />
          </label>
          <Button variant="primary" disabled={!address.trim() || add.isPending} onClick={keep}>
            <Icon name="plus" size={13} />
            {t('settings.browser.favorites.add')}
          </Button>
        </div>
        </div>
      </Card>

      <Card id="settings-extensions" className="mt-3.5 scroll-mt-3">
        <SectionTitle>{t('settings.browser.extensions.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.extensions.label')}
          hint={t('settings.browser.extensions.hint')}
          control={data && (
            <Segmented
              label={t('settings.browser.extensions.label')}
              value={data.extensions}
              options={[
                { value: 'offer', label: t('settings.browser.extensions.offer') },
                { value: 'refuse', label: t('settings.browser.extensions.refuse') },
              ]}
              onChange={(extensions) => setExtensions.mutate({ extensions }, {
                onSuccess: () => notify(t('settings.browser.extensions.set', {
                  choice: t(extensions === 'refuse' ? 'settings.browser.extensions.refused' : 'settings.browser.extensions.offered'),
                })),
                onError,
              })}
            />
          )}
        />
        {data?.settingsProblem && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.unreadable', { file: data.settingsPath, problem: data.settingsProblem })}
          </p>
        )}
      </Card>
    </>
  );
}
