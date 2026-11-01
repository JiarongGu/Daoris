import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { PlacePageView, type PlaceDriving, type PlaceHeadProps, type PlaceSectionSpec } from './PlacePage';
import type { PlaceSection } from './placeShell';

// SETUPUX1h (the setup-UX design §2.1): a place page is a head and folded sections; a section's state is its holder's, a folded one mounts
// nothing, a door opens its row, and *Back* returns once.

const mounts = vi.fn();

function Body({ name }: { name: string }) {
  mounts(name);
  return <p>{name} body</p>;
}

const SECTIONS: PlaceSectionSpec[] = (['workflow', 'roles', 'sessions'] as PlaceSection[]).map((id) => ({
  id,
  title: id === 'workflow' ? 'Workflow' : id === 'roles' ? 'Roles' : 'Sessions',
  summary: `${id} summary (from aurora)`,
  open: id === 'workflow',
  onOpenChange: vi.fn(),
  body: <Body name={id} />,
}));

const HEAD: PlaceHeadProps = {
  title: 'engine',
  pill: { tone: 'done', label: 'Driven' },
  facts: ['Merge after you accept, 2 options of its own', 'its own tree per session'],
  parent: { name: 'aurora', onOpen: vi.fn() },
  setUp: { onOpen: vi.fn() },
};

const DRIVING = (patch: Partial<PlaceDriving> = {}): PlaceDriving => ({
  drivable: true, held: false, ownTree: true, busy: false, terminal: 'daoris driver drive|undrive engine',
  onDrive: vi.fn(), onHold: vi.fn(), onTrees: vi.fn(), ...patch,
});

const draw = (props: Partial<Parameters<typeof PlacePageView>[0]> = {}) =>
  render(
    <Tooltip.Provider>
      <PlacePageView head={HEAD} sections={SECTIONS} focusAsked={0} {...props} />
    </Tooltip.Provider>,
  );

afterEach(async () => {
  mounts.mockReset();
  await i18n.changeLanguage('en');
});

describe('the place page', () => {
  it('draws the head, its pill and facts, and three sections folded to their lines', () => {
    draw();
    expect(screen.getByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
    expect(screen.getByText('Driven')).toBeInTheDocument();
    expect(screen.getByText(/Merge after you accept, 2 options of its own/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Set up…' })).toBeInTheDocument();
    for (const name of ['Workflow', 'Roles', 'Sessions']) expect(screen.getByRole('button', { name })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAccessibleDescription('roles summary (from aurora)');
  });

  // SETUPUX1o: from 64 rem the sections stand beside a column the open Workflow's chart is drawn into; below it there is none.
  it('keeps a column for the open Workflow\'s chart from 64 rem, and none below', () => {
    const measure = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      const width = this.hasAttribute('data-place-page') ? 1232 : 0;
      return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
    });
    const wide = draw();
    expect(document.querySelector('[data-place-columns]')).toHaveAttribute('data-place-columns', 'two');
    expect(document.querySelector('[data-place-aside]')).not.toBeNull();
    wide.unmount();
    measure.mockRestore();

    draw();
    expect(document.querySelector('[data-place-columns]')).toHaveAttribute('data-place-columns', 'one');
    expect(document.querySelector('[data-place-aside]')).toBeNull();
  });

  it('mounts the open section alone: a folded section reads nothing', () => {
    draw();
    expect(mounts.mock.calls.map(([name]) => name)).toEqual(['workflow']);
    expect(screen.queryByText('roles body')).toBeNull();
    expect(screen.queryByText('sessions body')).toBeNull();
  });

  it('leaves a section\'s state to its holder', async () => {
    draw();
    await userEvent.click(screen.getByRole('button', { name: 'Roles' }));
    expect(SECTIONS[1]!.onOpenChange).toHaveBeenCalledWith(true);
    // The holder has not opened it, so it stays folded.
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('reaches every press by the keyboard: a section toggles on Enter and on Space, in order after the head\'s acts', async () => {
    draw({ driving: DRIVING() });
    await userEvent.tab();
    const order: string[] = [];
    for (let at = 0; at < 8; at += 1) {
      order.push(document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.textContent?.trim().slice(0, 14) ?? '');
      await userEvent.tab();
    }
    // The head's acts and menu come first, then the three sections in order.
    expect(order.slice(0, 7).map((each) => each.replace(/^(Roles|Sessions).*/, '$1')))
      .toEqual(['aurora ›', 'Set up…', 'Hold', 'More actions', 'Workflow', 'Roles', 'Sessions']);
    screen.getByRole('button', { name: 'Roles' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.keyboard(' ');
    expect(SECTIONS[1]!.onOpenChange).toHaveBeenCalledTimes(2);
  });

  it('opens the parent\'s place through the head\'s fact door', async () => {
    draw();
    await userEvent.click(screen.getByRole('button', { name: /aurora/ }));
    expect(HEAD.parent!.onOpen).toHaveBeenCalledOnce();
  });

  it('offers Back once, naming the page it came from and its section', async () => {
    const onBack = vi.fn();
    draw({ back: { place: 'engine', section: 'Roles', onBack } });
    await userEvent.click(screen.getByRole('button', { name: '← Back to engine · Roles' }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('brings the row a door asked for into view, once its section has drawn it', async () => {
    const brought = vi.fn();
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    const Late = () => {
      const [shown, setShown] = useState(false);
      return <button onClick={() => setShown(true)}>read{shown && <span data-place-row="review" tabIndex={-1}>review row</span>}</button>;
    };
    draw({ row: 'review', onRowBrought: brought, sections: [{ ...SECTIONS[0]!, body: <Late /> }, ...SECTIONS.slice(1)] });
    expect(brought).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: /read/ }));
    await waitFor(() => expect(brought).toHaveBeenCalledOnce());
    expect(focus.mock.contexts.some((each) => (each as HTMLElement).dataset?.placeRow === 'review')).toBe(true);
    focus.mockRestore();
  });

  it('takes the focus to the head once asked and nothing holds it', async () => {
    const { rerender } = draw();
    expect(document.activeElement).toBe(document.body);
    rerender(
      <Tooltip.Provider>
        <PlacePageView head={HEAD} sections={SECTIONS} focusAsked={1} />
      </Tooltip.Provider>,
    );
    await waitFor(() => expect(document.activeElement).toBe(document.querySelector('[data-place-head]')));
  });

  it('leaves the focus where it is when something holds it', async () => {
    const { rerender } = draw();
    screen.getByRole('button', { name: 'Roles' }).focus();
    await act(async () => {
      rerender(
        <Tooltip.Provider>
          <PlacePageView head={HEAD} sections={SECTIONS} focusAsked={1} />
        </Tooltip.Provider>,
      );
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    });
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Roles' }));
  });
});

/** A menu opens on the key, as the other pages' tests open theirs. */
async function openMore() {
  screen.getByRole('button', { name: 'More actions' }).focus();
  await userEvent.keyboard('{Enter}');
}

describe('the head\'s driving acts', () => {
  it('holds and resumes with one press, and sets nothing else', async () => {
    const driving = DRIVING();
    draw({ driving });
    await userEvent.click(screen.getByRole('button', { name: 'Hold' }));
    expect(driving.onHold).toHaveBeenCalledWith(true);
    expect(driving.onDrive).not.toHaveBeenCalled();
  });

  it('says Resume on a held place, and offers no Hold on one not driven', () => {
    const { unmount } = draw({ driving: DRIVING({ held: true }) });
    expect(screen.getByRole('button', { name: 'Resume' })).toBeInTheDocument();
    unmount();
    draw({ driving: DRIVING({ drivable: false, held: false }) });
    expect(screen.queryByRole('button', { name: 'Hold' })).toBeNull();
  });

  it('confirms stopping in place before it sets driving', async () => {
    const driving = DRIVING();
    draw({ driving });
    await openMore();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Stop driving…' }));
    expect(driving.onDrive).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'stop driving engine' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Stop driving' }));
    expect(driving.onDrive).toHaveBeenCalledWith(false, expect.objectContaining({ done: expect.any(Function) }));
  });

  it('turns a tree per session on or off from the menu, and keeps the head\'s note', async () => {
    const driving = DRIVING({ note: 'This machine drives on a direct agent.', stewardSet: 'Set by the steward at 10:00' });
    draw({ driving });
    expect(screen.getByText('This machine drives on a direct agent.')).toBeInTheDocument();
    expect(screen.getByText('Set by the steward at 10:00')).toBeInTheDocument();
    await openMore();
    await userEvent.click(await screen.findByRole('menuitemcheckbox', { name: 'A tree per session' }));
    expect(driving.onTrees).toHaveBeenCalledWith(false);
  });
});
