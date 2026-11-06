import { useTranslation } from 'react-i18next';
import type { HelpReceipt } from './wired';

/**
 * What the agent's door said it loaded, beside what the start handed (HELPWIRED1b; the modes design §6): the servers it listed with the
 * status it gave each, then where the two disagree, a server handed and not listed, a server listed and not handed by Daoris; and the
 * count of its tools and skills. Read from the init frame the start recorded, never worked out again.
 *
 * @remarks
 * A molecule: the receipt arrives read. A door that sends no init frame (the protocol door) or a start before the frame was kept says
 * so, rather than reading as nothing loaded.
 */
export function WhatItSaidLoaded({ receipt }: { receipt: HelpReceipt }) {
  const { t } = useTranslation();
  const loaded = receipt.loaded;
  if (!loaded) return <span className="text-ink-soft">{t('help.wired.loaded.none')}</span>;

  const listed = new Set(loaded.servers.map((server) => server.name));
  const handed = new Set(receipt.servers.filter((server) => server.state !== 'withheld').map((server) => server.name));
  const notListed = [...handed].filter((name) => !listed.has(name));

  return (
    <div className="grid min-w-0 gap-1">
      <span className="text-ink-soft">{t('help.wired.loaded.counts', { tools: loaded.tools.length, skills: loaded.skills.length })}</span>
      <ul className="m-0 grid list-none gap-1 p-0">
        {loaded.servers.map((server) => (
          <li key={server.name}>
            <code>{server.name}</code>
            <span className="text-ink-soft"> · {server.status || t('help.wired.loaded.noStatus')}</span>
            {!handed.has(server.name) && <span className="text-ink-faint"> · {t('help.wired.loaded.unhanded')}</span>}
          </li>
        ))}
        {notListed.map((name) => (
          <li key={`not-listed:${name}`}>
            <code>{name}</code>
            <span className="text-warn"> · {t('help.wired.loaded.notListed')}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
