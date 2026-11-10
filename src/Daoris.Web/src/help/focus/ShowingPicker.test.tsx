import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { PlaceRepository } from './focusPlaces';
import { ShowingPicker } from './ShowingPicker';

// The history's *Showing* picker (`docs/2026-11-11-ask-scope-ux-design.md` §3): *This workspace* marked as the window's, and the
// other workspaces unfolding onto their repositories as the focus picker's do, in both catalogues.

const REPOSITORIES: PlaceRepository[] = [
  { name: 'engine', workspace: 'Games', checkout: true },
  { name: 'site', workspace: 'Work', checkout: true },
  { name: 'api', workspace: 'Work', checkout: true },
];

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const draw = () => {
  const onChoose = vi.fn();
  render(<ShowingPicker showing={{ kind: 'here' }} window="Games" repositories={REPOSITORIES} recent={[]} onChoose={onChoose} />);
  return onChoose;
};

const text = (row: Element) => (row.textContent ?? '').replace(/\s+/g, ' ').trim();
const rows = () => within(screen.getByRole('listbox')).getAllByRole('option').map(text);
const lit = () => text(screen.getAllByRole('option').find((row) => row.getAttribute('aria-selected') === 'true')!);

describe.each(['en', 'zh'])('the Showing picker in %s', (language) => {
  it('marks This workspace as this window’s', async () => {
    await i18n.changeLanguage(language);
    draw();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.showingIs', { value: i18n.t('help.history.showingHere', { workspace: 'Games' }) }) }));
    expect(rows()[0]).toBe(`${i18n.t('help.history.showingHere', { workspace: 'Games' })} ${i18n.t('help.focus.thisWindow')}`);
  });

  it('unfolds a workspace onto its repositories at the caret’s end, folds it at its start, and shows about the one chosen', async () => {
    await i18n.changeLanguage(language);
    const onChoose = draw();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.showingIs', { value: i18n.t('help.history.showingHere', { workspace: 'Games' }) }) }));
    expect(rows()).not.toContain('api');

    await userEvent.keyboard('{ArrowUp}');
    expect(lit()).toBe(`Work ${i18n.t('help.focus.count', { count: 2 })}`);
    await userEvent.keyboard('{ArrowRight}');
    expect(rows()).toEqual(expect.arrayContaining(['api', 'site']));
    await userEvent.keyboard('{ArrowLeft}');
    await userEvent.keyboard('{ArrowRight}{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenCalledWith({ kind: 'about', place: { kind: 'repository', workspace: 'Work', repository: 'api' } });
  });
});
