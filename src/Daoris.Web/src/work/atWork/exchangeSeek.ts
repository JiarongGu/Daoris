import { type RefObject, useEffect, useRef, useState } from 'react';

/** A press that opens *At work* at one request: the exchange's id, counted so the same one pressed again is sought again. */
export type ExchangeSeek = { exchange: string; n: number };

/**
 * The head's *Waiting on* door (it opens *At work* at the exchange): the request sought, and the press that
 * seeks one and opens the view.
 */
export function useAtWorkSeek(openView: (view: 'atWork') => void) {
  const [seek, setSeek] = useState<ExchangeSeek | null>(null);
  const open = (exchange: string) => {
    setSeek((was) => ({ exchange, n: (was?.n ?? 0) + 1 }));
    openView('atWork');
  };
  return { seek, open };
}

/** The row of `exchange` in *At work*, else its *Waiting on* line, where the request has no row of its own on screen. */
function rowOf(box: HTMLElement | null, exchange: string): HTMLElement | null {
  if (!box) return null;
  const rows = Array.from(box.querySelectorAll<HTMLElement>('[data-exchange]'));
  return rows.find((row) => row.dataset.exchange === exchange) ?? box.querySelector<HTMLElement>('[data-wait-line]');
}

/**
 * Which request *At work* outlines, and the focus put on its row once drawn, as a wait sought at the foot takes it (`useSought`):
 * the view may be drawn after the press, so the focus is owed until the row is there.
 */
export function useSoughtExchange(box: RefObject<HTMLElement | null>, seek: ExchangeSeek | null | undefined) {
  const [sought, setSought] = useState<string | null>(null);
  const focusing = useRef(false);
  useEffect(() => {
    if (!seek) return;
    setSought(seek.exchange);
    focusing.current = true;
  }, [seek?.n]);
  useEffect(() => {
    if (!focusing.current || !seek) return;
    const drawn = rowOf(box.current, seek.exchange);
    if (!drawn) return;
    drawn.focus();
    if (drawn.ownerDocument.activeElement === drawn) focusing.current = false;
  });
  return { sought, letGo: () => setSought(null) };
}
