import { describe, expect, it } from 'vitest';
import { standingOf } from './person';

// The words a rule refused a press in, as STATE's `standingConfirmation.helpPresses` carries them beside the press: read field by field, so a press a
// rule refused (the growth switch among them) keeps them and one that ran has none.

const press = (more: Record<string, unknown>) => ({ id: 'a', module: 'DAORIS.DRIVER', route: 'REGISTRY_CREATE', session: 's1', at: '2026-11-12T10:00:00Z', outcome: 'refused', ...more });
const whyOf = (more: Record<string, unknown>) => standingOf({ helpPresses: [press(more)] })?.helpPresses[0];

describe("a refused press's words over the bridge", () => {
  it('are kept with the press, and left off where there are none or they are no words', () => {
    expect(whyOf({ why: 'Opening rooms in `engine` is off (the Settings of `engine`, row New repositories and rooms).' })?.why)
      .toBe('Opening rooms in `engine` is off (the Settings of `engine`, row New repositories and rooms).');
    for (const why of [undefined, null, '', 3]) expect(whyOf({ why })).not.toHaveProperty('why');
  });
});
