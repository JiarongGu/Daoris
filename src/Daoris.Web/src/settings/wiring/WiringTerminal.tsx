import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { PathText, Pill, SettingRow } from '../../design/ui';
import { moment } from '../../locales/format';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The two variables a terminal reads first (D171 point 1), read again from §3.5's rows. */
const ACCOUNT_VARIABLES = ['DAORIS_HOME', 'DAORIS_SERVICE_URL'] as const;

/** Which `PATH` holds the install's launchers, in the words of the design's three answers (§3.7). */
function onPath(terminal: Pick<SystemWiring['terminal'], 'onAccountPath' | 'onMachinePath'>): string | null {
  const { onAccountPath: account, onMachinePath: machine } = terminal;
  if (account === null && machine === null) return null;
  if (account && machine) return 'both';
  if (account) return 'account';
  if (machine) return 'machine';
  return 'neither';
}

/** What a terminal finds first for a program: this install's, another's with where, or none. */
function Found({ found }: { found: SystemWiring['terminal']['firstDaoris'] }) {
  const { t } = useTranslation();
  return (
    <span className="grid justify-items-end gap-0.5 text-small text-ink-soft @max-[26rem]:justify-items-start">
      <span>{t(`settings.daoris.terminal.found.${found.is}`)}</span>
      {found.is === 'another' && found.path && <PathText path={found.path} className="text-small text-ink-faint" />}
    </span>
  );
}

/**
 * Terminal (the this-Daoris design §3.7), read-only: whether each launcher is there (a development run has none, and says
 * so), which `PATH` holds them, what a new terminal runs for `daoris` and `daoris-driver`, the address file a terminal reads
 * to find the service and whether it takes it, and the account's `DAORIS_HOME` and `DAORIS_SERVICE_URL`, which a terminal
 * reads first. Only whether `PATH` holds the install's folder is shown, never the whole of it (§5).
 *
 * @remarks
 * A molecule: drawn from props. It sets nothing and copies nothing: *Copy terminal line* and the PATH presses are WIRING1e's.
 */
export function WiringTerminal({ terminal, variables }: {
  terminal: SystemWiring['terminal'];
  variables: SystemWiring['variables'];
}) {
  const { t } = useTranslation();
  const { address } = terminal;
  const path = onPath(terminal);

  return (
    <WiringCard id="settings-daoris-terminal" title={t('settings.daoris.card.terminal')}>
      <SettingRow label={t('settings.daoris.label.launchers')}>
        {terminal.launchersWhy
          ? <Unread why={terminal.launchersWhy} />
          : (
            <ul className="m-0 grid list-none gap-1.5 p-0">
              {terminal.launchers.map((launcher) => (
                <li key={launcher.name} className="flex flex-wrap items-baseline gap-x-3 text-small">
                  <span className="font-mono text-ink-soft">{launcher.name}</span>
                  <Pill tone={launcher.present ? 'done' : 'neutral'}>{t(launcher.present ? 'settings.daoris.pill.present' : 'settings.daoris.pill.missing')}</Pill>
                </li>
              ))}
            </ul>
          )}
      </SettingRow>
      <SettingRow
        label={t('settings.daoris.label.onPath')}
        control={path
          ? <span className="text-small text-ink-soft">{t(`settings.daoris.terminal.path.${path}`)}</span>
          : <Unread why={terminal.onAccountPathWhy ?? terminal.onMachinePathWhy} />}
      />
      <SettingRow label={t('settings.daoris.label.firstDaoris')} control={<Found found={terminal.firstDaoris} />} />
      <SettingRow label={t('settings.daoris.label.firstDriver')} control={<Found found={terminal.firstDriver} />} />
      <SettingRow
        label={t('settings.daoris.label.addressFile')}
        hint={address.present && address.pid !== null && address.since
          ? t('settings.daoris.loop.since', { pid: address.pid, when: moment(address.since) })
          : undefined}
        control={address.present
          ? <Fact value={address.url} why="malformed"><PathText path={address.url ?? ''} className="text-small text-ink-soft" /></Fact>
          : <Unread why={address.why} />}
      >
        {address.present && (
          <p className="m-0 text-small text-ink-soft">
            {address.taken === null
              ? <Unread why={address.why} />
              : (
                <>
                  <Pill tone={address.taken ? 'done' : 'declined'}>{t(address.taken ? 'settings.daoris.pill.taken' : 'settings.daoris.pill.notTaken')}</Pill>
                  {!address.taken && address.why && <>{' '}<Unread why={address.why} /></>}
                </>
              )}
          </p>
        )}
      </SettingRow>
      {ACCOUNT_VARIABLES.map((name) => {
        const found = variables.find((variable) => variable.name === name);
        return (
          <SettingRow
            key={name}
            label={t('settings.daoris.terminal.account', { name })}
            control={!found || found.accountSet === null
              ? <Unread why="window" />
              : found.accountSet && found.account !== null
                ? <PathText path={found.account} className="text-small text-ink-soft" />
                : <span className="text-small text-ink-faint">{t('settings.daoris.why.not-set')}</span>}
          />
        );
      })}
    </WiringCard>
  );
}
