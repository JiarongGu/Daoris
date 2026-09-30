import { describe, expect, it } from 'vitest';
import { DEFAULT_PLACES, placesOf, viewsIn } from './placements';

// DOCK1b: a view is a thing that can move, a region is a place it can go, and where each stands is the
// viewer's, remembered like every other layout choice.

describe('where the views stand', () => {
  it('starts every view where the frame always drew it', () => {
    expect(placesOf(null)).toEqual(DEFAULT_PLACES);
    // The terminal (CONSOLE4b) stands in the panel, beside the console, as VS Code's does.
    expect(DEFAULT_PLACES).toEqual({ timeline: 'right', review: 'right', ask: 'right', console: 'panel', terminal: 'panel' });
  });

  it('reads a terminal the viewer moved, and holds it after the console in the panel', () => {
    expect(placesOf(JSON.stringify({ terminal: 'right' }))).toEqual({ ...DEFAULT_PLACES, terminal: 'right' });
    expect(viewsIn(DEFAULT_PLACES, 'panel')).toEqual(['console', 'terminal']);
    expect(viewsIn({ ...DEFAULT_PLACES, terminal: 'right' }, 'right')).toEqual(['timeline', 'review', 'ask', 'terminal']);
  });

  it('reads what the viewer moved, and keeps the default for everything else', () => {
    expect(placesOf(JSON.stringify({ ask: 'panel' }))).toEqual({ ...DEFAULT_PLACES, ask: 'panel' });
  });

  it('ignores what it cannot read rather than refusing the frame', () => {
    expect(placesOf('not json')).toEqual(DEFAULT_PLACES);
    expect(placesOf(JSON.stringify(['panel']))).toEqual(DEFAULT_PLACES);
    // A view this build does not know, and a place it does not have, are both dropped.
    expect(placesOf(JSON.stringify({ outline: 'panel', console: 'left', review: 'panel' })))
      .toEqual({ ...DEFAULT_PLACES, review: 'panel' });
  });

  it('lists a region\'s own views first, then what was moved into it, each in the frame\'s order', () => {
    const places = { ...DEFAULT_PLACES, console: 'right' as const, ask: 'panel' as const };
    expect(viewsIn(places, 'right')).toEqual(['timeline', 'review', 'console']);
    expect(viewsIn(places, 'panel')).toEqual(['terminal', 'ask']);
  });

  it('leaves out a view that is not here, wherever it stands', () => {
    expect(viewsIn(DEFAULT_PLACES, 'right', (view) => view !== 'ask')).toEqual(['timeline', 'review']);
    // No shell, no terminal: the panel holds the console alone.
    expect(viewsIn(DEFAULT_PLACES, 'panel', (view) => view !== 'terminal')).toEqual(['console']);
  });
});
