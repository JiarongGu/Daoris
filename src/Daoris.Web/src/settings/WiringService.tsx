import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring, WiringLocated } from '../bridge/system';
import { PathText, Pill, SettingRow } from '../design/ui';
import { moment } from '../locales/format';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The variable each program's location may be named by (`ServiceHostLocator`, `KnowledgeConnector`). */
const HOST_VARIABLE = 'DAORIS_HTTP_HOST';
const CONNECTOR_VARIABLE = 'DAORIS_MCP_HOST';

/** Which of the held states wears which pill word. */
const HELD_WORD: Record<string, string> = { hosted: 'hosted', adopted: 'adopted', none: 'none' };

/**
 * Service (the this-Daoris design §3.2): its address and where it came from, who started it, whether it answers, its tier and
 * version, the host program and the connector handed to sessions with how each was found, whether the person key was proved
 * (never the key), and each workspace's remote with its key's audit prefix. A service that is not answering leads, with why.
 *
 * @remarks
 * A molecule: drawn from props. A fact the reader could not give is its own words in its row and changes no other row; a
 * source is a word the catalogue holds, and a key is its audit prefix as a deployment's own `keys list` prints it.
 */
export function WiringService({ wiring }: { wiring: Pick<SystemWiring, 'service' | 'remotes' | 'remotesWhy'> }) {
  const { t } = useTranslation();
  const { service, remotes, remotesWhy } = wiring;

  const source = (from: string | null, variable?: string): string | undefined =>
    (from ? t(`settings.daoris.from.${from}`, { name: variable }) : undefined);
  const located = (found: WiringLocated): ReactNode => (
    <Fact value={found.path} why={found.pathWhy}><PathText path={found.path ?? ''} className="text-small text-ink-soft" /></Fact>
  );

  const answers = (
    <SettingRow
      key="answers"
      label={t('settings.daoris.label.answers')}
      hint={service.asked ? t('settings.daoris.service.heard', { when: moment(service.asked) }) : undefined}
      control={service.answers === null
        ? <Unread why={service.answersWhy} />
        : <Pill tone={service.answers ? 'done' : 'declined'}>{t(service.answers ? 'settings.daoris.pill.answering' : 'settings.daoris.pill.silent')}</Pill>}
    >
      {service.answers === false && <p className="m-0 text-small text-ink-soft"><Unread why={service.answersWhy} /></p>}
    </SettingRow>
  );

  const rows = [
    <SettingRow
      key="address"
      label={t('settings.daoris.label.address')}
      hint={source(service.from, 'DAORIS_SERVICE_URL')}
      control={<Fact value={service.url} why={service.urlWhy}><PathText path={service.url ?? ''} className="text-small text-ink-soft" /></Fact>}
    />,
    <SettingRow
      key="held"
      label={t('settings.daoris.label.held')}
      control={service.held === null ? <Unread why={service.heldWhy} /> : <Pill>{t(`settings.daoris.pill.${HELD_WORD[service.held] ?? 'none'}`)}</Pill>}
    />,
    <SettingRow
      key="tier"
      label={t('settings.daoris.label.tier')}
      control={<Fact value={service.tier} why={service.tierWhy}><span className="text-small text-ink-soft">{service.tier}</span></Fact>}
    />,
    <SettingRow
      key="version"
      label={t('settings.daoris.label.version')}
      control={<Fact value={service.version} why={service.versionWhy}><span className="text-small text-ink-soft">{service.version}</span></Fact>}
    />,
    <SettingRow key="host" label={t('settings.daoris.label.host')} hint={source(service.host.from, HOST_VARIABLE)} control={located(service.host)} />,
    <SettingRow
      key="connector"
      label={t('settings.daoris.label.connector')}
      hint={source(service.connector.from, CONNECTOR_VARIABLE)}
      control={located(service.connector)}
    />,
    <SettingRow
      key="proved"
      label={t('settings.daoris.label.proved')}
      control={service.proved === null
        ? <Unread why={service.provedWhy} />
        : <Pill tone={service.proved ? 'done' : 'neutral'}>{t(service.proved ? 'settings.daoris.pill.proved' : 'settings.daoris.pill.unproved')}</Pill>}
    />,
  ];

  return (
    <WiringCard id="settings-daoris-service" title={t('settings.daoris.card.service')}>
      {/* Why it is not answering leads (§7), and the rest follows as ready. */}
      {service.answers === false ? [answers, ...rows] : [rows[0], answers, ...rows.slice(1)]}
      {remotesWhy ? <SettingRow label={t('settings.daoris.card.service')} control={<Unread why={remotesWhy} />} /> : remotes.map((remote) => (
        <SettingRow
          key={remote.workspace}
          label={remote.workspace}
          hint={(
            <span className="flex flex-wrap gap-x-3">
              <span>{remote.keySet ? t('settings.daoris.service.key', { prefix: remote.keyPrefix ?? '' }) : t('settings.daoris.service.noKey')}</span>
              <span>{t(`settings.daoris.from.${remote.from}`)}</span>
            </span>
          )}
          control={<Fact value={remote.url} why={remote.urlWhy}><PathText path={remote.url ?? ''} className="text-small text-ink-soft" /></Fact>}
        />
      ))}
    </WiringCard>
  );
}
