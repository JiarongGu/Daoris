import { useEffect, useRef, useState } from 'react';
import { refusedNoRoute } from '../bridge/call';
import { useSystemWiring } from '../bridge/system';
import { failure, useErrorNotify } from '../design/ui';
import { DomainLoading } from './DomainLoading';
import type { SettingsDomainProps } from './domains';
import { WiringFolders } from './WiringFolders';
import { WiringHead, WiringOlder } from './WiringHead';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';

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
  const { query, measure } = useSystemWiring();
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
    </>
  );
}
