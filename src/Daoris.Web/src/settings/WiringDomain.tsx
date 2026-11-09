import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useOpenLogFolder, useSetupWiring, useSystemWiring } from '../bridge/shell';
import { failure, useErrorNotify } from '../design/ui';
import { sentence } from '../locales/format';
import type { StarterDoor } from '../help/starters';
import type { SetupAnswer } from '../data/wiring';
import type { Answered } from '../work/InlineConfirm';
import { DomainLoading } from './DomainLoading';
import type { SettingsDomainProps } from './domains';
import { setupSaid } from './wiring/setupSaid';
import { WiringAddressRow } from './wiring/WiringAddressRow';
import { WiringCards } from './wiring/WiringCards';
import { WiringPathRow } from './wiring/WiringPathRow';
import { WiringTerminalLine } from './wiring/WiringTerminalLine';

/** Where each file's door goes: the read names the place by a word, and the page knows its door. */
const FILE_DOORS: Record<string, StarterDoor> = {
  driver: { view: 'settings', section: 'driver' },
  tools: { view: 'settings', section: 'tools' },
  browser: { view: 'settings', section: 'browser' },
  agents: { view: 'agents' },
  plugins: { view: 'plugins' },
  workspace: { view: 'projects', workspaceSection: 'remote' },
};

/**
 * Settings → This Daoris: how this install is wired here, from the shell's one reader (`SYSTEM_WIRING`): the terminal and
 * the service, then the facts behind them. **It shows, and sets only the account's wiring** (*Open* by a row's id, *Put on my
 * PATH*, *Copy a terminal line*, *Change the service's address*, each said first inside its row); a row set elsewhere carries
 * a door to where. Machine-only, as every machine domain is. The sizes (the logs', the tool home's) are asked once, when the
 * page first has its answer, so the open itself walks no folder. `WiringCards` draws the cards; this holds the hooks.
 */
export function WiringDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  const { query, measure } = useSystemWiring();
  // The Logs card's own *Open folder*; every other row opens by its id.
  const openLogs = useOpenLogFolder();
  // Setting it up from the page: Open by id, the account's PATH and the service's address, each said first inside its row.
  const setup = useSetupWiring();
  const asked = useRef(false);
  useErrorNotify(query.error, notify);

  const answered = query.data !== undefined;
  useEffect(() => {
    if (!answered || asked.current) return;
    asked.current = true;
    measure().catch(failure(notify));
    // The first answer is the trigger; `measure` closes over nothing that changes what it does.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [answered]);

  if (!query.data) return <DomainLoading rows={6} />;
  const wiring = query.data;
  const accountAddress = wiring.variables.find((variable) => variable.name === 'DAORIS_SERVICE_URL');
  // A press's end told to its ask: done, or the refusal in the catalogue's words. The wiring is read again as the route declares.
  const ended = (answered: Answered) => ({
    onSuccess: (answer: SetupAnswer) => (answer.done ? answered.done() : answered.refused(setupSaid(t, answer.why, answer.message))),
    onError: (error: unknown) => answered.refused(sentence(error)),
  });

  return (
    <WiringCards
      wiring={wiring}
      onOpen={(folder) => setup.open.mutate(folder, {
        onSuccess: (answer) => { if (!answer.done) notify(setupSaid(t, answer.why, answer.message)); },
        onError: failure(notify),
      })}
      terminalSetup={(
        <>
          <WiringPathRow terminal={wiring.terminal} busy={setup.path.isPending} onPress={(on, answered) => setup.path.mutate(on, ended(answered))} />
          <WiringTerminalLine
            facts={{ home: wiring.folder.path, url: wiring.service.url, bin: wiring.terminal.bin ?? null }}
            onCopy={(line) => { void navigator.clipboard?.writeText(line).then(() => notify(t('settings.daoris.setup.line.copied')), () => {}); }}
          />
        </>
      )}
      addressSetup={(
        <WiringAddressRow
          account={accountAddress?.accountSet && accountAddress.account !== null ? accountAddress.account : null}
          busy={setup.address.isPending}
          onPress={(url, answered) => setup.address.mutate(url, ended(answered))}
        />
      )}
      onOpenLogFolder={() => openLogs.mutate(undefined, {
        onSuccess: (answer) => {
          if (!answer.opened && answer.folder) notify(t('settings.logs.notOpened', { folder: answer.folder }));
        },
        onError: failure(notify),
      })}
      doors={{
        driver: () => onGo({ view: 'settings', section: 'driver' }),
        ai: () => onGo({ view: 'settings', section: 'ai' }),
        logs: () => onGo({ view: 'settings', section: 'logs' }),
        file: (door) => onGo(FILE_DOORS[door] ?? { view: 'settings', section: 'driver' }),
      }}
    />
  );
}
