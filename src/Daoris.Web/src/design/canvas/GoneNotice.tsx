import { useState } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * Whether the selection kept with the viewer's place is gone (the canvas design §4, trap 18): set when a view kept under a graph that
 * has since changed is restored without the item that was selected, and cleared once the person chooses anything again.
 */
export function useSelectionGone(selected: string | null): { gone: boolean; onGone: () => void } {
  const [gone, setGone] = useState(false);
  // A choice made since ends the notice; adjusting this component's own state while rendering is React's documented way.
  if (gone && selected !== null) setGone(false);
  return { gone: gone && selected === null, onGone: () => setGone(true) };
}

/** **The selection-gone notice**: said once under the canvas, never a toast, since the canvas is where the selection was. */
export function GoneNotice() {
  const { t } = useTranslation();
  return <p role="status" className="mt-1 text-small text-ink-soft">{t('canvas.selectionGone')}</p>;
}
