import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { refusedNoRoute, useClearToolFolders, useOpenLogFolder, useSystemWiring, useUpdateState } from '../bridge/shell';
import { failure, useErrorNotify } from '../design/ui';
import { sentence } from '../locales/format';
import type { StarterDoor } from '../help/starters';
import { DomainLoading } from './DomainLoading';
import type { SettingsDomainProps } from './domains';
import { WiringFiles } from './WiringFiles';
import { WiringFolders } from './WiringFolders';
import { WiringHead, WiringOlder } from './WiringHead';
import { WiringKnowledge } from './WiringKnowledge';
import { WiringLogs } from './WiringLogs';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { WiringTerminal } from './WiringTerminal';
import { WiringToolHome } from './WiringToolHome';
import { WiringVariablesCard } from './WiringVariables';
import { WiringVersions } from './WiringVersions';

/** Where each file's door goes: the read names the place by a word, and the page knows its door (§3.4). */
const FILE_DOORS: Record<string, StarterDoor> = {
  driver: { view: 'settings', section: 'driver' },
  tools: { view: 'settings', section: 'tools' },
  browser: { view: 'settings', section: 'browser' },
  agents: { view: 'agents' },
  plugins: { view: 'plugins' },
  workspace: { view: 'projects', workspaceTab: 'setup', workspaceSection: 'remote' },
};

/**
 * Settings → This Daoris (WIRING1c, the this-Daoris design §2): how this install is set up here, read from the shell's one
 * reader (`SYSTEM_WIRING`, WIRING1b) and drawn as cards. **It shows and sets nothing**: the domain that sets a thing keeps its
 * control, and a row whose value is set elsewhere carries a door to where.
 *
 * - **Machine-only** like every machine domain (D47 §4): a browser is never offered it, and no host route answers any of it.
 * - **Sizes are asked once**, when the page first has its answer: the open itself walks no folder, and the answer to the ask
 *   says it is measuring, which the query reads again every two seconds until each size is counted.
 * - **A shell older than the page** answers the route `NO_ROUTE` (`DriverModule.UnknownType`); the page says so in one line,
 *   and Driver's home row, kept as the fallback, is what that shell can show.
 *
 * The cards are molecules over props; this holds the hooks (`presentational.test.ts`).
 */
export function WiringDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  const { query, measure } = useSystemWiring();
  // The update is the shell's own state, not the wiring's (UPDATE1b): the Versions card's line.
  const update = useUpdateState();
  // The one press of this page that opens a folder, the Machine log's own (LOG1c); the rest are WIRING1e's.
  const openLogs = useOpenLogFolder();
  // The person's press after the list of what Daoris's sessions left in their own tool home (OWNSPACE2).
  const clear = useClearToolFolders();
  const [busy, setBusy] = useState(false);
  const asked = useRef(false);
  const older = query.isError && refusedNoRoute(query.error);
  // An older shell's refusal is the page's state, not a failure to tell the person of.
  useErrorNotify(older ? null : query.error, notify);

  const ask = () => {
    setBusy(true);
    measure().catch(failure(notify)).finally(() => setBusy(false));
  };

  const answered = query.data !== undefined;
  useEffect(() => {
    if (!answered || asked.current) return;
    asked.current = true;
    ask();
    // The first answer is the trigger; `ask` closes over nothing that changes what it does.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [answered]);

  if (older) return <WiringOlder />;
  if (!query.data) return <DomainLoading rows={6} />;
  const wiring = query.data;

  return (
    <>
      <WiringHead kind={wiring.folder.kind} />
      <WiringFolders wiring={wiring} busy={busy} onMeasure={ask} />
      <WiringService wiring={wiring} />
      <WiringLoop loop={wiring.loop} onDriver={() => onGo({ view: 'settings', section: 'driver' })} />
      <WiringFiles files={wiring.files} onDoor={(door) => onGo(FILE_DOORS[door] ?? { view: 'settings', section: 'driver' })} />
      <WiringVariablesCard variables={wiring.variables} />
      <WiringVersions
        versions={wiring.versions}
        update={update.data}
        development={wiring.folder.kind === 'development'}
        onTools={() => onGo({ view: 'settings', section: 'tools' })}
        onDriver={() => onGo({ view: 'settings', section: 'driver' })}
      />
      <WiringTerminal terminal={wiring.terminal} variables={wiring.variables} />
      <WiringKnowledge
        knowledge={wiring.knowledge}
        tier={wiring.service.tier}
        tierWhy={wiring.service.tierWhy}
        onAi={() => onGo({ view: 'settings', section: 'ai' })}
      />
      <WiringLogs
        logs={wiring.logs}
        onOpenFolder={() => openLogs.mutate(undefined, {
          onSuccess: (answer) => {
            if (!answer.opened && answer.folder) notify(t('settings.logs.notOpened', { folder: answer.folder }));
          },
          onError: failure(notify),
        })}
        onLog={() => onGo({ view: 'settings', section: 'logs' })}
      />
      {wiring.toolHome && (
        <WiringToolHome
          toolHome={wiring.toolHome}
          measuring={wiring.measuring}
          onClear={(names, answered) => clear.mutate(names, {
            onSuccess: (outcome) => {
              // What stayed is said where the person is, with why; what went is the list's next read.
              if (outcome.kept.length > 0) {
                notify(t('settings.daoris.toolHome.kept', { count: outcome.kept.length, why: [...new Set(outcome.kept.map((each) => t(`settings.daoris.toolHome.why.${each.why}`, { defaultValue: each.why })))].join(', ') }));
              }
              answered.done();
            },
            onError: (error) => answered.refused(sentence(error)),
          })}
        />
      )}
    </>
  );
}
