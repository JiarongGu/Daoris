import { useTranslation } from 'react-i18next';
import {
  useDriver, usePlugins, useSetAccess, useSetCap, useSetCoolOff, useSetGitAccess, useSetManifest, useSetNotify, useSetQuiet,
} from '../bridge/shell';
import { type MachineSetting, SETTINGS } from '../bridge/generated/routes';
import { failure } from '../design/ui';
import { sentence } from '../locales/format';
import { DriverCards } from './DriverCards';
import { accessToast } from './repository/Access';
import type { SettingsDomainProps } from './domains';

/** A machine setting as the driver's one list declares it. */
export function settingOf(key: string): MachineSetting {
  const setting = SETTINGS.find((each) => each.key === key);
  if (!setting) throw new Error(`no machine setting is named ${key}`);
  return setting;
}

/**
 * Settings → Agents and sessions: the driver's dials over
 * `driver.json`, the same file `daoris settings` edits. The file is the truth and this is an
 * editor over it: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, like every machine domain.** A browser over a keyed remote must never read where a
 * machine lives or syncs, and never re-point it, so the state lives behind the shell's bridge and the
 * service has no route onto it at all.
 *
 * **Where plugins are looked for** is a row of the dials, read-only, with the
 * door to the Plugins place: nothing about a plugin is set here. Its Plugins section lists each installed plugin with what it holds set, each a door to its
 * page, where that is changed. Where this machine's Daoris lives is Disk space's first row, and the update is Updates'.
 *
 * The cards are `DriverCards`, a molecule over the shell's facts, so a story draws the domain at the frame's widths (R6);
 * this holds the hooks and says each change's toast (`presentational.test.ts`).
 */
export function DriverDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  // Whether this machine interrupts the person — the same `driver.json` field
  // `daoris settings notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  // The catalogue the Plugins place reads, for its folder and its plugins' settings. Where it names none, the row is absent.
  const plugins = usePlugins();
  const pluginsFolder = typeof plugins.data?.folder === 'string' ? plugins.data.folder : null;
  const setNotify = useSetNotify();
  const setCoolOff = useSetCoolOff();
  const setQuiet = useSetQuiet();
  const setCap = useSetCap();
  const setManifest = useSetManifest();
  // This machine's sessions' access: the default under every workspace's and repository's.
  const setAccess = useSetAccess();
  const setGitAccess = useSetGitAccess();
  const onError = failure(notify);

  return (
    <DriverCards
      minutes={{ cooloff: settingOf('cooloff'), quiet: settingOf('quiet'), manifest: settingOf('manifest') }}
      facts={{
        pluginsFolder,
        plugins: plugins.data?.plugins ?? null,
        notify: driver.data?.notify ?? true,
        coolOff: driver.data?.coolOff,
        quiet: driver.data?.quiet,
        manifestFiles: driver.data?.manifestFiles,
        manifestMinutes: driver.data?.manifestMinutes,
        cap: driver.data?.cap,
        machineAccess: driver.data?.machineAccess,
        machineGitAccess: driver.data?.machineGitAccess,
      }}
      busy={{ access: setAccess.isPending, gitAccess: setGitAccess.isPending }}
      // This machine's git access, as Settings → Git sets it.
      onGitAccess={(edit, answered) => setGitAccess.mutate({ machine: true, ...edit }, {
        onSuccess: () => answered?.done(),
        onError: (error) => (answered ? answered.refused(sentence(error)) : onError(error)),
      })}
      onGo={onGo}
      onNotify={(on) => setNotify.mutate({ notify: on }, {
        onSuccess: () => notify(t(on ? 'settings.notify.on' : 'settings.notify.off')),
        onError,
      })}
      onCoolOff={(value) => setCoolOff.mutate({ minutes: value }, {
        onSuccess: () => notify(t('settings.cooloff.set', { count: value })),
        onError,
      })}
      onQuiet={(value) => setQuiet.mutate({ minutes: value }, {
        onSuccess: () => notify(t('settings.quiet.set', { count: value })),
        onError,
      })}
      onManifest={(bound) => setManifest.mutate(bound, {
        onSuccess: () => notify('files' in bound
          ? t('settings.manifest.setFiles', { count: bound.files })
          : t('settings.manifest.setMinutes', { count: bound.minutes })),
        onError,
      })}
      onCap={(value) => setCap.mutate({ cap: value }, {
        onSuccess: () => notify(t('settings.cap.set', { count: value })),
        onError,
      })}
      onAccess={(edit, answered) => setAccess.mutate({ machine: true, ...edit }, {
        onSuccess: () => {
          notify(accessToast(t, t('settings.access.thisMachine'), edit, true));
          answered?.done();
        },
        onError: (error) => (answered ? answered.refused(sentence(error)) : onError(error)),
      })}
    />
  );
}
