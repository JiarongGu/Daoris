import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  type DownloadCleared, type SpacePlan, type SpaceStarted, standingOf, useClearDownloads, useDriver, useOpenLogFolder,
  useSetStandingConfirmation, useSetupWiring, useSpaceFree, useSystemDownloads, useSystemSpace, useSystemWiring, useUpdateState,
} from '../bridge/shell';
import { useSetKeep } from '../bridge/driver';
import { failure, useErrorNotify } from '../design/ui';
import { sentence } from '../locales/format';
import { standingSaid } from '../person/standing';
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
import { WiringDownloads } from './wiring/WiringDownloads';
import { WiringSpace } from './wiring/WiringSpace';
import { WiringSpaceFree } from './wiring/WiringSpaceFree';
import { WiringStanding } from './wiring/WiringStanding';

/** Where each file's door goes: the read names the place by a word, and the page knows its door (§3.4). */
const FILE_DOORS: Record<string, StarterDoor> = {
  driver: { view: 'settings', section: 'driver' },
  tools: { view: 'settings', section: 'tools' },
  browser: { view: 'settings', section: 'browser' },
  agents: { view: 'agents' },
  plugins: { view: 'plugins' },
  workspace: { view: 'projects', workspaceSection: 'remote' },
};

/**
 * Settings → This Daoris (WIRING1c, the this-Daoris design §2): how this install is set up here, read from the shell's one
 * reader (`SYSTEM_WIRING`, WIRING1b) and drawn as cards. **It shows, and sets only the keep rules and the account's wiring**: the
 * domain that sets a thing keeps its control, and a row whose value is set elsewhere carries a door to where. Its acts are the Downloads row's clearing
 * (CACHECLEAR1), listed first and then pressed, which removes only what nothing uses; setting the wiring up (WIRING1e): *Open* by a
 * row's id, *Put on my PATH*, *Copy a terminal line* and *Change the service's address*, each said first inside its row, on the
 * account alone; the Disk space card's *Change…* (KEEP1d),
 * the keep rules, which have no other place on the page (the cleanup design §8, the this-Daoris design §9); and that card's *Free up
 * space…* (KEEP1e), listed first and then pressed, removing exactly what was listed.
 *
 * - **Machine-only** like every machine domain (D47 §4): a browser is never offered it, and no host route answers any of it.
 * - **Sizes are asked once**, when the page first has its answer: the open itself walks no folder, and the answer to the ask
 *   says it is measuring, which the query reads again every two seconds until each size is counted.
 *
 * The cards are molecules over props, composed by `WiringCards` so a story draws the whole domain (R6); this holds the hooks
 * (`presentational.test.ts`).
 */
export function WiringDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  const { query, measure } = useSystemWiring();
  // The update is the shell's own state, not the wiring's (UPDATE1b): the Versions card's line.
  const update = useUpdateState();
  // The Logs card's own *Open folder* (LOG1c); every other row opens by its id (WIRING1e, below).
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
  useErrorNotify(space.query.error, notify);
  const clearDownloads = useClearDownloads();
  // KEEP1d: each rule's *Change…*, the same `keep` `daoris settings keep` edits; a refusal is said under its editor.
  const setKeep = useSetKeep();
  // KEEP1e: Free up space…, listed first under the card's head, then pressed; a refusal stays beside the press (R3).
  const spaceFree = useSpaceFree();
  const [freeOpen, setFreeOpen] = useState(false);
  const [plan, setPlan] = useState<SpacePlan | undefined>(undefined);
  const [freeRefused, setFreeRefused] = useState<SpaceStarted | null>(null);
  useErrorNotify(downloads.error, notify);
  // TERMTRUST1a (D201): the Terminal card's standing confirmation, read from the driver's state; *Turn off*'s answer said under it.
  const standing = standingOf(useDriver().data?.standingConfirmation);
  const setStanding = useSetStandingConfirmation();
  const [offRefused, setOffRefused] = useState<string | null>(null);
  const [offUnwritten, setOffUnwritten] = useState(false);
  // WIRING1e: setting it up from the page: Open by id, the account's PATH and the service's address, each said first inside its row.
  const setup = useSetupWiring();
  const asked = useRef(false);
  useErrorNotify(query.error, notify);

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

  if (!query.data) return <DomainLoading rows={6} />;
  const wiring = query.data;
  const accountAddress = wiring.variables.find((variable) => variable.name === 'DAORIS_SERVICE_URL');
  // A press's end told to its ask: done, or the refusal in the catalogue's words. The wiring is read again as the route declares.
  const ended = (answered: Answered) => ({
    onSuccess: (answer: SetupAnswer) => (answer.done ? answered.done() : answered.refused(setupSaid(t, answer.why, answer.message))),
    onError: (error: unknown) => answered.refused(sentence(error)),
  });
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
          busy={counting}
          onCount={count}
          onDownloads={() => { setCleared(null); setDownloadsOpen('space'); }}
          downloads={downloadsOpen === 'space' ? list : undefined}
          onKeep={(set, answered) => setKeep.mutate(set, {
            onSuccess: () => answered.done(),
            onError: (error) => answered.refused(sentence(error)),
          })}
          onFreeUp={() => {
            setFreeOpen(true);
            setPlan(undefined);
            setFreeRefused(null);
            spaceFree.list.mutate(undefined, { onSuccess: setPlan, onError: failure(notify) });
          }}
          freeing={freeOpen ? (
            <WiringSpaceFree
              plan={plan}
              busy={spaceFree.free.isPending || spaceFree.list.isPending}
              refused={freeRefused}
              onFree={(units) => spaceFree.free.mutate(units, {
                // Started: the run's line says the rest, so the list closes; held: said here, the list kept open (R3).
                onSuccess: (answer) => {
                  if (answer.run) setFreeOpen(false);
                  else setFreeRefused(answer);
                },
                onError: failure(notify),
              })}
              onClose={() => setFreeOpen(false)}
            />
          ) : undefined}
        />
      )}
      standing={standing && (
        <WiringStanding
          standing={standing}
          busy={setStanding.isPending}
          refused={offRefused}
          unwritten={offUnwritten}
          onTurnOn={(presses, answered) => {
            setOffRefused(null);
            setOffUnwritten(false);
            setStanding.mutate(presses, {
              onSuccess: (answer) => (answer.done ? answered.done() : answered.refused(standingSaid(t, answer.why, answer.message))),
              onError: (error) => answered.refused(sentence(error)),
            });
          }}
          onTurnOff={() => {
            setOffRefused(null);
            setOffUnwritten(false);
            setStanding.mutate([], {
              // Off for this run, its record unwritten: said under the row, as the next start may read it as on.
              onSuccess: (answer) => {
                if (!answer.done) setOffRefused(standingSaid(t, answer.why, answer.message));
                else if (answer.why === 'unwritten') setOffUnwritten(true);
              },
              onError: (error) => setOffRefused(sentence(error)),
            });
          }}
        />
      )}
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
        tools: () => onGo({ view: 'settings', section: 'tools' }),
        ai: () => onGo({ view: 'settings', section: 'ai' }),
        logs: () => onGo({ view: 'settings', section: 'logs' }),
        file: (door) => onGo(FILE_DOORS[door] ?? { view: 'settings', section: 'driver' }),
      }}
    />
  );
}
