import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  type DownloadCleared, type SpacePlan, type SpaceStarted, useClearDownloads, useSetupWiring, useSpaceFree, useSystemDownloads, useSystemSpace, useSystemWiring,
} from '../bridge/shell';
import { useSetKeep } from '../bridge/driver';
import { failure, useErrorNotify } from '../design/ui';
import { sentence } from '../locales/format';
import { DomainLoading } from './DomainLoading';
import type { SettingsDomainProps } from './domains';
import { setupSaid } from './wiring/setupSaid';
import { WiringDownloads } from './wiring/WiringDownloads';
import { WiringFolders } from './wiring/WiringFolders';
import { WiringSpace } from './wiring/WiringSpace';
import { WiringSpaceFree } from './wiring/WiringSpaceFree';

/**
 * Settings → Disk space: what Daoris keeps on this disk. Its folders are one list from the shell's one reader
 * (`SYSTEM_WIRING`), each with its size and *Open folder*; then the Disk space card, its kinds, the keep rules and *Free up
 * space…*. Its acts are the Downloads row's clearing, listed first and then pressed, which removes only what nothing uses;
 * *Open folder* by a row's id; each rule's *Change…*; and *Free up space…*, listed first and then pressed, removing exactly what
 * is listed.
 *
 * - **Machine-only** like every machine domain: a browser is never offered it, and no host route answers any of it.
 * - **Sizes are asked once**, when the page first has its answer: the open itself walks no folder, and the answer to the ask
 *   says it is measuring, which the query reads again every two seconds until each size is counted.
 */
export function SpaceDomain({ notify }: Pick<SettingsDomainProps, 'notify'>) {
  const { t } = useTranslation();
  const { query, measure } = useSystemWiring();
  const [busy, setBusy] = useState(false);
  // The Downloads row's list, read once the person opens it, and the one press after it.
  // Which row opened it, the Folders card's or the Disk space card's, so the list is drawn under the row that is pressed.
  const [downloadsOpen, setDownloadsOpen] = useState<false | 'folders' | 'space'>(false);
  const [cleared, setCleared] = useState<DownloadCleared[] | null>(null);
  const downloads = useSystemDownloads(downloadsOpen !== false);
  // The Disk space card's read; its sizes are counted only when the person asks, or by the look once a day.
  const space = useSystemSpace();
  const [counting, setCounting] = useState(false);
  const clearDownloads = useClearDownloads();
  // Each rule's *Change…*, the same `keep` `daoris settings keep` edits; a refusal is said under its editor.
  const setKeep = useSetKeep();
  // Free up space…, listed first under the card's head, then pressed; a refusal stays beside the press (R3).
  const spaceFree = useSpaceFree();
  const [freeOpen, setFreeOpen] = useState(false);
  const [plan, setPlan] = useState<SpacePlan | undefined>(undefined);
  const [freeRefused, setFreeRefused] = useState<SpaceStarted | null>(null);
  const setup = useSetupWiring();
  const asked = useRef(false);
  useErrorNotify(space.query.error, notify);
  useErrorNotify(downloads.error, notify);
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
    <>
      <WiringFolders
        wiring={query.data}
        busy={busy}
        onMeasure={ask}
        onDownloads={() => { setCleared(null); setDownloadsOpen('folders'); }}
        downloads={downloadsOpen === 'folders' ? list : undefined}
        onOpen={(folder) => setup.open.mutate(folder, {
          onSuccess: (answer) => { if (!answer.done) notify(setupSaid(t, answer.why, answer.message)); },
          onError: failure(notify),
        })}
      />
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
    </>
  );
}
