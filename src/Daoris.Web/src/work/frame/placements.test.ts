import { describe, expect, it } from 'vitest';
import { DEFAULT_PLACES, placesOf, viewsIn } from './placements';

// A view is a thing that can move, a region is a place it can go, and where each stands is the
// viewer's, remembered like every other layout choice.

describe('where the views stand', () => {
  it('starts every view where the frame always drew it', () => {
    expect(placesOf(null)).toEqual(DEFAULT_PLACES);
    // The terminal stands in the panel, beside the console, as VS Code's does; *Notifications* on the right, first; no *At work*
    // among them, who the line worked with being rows in its timeline.
    expect(DEFAULT_PLACES).toEqual({
      notifications: 'right', timeline: 'right', outputs: 'right', ask: 'right', console: 'panel', terminal: 'panel',
    });
  });

  it('reads a terminal the viewer moved, and holds it after the console in the panel', () => {
    expect(placesOf(JSON.stringify({ terminal: 'right' }))).toEqual({ ...DEFAULT_PLACES, terminal: 'right' });
    expect(viewsIn(DEFAULT_PLACES, 'panel')).toEqual(['console', 'terminal']);
    expect(viewsIn({ ...DEFAULT_PLACES, terminal: 'right' }, 'right'))
      .toEqual(['notifications', 'timeline', 'outputs', 'ask', 'terminal']);
  });

  it('drops the At work view an earlier build kept, who the line worked with now read in its timeline’s rows', () => {
    expect(placesOf(JSON.stringify({ atWork: 'panel' }))).toEqual(DEFAULT_PLACES);
  });

  it('drops the Workflow view an earlier build kept, its run now read in the session head’s track', () => {
    expect(placesOf(JSON.stringify({ workflow: 'panel' }))).toEqual(DEFAULT_PLACES);
  });

  it('reads what the viewer moved, and keeps the default for everything else', () => {
    expect(placesOf(JSON.stringify({ ask: 'panel' }))).toEqual({ ...DEFAULT_PLACES, ask: 'panel' });
    // *Notifications* moves as every view does, and is kept where the person put it.
    expect(placesOf(JSON.stringify({ notifications: 'panel' }))).toEqual({ ...DEFAULT_PLACES, notifications: 'panel' });
    expect(viewsIn({ ...DEFAULT_PLACES, notifications: 'panel' }, 'panel')).toEqual(['console', 'terminal', 'notifications']);
  });

  it('ignores what it cannot read rather than refusing the frame', () => {
    expect(placesOf('not json')).toEqual(DEFAULT_PLACES);
    expect(placesOf(JSON.stringify(['panel']))).toEqual(DEFAULT_PLACES);
    // A view this build does not know, and a place it does not have, are both dropped.
    expect(placesOf(JSON.stringify({ outline: 'panel', console: 'left', outputs: 'panel' })))
      .toEqual({ ...DEFAULT_PLACES, outputs: 'panel' });
  });

  it('lists a region\'s own views first, then what was moved into it, each in the frame\'s order', () => {
    const places = { ...DEFAULT_PLACES, console: 'right' as const, ask: 'panel' as const };
    expect(viewsIn(places, 'right')).toEqual(['notifications', 'timeline', 'outputs', 'console']);
    expect(viewsIn(places, 'panel')).toEqual(['terminal', 'ask']);
  });

  it('leaves out a view that is not here, wherever it stands', () => {
    expect(viewsIn(DEFAULT_PLACES, 'right', (view) => view !== 'ask' && view !== 'notifications')).toEqual(['timeline', 'outputs']);
    // No shell, no terminal: the panel holds the console alone.
    expect(viewsIn(DEFAULT_PLACES, 'panel', (view) => view !== 'terminal')).toEqual(['console']);
  });
});
