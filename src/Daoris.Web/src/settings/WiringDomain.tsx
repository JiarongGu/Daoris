import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  type DownloadCleared, refusedNoRoute, useClearDownloads, useOpenLogFolder, useSystemDownloads, useSystemSpace, useSystemWiring,
  useUpdateState,
} from '../bridge/shell';
import { useSetKeep } from '../bridge/driver';
import { failure, useErrorNotify } from '../design/ui';
import { sentence } from '../locales/format';
import type { StarterDoor } from '../help/starters';
import { DomainLoading } from './DomainLoading';
import type { SettingsDomainProps } from './domains';
import { WiringCards } from './wiring/WiringCards';
import { WiringDownloads } from './wiring/WiringDownloads';
import { WiringOlder } from './wiring/WiringHead';
import { WiringSpace } from './wiring/WiringSpace';

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
 * reader (`SYSTEM_WIRING`, WIRING1b) and drawn as cards. **It shows, and sets only the keep rules**: the domain that sets a thing
 * keeps its control, and a row whose value is set elsewhere carries a door to where. Its acts are the Downloads row's clearing
 * (CACHECLEAR1), listed first and then pressed, which removes only what nothing uses, and the Disk space card's *Change…* (KEEP1d),
 * the keep rules, which have no other place on the page (the cleanup design §8, the this-Daoris design §9).
 *
 * - **Machine-only** like every machine domain (D47 §4): a browser is never offered it, and no host route answers any of it.
 * - **Sizes are asked once**, when the page first has its answer: the open itself walks no folder, and the answer to the ask
 *   says it is measuring, which the query reads again every two seconds until each size is counted.
 * - **A shell older than the page** answers the route `NO_ROUTE` (`DriverModule.UnknownType`); the page says so in one line,
 *   and Driver's home row, kept as the fallback, is what that shell can show.
 *
 * The cards are molecules over props, composed by `WiringCards` so a story draws the whole domain (R6); this holds the hooks
 * (`presentational.test.ts`).
 */
export function WiringDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  const { query, measure } = useSystemWiring();
  // The update is the shell's own state, not the wiring's (UPDATE1b): the Versions card's line.
  const update = useUpdateState();
  // The one press of this page that opens a folder, the Machine log's own (LOG1c); the rest are WIRING1e's.
  const openLogs = useOpenLogFolder();
  const [busy, setBusy] = useState(false);
  // CACHECLEAR1: the Downloads row's list, read once the person opens it, and the one press after it (D88's shape).
  // KEEP1c: which row opened it, the Folders card's or the Disk space card's, so the list is drawn under the row that was pressed.
  const [downloadsOpen, setDownloadsOpen] = useState<false | 'folders' | 'space'>(false);
  const [cleared, setCleared] = useState<DownloadCleared[] | null>(null);
  const downloads = useSystemDownloads(downloadsOpen !== false);
  // KEEP1c: the Disk space card's read; its sizes are counted only when the person asks, or by the look once a day.
  const space = useSystemSpace();
  const [counting, setCounting] = useState(false);
  const spaceOlder = space.query.isError && refusedNoRoute(space.query.error);
  useErrorNotify(spaceOlder ? null : space.query.error, notify);
  const clearDownloads = useClearDownloads();
  // KEEP1d: each rule's *Change…*, the same `keep` `daoris driver keep` edits; a refusal is said under its editor.
  const setKeep = useSetKeep();
  useErrorNotify(downloads.error, notify);
  const asked = useRef(false);
  const older = query.isError && refusedNoRoute(query.error);
  // An older shell's refusal is the page's state, not a failure to tell the person of.
  useErrorNotify(older ? null : query.error, notify);

  const ask = () => {
    setBusy(true);
    measure().catch(failure(notify)).finally(() => setBusy(false));
  };

  const count = (kinds?: string[]) => {
    setCounting(true);
    space.count(kinds).catch(failure(notify)).finally(() => setCounting(false));
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
  const list = (
    <WiringDownloads
      listing={downloads.data}
      busy={clearDownloads.isPending}
      cleared={cleared}
      onClear={(rows) => clearDownloads.mutate(rows, {
        onSuccess: (answer) => setCleared(answer.cleared),
        onError: failure(notify),
      })}
      onClose={() => { setDownloadsOpen(false); setCleared(null); }}
    />
  );

  return (
    <WiringCards
      wiring={wiring}
      update={update.data}
      busy={busy}
      onMeasure={ask}
      onDownloads={() => { setCleared(null); setDownloadsOpen('folders'); }}
      downloads={downloadsOpen === 'folders' ? list : undefined}
      space={(
        <WiringSpace
          // An answer with no kinds is no disk space this page reads: drawn as still on its way, never as a card of nothing.
          space={Array.isArray(space.query.data?.kinds) ? space.query.data : undefined}
          older={spaceOlder}
          busy={counting}
          onCount={count}
          onDownloads={() => { setCleared(null); setDownloadsOpen('space'); }}
          downloads={downloadsOpen === 'space' ? list : undefined}
          onKeep={(set, answered) => setKeep.mutate(set, {
            onSuccess: () => answered.done(),
            onError: (error) => answered.refused(sentence(error)),
          })}
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
        tools: () => onGo({ view: 'settings', section: 'tools' }),
        ai: () => onGo({ view: 'settings', section: 'ai' }),
        logs: () => onGo({ view: 'settings', section: 'logs' }),
        file: (door) => onGo(FILE_DOORS[door] ?? { view: 'settings', section: 'driver' }),
      }}
    />
  );
}
