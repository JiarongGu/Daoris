import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { driverKey } from './shell';

type Tick = { events?: string[] };

/**
 * The shell's push channel into the page (D46 §6): the driver's tick reports arrive as toasts, and
 * everything a tick can change refetches — in the desktop the session surface is live, while a plain
 * browser keeps its polling and never mounts a transport. Renders nothing; costs nothing where no
 * host answers.
 */
export function ShellSignals({ notify }: { notify: (text: string, kind?: 'ok' | 'error') => void }) {
  const { isAvailable, bridge } = useShenora();
  const client = useQueryClient();

  useEffect(() => {
    if (!isAvailable) return;
    // The kit buffers host events until this handshake — without it, no notification ever arrives.
    bridge.notifyReady().catch(() => {
      // A failed handshake is the host log's problem; the page stays a working page.
    });
  }, [isAvailable, bridge]);

  useShenoraEvent<Tick>('DAORIS', 'DRIVER_TICK', (tick) => {
    // The driver's own sentences, verbatim — like every system sentence in this UI.
    for (const line of tick?.events ?? []) notify(line);
    void client.invalidateQueries({ queryKey: ['sessions'] });
    void client.invalidateQueries({ queryKey: ['quests'] });
    void client.invalidateQueries({ queryKey: driverKey });
  });

  useShenoraEvent<{ message?: string }>('DAORIS', 'DRIVER_ERROR', (error) => {
    notify(error?.message ?? 'driver error', 'error');
  });

  return null;
}
