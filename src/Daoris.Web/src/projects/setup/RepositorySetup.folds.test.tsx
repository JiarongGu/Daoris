import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { NO_VERIFICATION, START } from '../../workflow/fixtures';
import { SETUP_DEFAULTS } from '../fixtures';
import { FOLD_SECTIONS, RepositorySetup, type RepositorySetupProps } from './RepositorySetup';

// SETUPUX1o (D221; the Setup design's §2.3): the place's Workflow draws the rows a repository's Setup holds as one row of folds, each with its
// owner: Landing, Second opinion, Your look, Landed route, The line and More (commits, and the heavy gates moved from Sessions). The names
// are one wrapped row so the head and the three section lines keep the first screen; each says its values to a reader. Props only.

const element = (props: RepositorySetupProps) => (
  <Tooltip.Provider><RepositorySetup {...props} only={FOLD_SECTIONS} gatesApart /></Tooltip.Provider>
);
const draw = (props: RepositorySetupProps) => render(element(props));
const head = (name: string) => screen.getByRole('button', { name });

const WITH_EVERYTHING: RepositorySetupProps = {
  ...SETUP_DEFAULTS,
  work: {
    ...SETUP_DEFAULTS.work,
    commits: { repository: 'engine', workspace: 'default', rule: { mode: 'as-you-go' }, source: 'default' },
    onCommits: vi.fn(),
    review: { repository: 'engine', workspace: 'default', rule: null, source: null },
    onReview: vi.fn(),
    opinion: { repository: 'engine', workspace: 'default', rule: null, source: null },
    onOpinion: vi.fn(),
  },
  lands: { is: 'version', summary: 'merge into the line, as Release v2 says', editor: <p>the land options</p> },
  landed: { start: NO_VERIFICATION, workspace: 'aurora', onChoose: vi.fn() },
  heavy: { gates: [{ name: 'build', run: 'dotnet build' }], onSave: vi.fn() },
};

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's Workflow folds", () => {
  it('draws the landing, the second opinion, your look, the landed route, the line and more, in that order, each folded and saying its values', () => {
    draw(WITH_EVERYTHING);

    const names = ['Landing', 'Second opinion', 'Your look', 'Landed route', 'The line', 'More'];
    const heads = names.map(head);
    heads.forEach((each, at) => {
      expect(each).toHaveAttribute('aria-expanded', 'false');
      if (at > 0) expect(heads[at - 1]!.compareDocumentPosition(each) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });
    expect(head('Landing')).toHaveAccessibleDescription('merge into the line, as Release v2 says');
    expect(head('Second opinion')).toHaveAccessibleDescription("no second opinion (Daoris's default)");
    expect(head('Your look')).toHaveAccessibleDescription("no review environment (Daoris's default)");
    expect(head('Landed route')).toHaveAccessibleDescription("no verification (Daoris's default)");
    expect(head('The line')).toHaveAccessibleDescription("line main (the workspace's default)");
    expect(head('More')).toHaveAccessibleDescription("commits as it goes (Daoris's default) · 1 heavy gate: build");
  });

  it("draws none of the whole Setup's sections, and keeps the landed route's choices where they were", async () => {
    const choose = vi.fn();
    draw({ ...WITH_EVERYTHING, landed: { start: START, workspace: 'aurora', onChoose: choose } });

    expect(screen.queryByRole('button', { name: 'Line and landing' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Heavy gates' })).toBeNull();
    await userEvent.click(head('Landed route'));
    const choices = screen.getByRole('group', { name: 'What verifies the work that lands on engine' });
    expect(within(choices).getByRole('button', { name: 'Verify the line' })).toBeInTheDocument();
    expect(within(choices).getByRole('button', { name: 'No verification here' })).toBeInTheDocument();
    await userEvent.click(within(choices).getByRole('button', { name: 'No verification here' }));
    expect(choose).toHaveBeenCalledWith('current', expect.anything());
  });

  it('keeps the heavy gates in More with the commits, edited whole as they were', async () => {
    const onSave = vi.fn();
    draw({ ...WITH_EVERYTHING, heavy: { gates: [{ name: 'build', run: 'dotnet build' }, { name: 'unit', run: 'dotnet test' }], onSave } });

    await userEvent.click(head('More'));
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move gate 2 up' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenCalledWith([{ name: 'unit', run: 'dotnet test' }, { name: 'build', run: 'dotnet build' }], expect.anything());
  });

  it('opens Landing where a door asked for it, and no fold else', () => {
    const { unmount } = draw(WITH_EVERYTHING);
    expect(head('Landing')).toHaveAttribute('aria-expanded', 'false');
    expect(head('The line')).toHaveAttribute('aria-expanded', 'false');
    unmount();

    draw({ ...WITH_EVERYTHING, open: 'landing' });
    expect(head('Landing')).toHaveAttribute('aria-expanded', 'true');
  });

  it('opens again for a door that asks twice, after the person folded it', async () => {
    const props = { ...WITH_EVERYTHING, open: 'landing' as const, openAgain: 1 };
    const { rerender } = draw(props);
    await userEvent.click(head('Landing'));
    expect(head('Landing')).toHaveAttribute('aria-expanded', 'false');

    rerender(element({ ...props, openAgain: 2 }));
    expect(head('Landing')).toHaveAttribute('aria-expanded', 'true');
  });

  it('stands the folds it is given first, as the page puts Gates before the others, opened by its own need', () => {
    draw({ ...WITH_EVERYTHING, lead: [{ id: 'gates', title: 'Gates', summary: 'Waits for your acceptance', body: <p>the declaration</p>, opens: true }] });

    const heads = screen.getAllByRole('button').map((each) => each.textContent);
    expect(heads.slice(0, 2)).toEqual(['Gates', 'Landing']);
    expect(head('Gates')).toHaveAttribute('aria-expanded', 'true');
    expect(within(screen.getByRole('region', { name: 'Gates' })).getByText('the declaration')).toBeInTheDocument();
  });

  it('marks each fold as a row of the place a door can bring into view', () => {
    draw(WITH_EVERYTHING);
    expect(document.querySelector('[data-place-row="landing"]')).toBe(head('Landing'));
  });

  it('names its folds in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(WITH_EVERYTHING);
    for (const name of ['落地', '第二意见', '你的查看', '落地路线', '主线', '更多']) expect(head(name)).toBeInTheDocument();
  });
});
