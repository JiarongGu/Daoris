import { render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { type Confirmation, Confirmations } from './Confirmations';
import { helpCardPlaceOf, helpPlaceSaid } from './helpPlaceSaid';

// A restricted Ask Daoris press whose place lies outside its conversation's focus says so on its card (`docs/2026-11-11-ask-scope-ux-
// design.md` §4), from the judgement the receipt and Notifications use; a press within its focus, or naming no place, says nothing.

const work = { kind: 'workspace', workspace: 'Work' } as const;
const games = { kind: 'workspace', workspace: 'Games' } as const;
const api = { kind: 'repository', workspace: 'Work', repository: 'api' } as const;
const engine = { kind: 'repository', workspace: 'Games', repository: 'engine' } as const;

const ARCHIVE: Confirmation = {
  source: 'pipe', id: 'a1', module: 'DAORIS.DRIVER', route: 'SESSION_ARCHIVE',
  fields: [{ name: '/ids/0', type: 'string', value: 's1a2b3c4' }],
  asker: { process: null, pid: 77, session: 'h1', help: true }, asked: new Date(Date.now() - 60_000).toISOString(), waitedOn: true,
};

const draw = (waiting: Confirmation) => render(
  <Confirmations waiting={[waiting]} onAnswer={vi.fn(async () => ({ answered: true as const }))} notify={vi.fn()} names={{ session: () => null }} />,
);

afterEach(async () => {
  await i18n.changeLanguage('en');
});

describe.each([
  ['en', "In Work, outside this conversation's focus (Games)", "In api, outside this conversation's focus (engine)"],
  ['zh', '在 Work 中，在这段对话的聚焦（Games）之外', '在 api 中，在这段对话的聚焦（engine）之外'],
])('the card of an Ask Daoris press beyond its focus (%s)', (language, workspace, repository) => {
  it('names the place and the focus it lies outside, and says nothing within it', async () => {
    await i18n.changeLanguage(language);
    const t = i18n.t.bind(i18n);
    expect(helpPlaceSaid(t, { place: work, beyond: true, focus: games })).toBe(workspace);
    expect(helpPlaceSaid(t, { place: api, beyond: true, focus: engine })).toBe(repository);
    expect(helpPlaceSaid(t, { place: engine, beyond: false, focus: games })).toBeNull();
    expect(helpPlaceSaid(t, null)).toBeNull();
  });

  it('draws its line beside the asker, and none on a card within its focus', async () => {
    await i18n.changeLanguage(language);
    const { container, unmount } = draw({ ...ARCHIVE, helpPlace: { place: work, beyond: true, focus: games } });
    expect(container.querySelector('[data-help-beyond]')?.textContent).toBe(workspace);
    unmount();

    const { container: within_ } = draw({ ...ARCHIVE, helpPlace: { place: engine, beyond: false, focus: games } });
    expect(within_.querySelector('[data-help-beyond]')).toBeNull();
  });
});

describe("the card's place as the shell sends it", () => {
  it('reads a place, whether beyond, and the focus, and leaves out one it cannot read', () => {
    expect(helpCardPlaceOf({ place: { kind: 'workspace', workspace: 'Work' }, beyond: true, focus: { kind: 'workspace', workspace: 'Games' } }))
      .toEqual({ place: work, beyond: true, focus: games });
    expect(helpCardPlaceOf({ place: { kind: 'repository', workspace: 'Games', repository: 'engine' }, beyond: false, focus: null }))
      .toEqual({ place: engine, beyond: false, focus: null });
    expect(helpCardPlaceOf({ place: { kind: 'nowhere' }, beyond: true })).toBeNull();
    expect(helpCardPlaceOf(null)).toBeNull();
    expect(helpCardPlaceOf('x')).toBeNull();
  });
});
