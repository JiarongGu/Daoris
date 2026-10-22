import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FRAME_HELD } from '../../design/ui';
import { cn } from '../../lib/cn';

/** How long a level stays said once it changes: long enough to read, gone before it is in the way. */
export const READOUT_LASTS = 1500;

/**
 * The zoom's readout (ZOOM1, the UI/UX design §3.11): the level said as it changes, over the top of the content just under
 * the title strip, as a browser's zoom bubble says it, and gone after a moment; the status bar keeps saying it while it is
 * not 100%. Said at 100% too, so going back is heard.
 *
 * @remarks
 * It keeps the frame's size (`frame-held`), so the number never grows with what it measures. A status, read once by a
 * reader as it changes, never a pointer target: it sits over whatever is under it.
 */
export function ZoomReadout({ level, lasts = READOUT_LASTS }: {
  /** The level in force, in percent; a change is what it says. */
  level: number;
  lasts?: number;
}) {
  const { t } = useTranslation();
  const [said, setSaid] = useState<number | null>(null);
  const was = useRef(level);

  useEffect(() => {
    if (was.current === level) return undefined;
    was.current = level;
    setSaid(level);
    // Each change keeps it up for its own moment: a step taken while it shows restarts the clock.
    const gone = window.setTimeout(() => setSaid(null), lasts);
    return () => window.clearTimeout(gone);
  }, [level, lasts]);

  if (said === null) return null;
  return (
    <div
      role="status"
      className={cn(
        'pointer-events-none fixed left-1/2 top-12 z-40 -translate-x-1/2 rounded-card border border-line bg-overlay px-3 py-1.5',
        'text-title tabular-nums text-ink shadow-lg',
        FRAME_HELD,
      )}
    >
      <span className="sr-only">{t('work.status.zoom')} </span>
      {t('work.status.zoomLevel', { percent: said })}
    </div>
  );
}
