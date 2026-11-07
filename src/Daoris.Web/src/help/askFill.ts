import { useSyncExternalStore } from 'react';

/**
 * Words put in Ask Daoris's box for the person to finish: a page hands them over, the window that holds Ask Daoris
 * opens it with them as a draft, and the person sends them or not. Only a window with a helper registers a door, so a page offers its
 * *Ask why* and *Fix it* only where there is one.
 */
type Fill = (text: string) => void;

let door: Fill | null = null;
const heard = new Set<() => void>();
const tell = () => heard.forEach((each) => each());

/** The window's door, held until the registration that made it lets go; a later registration replaces it. */
export function registerAskFill(fill: Fill): () => void {
  door = fill;
  tell();
  return () => {
    if (door === fill) {
      door = null;
      tell();
    }
  };
}

/** Put `text` in Ask Daoris's box; false where this window has no Ask Daoris to put it in. */
export function fillAskDaoris(text: string): boolean {
  if (!door) return false;
  door(text);
  return true;
}

const subscribe = (listener: () => void) => {
  heard.add(listener);
  return () => { heard.delete(listener); };
};

/** Whether a page can fill Ask Daoris in, as the window comes to have one or loses it. */
export const useCanFillAsk = (): boolean => useSyncExternalStore(subscribe, () => door !== null);
