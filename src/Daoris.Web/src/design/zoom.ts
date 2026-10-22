import { useSyncExternalStore } from 'react';
import { store, stored } from '../lib/stored';

// The window's zoom (ZOOM1, the UI/UX design §3.11): the content larger or smaller in an editor's steps, the frame kept at its
// own size. Kept for the machine as the theme is (`theme.ts`, the exemplar), in the window's own storage: the engine's
// profile is this machine's, so a level chosen here is this machine's and no other's.
//
// **Applied to the page's root size**, which every rem the page draws in follows: the type, the spacing, the icons, and what
// opens over the window (a drawer, a menu, the palette, a toast), each positioned on real geometry, so a menu still opens
// at its trigger. The frame (the title strip, the activity bar, the status bar, and what they open) wears `frame-held`,
// whose CSS zoom is this level's inverse, `--frame-zoom`, and so keeps its size at every level. The engine's own zoom is
// never used: it scaled the frame with the content (the owner's report, 2026-10-22), and the wheel that drove it is taken
// here (`zoomWheel`).

/** The levels, in percent: a browser's and an editor's familiar steps, bounded at half and double the page as drawn. */
export const ZOOM_LEVELS = [50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200] as const;

/** The page as drawn. */
export const ZOOM_DEFAULT = 100;

/** Where the level is remembered: per machine, as the theme's choice is (`THEME_KEY`). */
export const ZOOM_KEY = 'daoris.zoom';

const isLevel = (level: number) => (ZOOM_LEVELS as readonly number[]).includes(level);

/** The remembered level, or 100% where there is none or it is one this build does not step to. */
export function readZoom(): number {
  const held = Number(stored(ZOOM_KEY));
  return isLevel(held) ? held : ZOOM_DEFAULT;
}

let current: number = typeof window === 'undefined' ? ZOOM_DEFAULT : readZoom();
const listeners = new Set<() => void>();

/** The level in force. */
export function zoomLevel(): number {
  return current;
}

/** The next step from `level` in `direction`, held at the ends: zooming in at 200% stays at 200%. */
export function zoomStep(level: number, direction: 1 | -1): number {
  if (direction > 0) return ZOOM_LEVELS.find((step) => step > level) ?? ZOOM_LEVELS[ZOOM_LEVELS.length - 1]!;
  return [...ZOOM_LEVELS].reverse().find((step) => step < level) ?? ZOOM_LEVELS[0];
}

/**
 * Put a level on the document: the root's size, and the frame's inverse. 100% removes both, so the page is exactly the one
 * drawn before there was a zoom.
 */
export function applyZoom(level: number = current): void {
  const root = document.documentElement;
  if (level === ZOOM_DEFAULT) {
    root.style.removeProperty('font-size');
    root.style.removeProperty('--frame-zoom');
    return;
  }
  root.style.fontSize = `${level}%`;
  root.style.setProperty('--frame-zoom', String(ZOOM_DEFAULT / level));
}

function tell(): void {
  for (const listener of listeners) listener();
}

/** Choose a level, remember it, apply it, and tell whoever listens. A level that is not a step is refused. */
export function setZoom(level: number): void {
  if (!isLevel(level) || level === current) return;
  current = level;
  // Not remembered where storage is refused; still applied for as long as this page lives.
  store(ZOOM_KEY, level === ZOOM_DEFAULT ? null : String(level));
  applyZoom(level);
  tell();
}

/** View's three zoom acts (ZOOM1): a step in, a step out, or back to the page as drawn. */
export function zoomBy(step: 'in' | 'out' | 'reset'): void {
  setZoom(step === 'reset' ? ZOOM_DEFAULT : zoomStep(current, step === 'in' ? 1 : -1));
}

/**
 * A level another main window chose (ONEINSTANCE1): the store tells every other document of a write, so this one follows
 * as the theme does (WINDOW1), applied and told without writing it back.
 */
export function followStoredZoom(event: StorageEvent): void {
  if (event.key !== ZOOM_KEY && event.key !== null) return;
  const next = readZoom();
  if (next === current) return;
  current = next;
  applyZoom(next);
  tell();
}

if (typeof window !== 'undefined') window.addEventListener('storage', followStoredZoom);

/** Hear every change of level. Returns the way to stop hearing. */
export function subscribeZoom(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** The level, as a component reads it. */
export function useZoom(): number {
  return useSyncExternalStore(subscribeZoom, zoomLevel, zoomLevel);
}

/** How far a wheel travels, in pixels, before it is one step: a mouse's notch is 100, a touchpad's pinch a few at a time. */
const WHEEL_STEP = 50;

/** A wheel's travel in pixels, whatever it counts in: lines at a line's height, pages at a window's. */
const travelled = (event: WheelEvent) => (event.deltaMode === 1 ? event.deltaY * 33 : event.deltaMode === 2 ? event.deltaY * 800 : event.deltaY);

/**
 * The wheel with Ctrl, as an editor reads it: a step a notch, a touchpad's small travel gathered into one, towards the reader
 * (in) as the wheel turns away. Its default is prevented, so the engine's own zoom never runs beside this one.
 *
 * @remarks
 * Heard on the window, after every surface under the pointer: **a surface that zooms itself takes the wheel first** (the Map,
 * `LayeredMap`'s own listener prevents it), and a wheel already taken is left alone, so the Map keeps its Ctrl+wheel. A plain
 * wheel scrolls, untouched.
 */
export function zoomWheel(onStep: (direction: 1 | -1) => void): (event: WheelEvent) => void {
  let gathered = 0;
  return (event) => {
    if (!(event.ctrlKey || event.metaKey) || event.defaultPrevented) return;
    event.preventDefault();
    const delta = travelled(event);
    if (delta === 0) return;
    if (Math.sign(delta) !== Math.sign(gathered)) gathered = 0;
    gathered += delta;
    if (Math.abs(gathered) < WHEEL_STEP) return;
    gathered = 0;
    onStep(delta < 0 ? 1 : -1);
  };
}
