import { useTranslation } from 'react-i18next';
import {
  useDriver, usePlugins, useSayUpdate, useSetAccess, useSetCoolOff, useSetNotify, useSetStrikes, useUpdateState,
} from '../bridge/shell';
import { useSystemWiring } from '../bridge/system';
import { failure } from '../design/ui';
import { sentence } from '../locales/format';
import { DriverCards } from './DriverCards';
import { accessToast } from './repository/Access';
import type { SettingsDomainProps } from './domains';

/**
 * The Driver domain (D75): where this machine's Daoris lives, and the driver's two dials over
 * `driver.json`, the same file `daoris driver` edits (D50). The file is the truth and this is an
 * editor over it: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, like every machine domain.** A browser over a keyed remote must never read where a
 * machine lives or syncs, and never re-point it, so the state lives behind the shell's bridge and the
 * service has no route onto it at all. For the wiring, the key goes in and never comes out: what is
 * rendered is the audit prefix a deployment's own `keys list` prints (`WiringSettings`).
 *
 * **Where plugins are looked for** is a fact of the home, so it is a row under it since UX6j (D150 §2.3; the plugins
 * design §5), read-only, with the door to the Plugins place: Settings → Plugins retired there, since nothing about a
 * plugin is set here.
 *
 * The cards are `DriverCards`, a molecule over the shell's facts, so a story draws the domain at the frame's widths (R6);
 * this holds the hooks and says each change's toast (`presentational.test.ts`).
 */
export function DriverDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  // The catalogue the Plugins place reads, asked for its folder alone. An older shell names none, and the row is absent.
  const plugins = usePlugins();
  const pluginsFolder = typeof plugins.data?.folder === 'string' ? plugins.data.folder : null;
  // Whether this shell answers This Daoris's read (WIRING1c): by its schema, so an older shell's refusal and a stranger's
  // answer both leave the row as it was. The open asks no measure, so it walks no folder.
  const wiring = useSystemWiring();
  const placeAnswers = typeof wiring.query.data?.schema === 'number';
  const setNotify = useSetNotify();
  const setStrikes = useSetStrikes();
  const setCoolOff = useSetCoolOff();
  // The install's update (UPDATE1b, D139 §6): the query the banner reads, and the same word it says.
  const update = useUpdateState();
  const sayUpdate = useSayUpdate();
  // This machine's sessions' access (AUTONOMY1d, D184 point 1): the default under every workspace's and repository's.
  const setAccess = useSetAccess();
  const onError = failure(notify);

  return (
    <DriverCards
      facts={{
        home: driver.data?.home,
        homeAccount: driver.data?.homeAccount,
        homeNotice: driver.data?.homeNotice,
        hostNotice: driver.data?.hostNotice,
        placeAnswers,
        pluginsFolder,
        notify: driver.data?.notify ?? true,
        strikes: driver.data?.strikes ?? 3,
        coolOff: driver.data?.coolOff,
        machineAccess: driver.data?.machineAccess,
        update: update.data,
      }}
      busy={{ access: setAccess.isPending, update: sayUpdate.isPending }}
      onGo={onGo}
      onNotify={(on) => setNotify.mutate({ notify: on }, {
        onSuccess: () => notify(t(on ? 'settings.notify.on' : 'settings.notify.off')),
        onError,
      })}
      onStrikes={(value) => setStrikes.mutate({ strikes: value }, {
        onSuccess: () => notify(t(value === 0 ? 'settings.strikes.never' : 'settings.strikes.set', { count: value })),
        onError,
      })}
      onCoolOff={(value) => setCoolOff.mutate({ minutes: value }, {
        onSuccess: () => notify(t('settings.cooloff.set', { count: value })),
        onError,
      })}
      onAccess={(edit, answered) => setAccess.mutate({ machine: true, ...edit }, {
        onSuccess: () => {
          notify(accessToast(t, t('settings.access.thisMachine'), edit, true));
          answered?.done();
        },
        onError: (error) => (answered ? answered.refused(sentence(error)) : onError(error)),
      })}
      onSayUpdate={(mode) => sayUpdate.mutate(mode, { onError })}
    />
  );
}
