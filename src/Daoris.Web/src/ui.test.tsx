import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { SessionState } from './api';
import './i18n';
import * as Tooltip from '@radix-ui/react-tooltip';
import {
  Button, CountBadge, Dot, Drawer, EmptyState, Inline, Menu, MetaLine, MonoWell, PathText, Pill, QuickPanel, Segmented,
  SelectField, SESSION_ACTIVE, SESSION_DOT, SESSION_TONE, SettingRow, shownState, StripMark, Tile, Tip, WaitingCard,
} from './ui';
import { CommandPalette } from './work/CommandPalette';

describe('the primitives', () => {
  it('a pill always carries its text label — status never rides on hue alone', () => {
    render(<Pill tone="declined" title="turned down">Declined</Pill>);
    const pill = screen.getByText('Declined');
    expect(pill).toHaveAttribute('title', 'turned down');
    expect(pill.className).toContain('st-declined');
  });

  it('a warned tile wears the warning on its note, never on its value', () => {
    render(<Tile label="Open quests" value={3} note="oldest has sat 12d" warn />);
    expect(screen.getByText('oldest has sat 12d').className).toContain('st-open');
    expect(screen.getByText('3').className).not.toContain('st-open');
  });

  it('an empty state offers the action that would change the fact', () => {
    render(
      <EmptyState
        icon="inbox"
        headline="No open quests anywhere"
        body="The family owes itself nothing right now."
        action={<Button>Ask for something</Button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'Ask for something' })).toBeInTheDocument();
    // Its lines are set to leave no word alone on the last one (seen on Search's, UX5 U41).
    expect(screen.getByText('The family owes itself nothing right now.').closest('p')).toHaveClass('text-pretty');
  });

  /**
   * 🔴 A button submits only when it says so. HTML's default inside a form is `submit`, and two
   * untyped buttons were: the composer's send, which sent each message twice, and a key form's
   * cancel, which saved the key (2026-09-25).
   */
  it('a button is a button unless it says it submits', () => {
    render(<form><Button>cancel</Button><Button type="submit">save</Button></form>);
    expect(screen.getByRole('button', { name: 'cancel' })).toHaveAttribute('type', 'button');
    expect(screen.getByRole('button', { name: 'save' })).toHaveAttribute('type', 'submit');
  });

  it('a disabled primary stays announced as disabled', () => {
    render(<Button variant="primary" disabled>Publish quest</Button>);
    expect(screen.getByRole('button', { name: 'Publish quest' })).toBeDisabled();
  });
});

/**
 * A setting is a row (2026-09-23): label, one-line hint, the control at the right — and the why on
 * the glyph, for the reader who asks, rather than as the paragraph that used to sit above every
 * control on the Machine view.
 */
describe('a select', () => {
  // SELECT1: a list longer than the window ran off its edge, with nothing to scroll it by. The list is capped at
  // the room the popper measures on the side it opened, and it scrolls inside that.
  it('caps its open list at the room the window has, and scrolls inside it', async () => {
    const user = userEvent.setup();
    const options = Array.from({ length: 60 }, (_, i) => ({ value: `repo-${i}`, label: `repository ${i}` }));
    render(<SelectField value="" onChange={() => {}} options={options} ariaLabel="To" />);

    await user.click(screen.getByRole('combobox', { name: 'To' }));

    const list = await screen.findByRole('listbox');
    const content = list.closest('[data-radix-select-content], [role="listbox"]') as HTMLElement;
    expect(content.className).toContain('max-h-[var(--radix-select-content-available-height)]');
    expect(screen.getAllByRole('option')).toHaveLength(60);
  });
});

describe('a menu', () => {
  // MENU1: the dropdown menus had no atom, and a long one (a receiver filter, a long workspace list) ran off the
  // window as the select had. The menu is capped at the room the popper measures on its side, and scrolls inside.
  // Opened from the keyboard with a fresh `userEvent.setup()`, as AppMenu's and SyncStatus's tests are: the shared API
  // carries pointer state between tests, and a `defaultOpen` menu does not open under jsdom.
  const open = async (name: string) => {
    const user = userEvent.setup();
    screen.getByRole('button', { name }).focus();
    await user.keyboard('{Enter}');
    return screen.findByRole('menu');
  };

  it('caps its content at the room the window has, and scrolls inside it', async () => {
    render(
      <Menu.Root>
        <Menu.Trigger>Receiver</Menu.Trigger>
        <Menu.Content align="end">
          <Menu.Label>Receiver</Menu.Label>
          {Array.from({ length: 60 }, (_, i) => <Menu.Item key={i}>{`repository ${i}`}</Menu.Item>)}
        </Menu.Content>
      </Menu.Root>,
    );

    const menu = await open('Receiver');
    expect(menu.className).toContain('max-h-[var(--radix-dropdown-menu-content-available-height)]');
    expect(menu.className).toContain('overflow-y-auto');
    expect(screen.getAllByRole('menuitem')).toHaveLength(60);
    // A modal menu makes the body inert, and a menu in the title bar must leave the window draggable under it.
    expect(document.body.style.pointerEvents).not.toBe('none');
  });

  it('reserves the tick column where asked, ticks the current item, and leaves it out where not', async () => {
    render(
      <Menu.Root>
        <Menu.Trigger>Size</Menu.Trigger>
        <Menu.Content>
          <Menu.Item tick>100%</Menu.Item>
          <Menu.Item tick={false}>50%</Menu.Item>
          <Menu.Item>Fit</Menu.Item>
          <Menu.Separator />
          <Menu.CheckboxItem checked>Quests</Menu.CheckboxItem>
          <Menu.RadioGroup value="all">
            <Menu.RadioItem value="all">All</Menu.RadioItem>
          </Menu.RadioGroup>
        </Menu.Content>
      </Menu.Root>,
    );
    await open('Size');
    const item = (name: string) => screen.getByText(name).closest('[role^="menuitem"]') as HTMLElement;
    expect(item('100%').querySelector('svg')).not.toBeNull();
    expect(item('100%').className).toContain('text-ink');
    expect(item('50%').firstElementChild?.className).toContain('w-3.5');
    expect(item('50%').querySelector('svg')).toBeNull();
    expect(item('Fit').firstElementChild).toBeNull();
    // A checkbox and a radio item reserve the column always, and tick it while chosen.
    expect(item('Quests').getAttribute('role')).toBe('menuitemcheckbox');
    expect(item('Quests').querySelector('svg')).not.toBeNull();
    expect(item('All').getAttribute('role')).toBe('menuitemradio');
    expect(item('All').querySelector('svg')).not.toBeNull();
  });
});

describe('a setting row', () => {
  it('leads with the label, keeps the hint to a line, and holds the why off the page', () => {
    render(
      <Tooltip.Provider>
        <SettingRow
          label="Park a quest after this many failed sessions"
          hint="Also `daoris driver strikes <n>`."
          why="One quest whose session could not start ran eighteen times."
          control={<input aria-label="count" defaultValue="3" />}
        >
          <span>zero means it keeps trying</span>
        </SettingRow>
      </Tooltip.Provider>,
    );

    expect(screen.getByText('Park a quest after this many failed sessions')).toBeTruthy();
    expect(screen.getByText(/daoris driver strikes/)).toBeTruthy();
    expect(screen.getByLabelText('count')).toBeTruthy();
    expect(screen.getByText('zero means it keeps trying')).toBeTruthy();
    // The why is announced on its glyph and is not a paragraph anyone has to scroll past.
    expect(screen.getByRole('note', { name: /eighteen times/ })).toBeTruthy();
    expect(screen.queryByText(/eighteen times/)).toBeNull();
  });

  /**
   * UX5 U58, seen on the window: at 888 a path as the control took its whole width first, and the
   * label beside it was 24px wide, one character a line. The label's column has a floor, 16rem or
   * half the row, and the control gives way to it: a path breaks at its separators.
   */
  it('keeps a floor under the label, so a long control gives way to it', () => {
    const { container } = render(
      <SettingRow label="Which account each tool runs as" control={<span>a-long-path</span>} />);

    const grid = container.querySelector('.grid')!;
    expect(grid.getAttribute('class')).toContain('grid-cols-[minmax(min(16rem,50%),1fr)_auto]');
  });

  /**
   * WSR4, seen on the window: with Settings' nav and the side bar open a row was 309px, the floor left
   * the control 130px, and a branch pattern showed eleven characters. A row that narrow puts its control
   * under the label, at the row's full width; the row is its own container, so it answers to the width
   * it has, not the window's.
   */
  it('puts the control under the label when the row itself is narrow', () => {
    const { container } = render(
      <SettingRow label="How work lands" control={<input aria-label="pattern" />} />);

    const row = container.firstElementChild!;
    expect(row.getAttribute('class')).toContain('@container');
    const grid = row.querySelector('.grid')!;
    expect(grid.getAttribute('class')).toContain('@max-[26rem]:grid-cols-1');
    expect(screen.getByLabelText('pattern').parentElement!.getAttribute('class')).toContain('@max-[26rem]:justify-start');
  });
});

describe('the liveness dot', () => {
  it('carries its word, and hides the mark from the reader who already has the word', () => {
    const { container } = render(<Dot tone="live" label="working" />);
    expect(screen.getByText('working')).toBeInTheDocument();
    // One mark, and it is decorative: the label is the accessible name, said once.
    const marks = container.querySelectorAll('[aria-hidden="true"]');
    expect(marks).toHaveLength(1);
    expect(marks[0].className).toContain('bg-accent');
  });

  it('spends a different hue per meaning, so the five are told apart with the label AND without', () => {
    const hue = (tone: 'live' | 'parked' | 'failed' | 'ended' | 'idle') => {
      const { container } = render(<Dot tone={tone} label={tone} />);
      return container.querySelector('[aria-hidden="true"]')!.className;
    };
    const hues = [hue('live'), hue('parked'), hue('failed'), hue('ended'), hue('idle')];
    expect(new Set(hues).size).toBe(5);
  });

  /**
   * Ended covers completed, failed, declined and stopped alike, so its mark says only that nothing
   * is happening. Seen on the installed window: it wore done's green, and a failed session sat in the
   * rail with a success mark beside the word 失败.
   */
  it('never borrows an outcome hue for ended, which is every outcome at once', () => {
    const { container } = render(<Dot tone="ended" label="failed" />);
    expect(container.querySelector('[aria-hidden="true"]')!.className).not.toMatch(/\bbg-st-/);
  });

  /**
   * UX5 U1: waiting on the person wore declined's red on the dot and the pill, `--warn` on the map,
   * and open's amber on the band's card, three hues for one fact. It is the status palette's waiting
   * hue everywhere, and red stays an outcome's, which is what a failed tool call is.
   */
  it('wears the waiting hue for a person, the one the band\'s card wears, and keeps red for a failure', () => {
    const mark = (tone: 'parked' | 'failed') =>
      render(<Dot tone={tone} label={tone} />).container.querySelector('[aria-hidden="true"]')!.className;
    expect(mark('parked')).toContain('bg-st-open');
    expect(mark('failed')).toContain('bg-st-declined');
    const card = render(<WaitingCard title="waiting" />).container.querySelector('section')!;
    expect(card.className).toContain('border-l-st-open');
  });
});

/**
 * D118 §5: one item on a list closed to its strip — FRAME6's session strip row made general, so Plugins'
 * strip (PLUGUI1) is the same atom.
 */
describe('the strip mark', () => {
  const inStrip = (node: ReactElement) => render(<Tooltip.Provider><ul>{node}</ul></Tooltip.Provider>);

  it('names itself in words, and shows only the initial and the mark, both hidden from a reader', async () => {
    const onPress = vi.fn();
    const { container } = inStrip(<StripMark label="Chat · engine · working" initialOf="engine" tone="live" onPress={onPress} />);

    const mark = screen.getByRole('button', { name: 'Chat · engine · working' });
    expect(container.querySelectorAll('[aria-hidden="true"]')).toHaveLength(2);
    expect(mark).toHaveTextContent('e');
    expect(container.querySelector('[aria-hidden="true"].size-1\\.5')!.className).toContain('bg-accent');
    await userEvent.click(mark);
    expect(onPress).toHaveBeenCalledOnce();
  });

  it('marks the one chosen, and is a row its list\'s keys move to', () => {
    const { container } = inStrip(<StripMark label="engine" initialOf="engine" current />);
    expect(screen.getByRole('button', { name: 'engine' })).toHaveAttribute('aria-current', 'true');
    expect(container.querySelector('li')).toHaveAttribute('data-list-row');
  });

  /** D119: a plugin that is simply on wears no mark, and one switched off a faint initial. */
  it('wears no mark where it is given none, and a faint initial where it is dimmed', () => {
    const { container } = inStrip(<StripMark label="lint-on-save · off" initialOf="lint-on-save" dimmed />);
    expect(container.querySelector('.size-1\\.5')).toBeNull();
    expect(screen.getByText('l')).toHaveClass('text-ink-faint');
  });

  it('takes a 中文 name\'s first character whole', () => {
    inStrip(<StripMark label="引擎" initialOf="引擎" tone="parked" />);
    expect(screen.getByRole('button', { name: '引擎' })).toHaveTextContent('引');
  });
});

/**
 * The catalogues and the service mark a command or a name with backticks, and nothing rendered them:
 * the installed window printed `daoris driver notify on|off` with its backticks, in both languages.
 */
describe('a code span in a sentence', () => {
  it('sets a backticked span as code and drops the backticks, the words unchanged', () => {
    const { container } = render(<p><Inline text="Also `daoris agent rules`: the same file." /></p>);
    const code = container.querySelector('code')!;
    expect(code).toHaveTextContent('daoris agent rules');
    expect(code.className).toContain('font-mono');
    expect(container).toHaveTextContent('Also daoris agent rules: the same file.');
    expect(container.textContent).not.toContain('`');
  });

  it('sets every pair, and leaves a lone backtick as the character it is', () => {
    const { container } = render(<p><Inline text="`a` then `b` and ` alone" /></p>);
    expect([...container.querySelectorAll('code')].map((c) => c.textContent)).toEqual(['a', 'b']);
    expect(container).toHaveTextContent('a then b and ` alone');
  });

  it('is the setting row hint, because every hint is a sentence of the console', () => {
    const { container } = render(<SettingRow label="notify" hint="Also `daoris driver notify on|off`." />);
    expect(container.querySelector('code')).toHaveTextContent('daoris driver notify on|off');
  });
});

describe('the mono well', () => {
  it('renders its text verbatim — never translated, because output is data', () => {
    render(<MonoWell text={'line one\n  indented 道衍'} />);
    expect(screen.getByText(/indented 道衍/)).toBeInTheDocument();
  });

  it('states what fell out of the window rather than quietly skipping it', () => {
    render(<MonoWell text="tail" dropped={12_043} />);
    expect(screen.getByText(/12,?043/)).toBeInTheDocument();
  });

  it('says nothing about drops when nothing dropped', () => {
    render(<MonoWell text="tail" dropped={0} />);
    expect(screen.queryByText(/fell outside/)).not.toBeInTheDocument();
  });

  it('shows the live mark only while live', () => {
    const { rerender } = render(<MonoWell text="tail" label="console" live />);
    expect(screen.getByText('live')).toBeInTheDocument();
    rerender(<MonoWell text="tail" label="console" />);
    expect(screen.queryByText('live')).not.toBeInTheDocument();
  });
});

describe('the meta line', () => {
  it('omits a pair whose value is absent — an absence means something, a blank looks like a bug', () => {
    render(
      <MetaLine items={[
        { label: 'repository', value: 'engine' },
        { label: 'profile', value: null },
        { label: 'tree', value: '' },
      ]}
      />,
    );
    expect(screen.getByText('repository')).toBeInTheDocument();
    expect(screen.queryByText('profile')).not.toBeInTheDocument();
    expect(screen.queryByText('tree')).not.toBeInTheDocument();
  });

  /**
   * UX5 U8, seen on the window: `break-all` broke a tree anywhere, so the head read `family\g` over
   * `ame`. A path breaks after a separator, and inside a name only when that one name is wider than
   * the line, which is what `anywhere` means once the separators offer somewhere better.
   */
  it('lets a long path break after a separator rather than inside a name', () => {
    const { container } = render(
      <MetaLine items={[{ label: 'tree', value: 'D:\\work\\family\\game', mono: true }]} />);
    const path = container.querySelector('dd > span')!;
    expect(path.textContent).toBe('D:\\work\\family\\game');
    expect(path.className).not.toContain('break-all');
    expect(path.className).toContain('wrap-anywhere');
    expect(path.innerHTML).toBe('D:\\<wbr>work\\<wbr>family\\<wbr>game');
  });

  it('breaks a URL and a forward-slashed path the same way', () => {
    const { container } = render(<PathText path="https://host.example/a/b" />);
    expect(container.firstElementChild!.innerHTML).toBe('https:/<wbr>/<wbr>host.example/<wbr>a/<wbr>b');
  });
});

describe('the session tone map', () => {
  // The twin of QUEST_TONE, and the reason that one exists: the compiler holds the completeness, so
  // a tenth state cannot ship half-toned. The runtime half is that every state resolves to a tone.
  const STATES: SessionState[] = [
    'queued', 'starting', 'working', 'awaiting-person',
    'completed', 'declined', 'stood-down', 'failed', 'stopped',
  ];

  // The nine the wire carries, and the one the page shows a live chat between turns (UX5 U17).
  const SHOWN = [...STATES, 'idle'];

  it('tones every session state, and awaiting-person wears waiting, never an outcome', () => {
    for (const state of SHOWN) expect(SESSION_TONE[state as keyof typeof SESSION_TONE]).toBeTruthy();
    expect(Object.keys(SESSION_TONE).sort()).toEqual([...SHOWN].sort());
    // Declined's red beside the word "awaiting you" read as a session that had failed (UX5 U1).
    expect(SESSION_TONE['awaiting-person']).toBe('open');
    // Idle is quiet: no status hue, and no live mark (U17, the reference console's own reading).
    expect(SESSION_TONE.idle).toBe('neutral');
    expect(SESSION_DOT.idle).toBe('idle');
  });

  it('marks every session state as a dot too — the pill says which, the dot says whether', () => {
    for (const state of SHOWN) expect(SESSION_DOT[state as keyof typeof SESSION_DOT]).toBeTruthy();
    expect(Object.keys(SESSION_DOT).sort()).toEqual([...SHOWN].sort());
  });

  /**
   * UX5 U17: a live chat is idle between turns and working while one runs, as the driver says; a
   * turn it has not answered for is the record's word. Driven work is one long turn.
   */
  it('shows a live chat between turns as idle, and nothing else', () => {
    const chat = { kind: 'chat' as const, state: 'working' as const };
    expect(shownState(chat, false)).toBe('idle');
    expect(shownState(chat, true)).toBe('working');
    expect(shownState(chat, undefined)).toBe('working');
    expect(shownState({ kind: 'driven', state: 'working' }, false)).toBe('working');
    expect(shownState({ kind: 'chat', state: 'awaiting-person' }, false)).toBe('awaiting-person');
    expect(shownState({ kind: 'chat', state: 'starting' }, false)).toBe('starting');
  });

  /**
   * The reference console's one priority rule — a session needing its person outranks its own
   * activity — holds here by construction rather than by precedence: parked is a STATE, so there is
   * no busier state to lose to. This is the assertion that would fail if someone folded it into the
   * running set or gave it the live mark.
   */
  it('keeps attention out of the running marks, so activity can never outrank it', () => {
    expect(SESSION_DOT['awaiting-person']).toBe('parked');
    expect(SESSION_DOT.working).toBe('live');
    expect(SESSION_DOT.completed).toBe('ended');
  });

  it('counts exactly the four states that still hold a repository', () => {
    expect([...SESSION_ACTIVE].sort())
      .toEqual(['awaiting-person', 'queued', 'starting', 'working']);
    // Parked counts as active on purpose (D46 §4): the person is the flow control, not an exit.
    expect(SESSION_ACTIVE.has('awaiting-person')).toBe(true);
    expect(STATES.filter((state) => !SESSION_ACTIVE.has(state))).toHaveLength(5);
  });
});

/**
 * D41 §6 says a drawer is `role="dialog"` **with `aria-modal`**. Radix writes the role and traps the
 * focus but never writes that attribute, so the sentence described the design and not the page — for
 * as long as it had existed, with every gate green, because no gate reads prose.
 *
 * This is the gate that now reads it. It covers every modal surface the platform has, so the next one
 * is caught by the act of not being listed.
 */
describe('every modal surface says it is modal', () => {
  it('the drawer does', () => {
    render(
      <Tooltip.Provider>
        <Drawer title="Reading" onClose={() => {}}>a knowledge entry</Drawer>
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('dialog').getAttribute('aria-modal')).toBe('true');
  });

  it('the command palette does', () => {
    render(
      <CommandPalette
        open
        commands={[{ id: 'a', title: 'A', group: 'go', icon: 'overview', run: () => {} }]}
        onClose={() => {}}
      />,
    );
    expect(screen.getByRole('dialog').getAttribute('aria-modal')).toBe('true');
  });

  it('a box at the palette\'s place does, named by its title, its focus where it was asked to land', async () => {
    render(
      <Tooltip.Provider>
        <QuickPanel open onClose={() => {}} title="Quick Ask" header={{ icon: 'help', closeLabel: 'Close' }} initialFocus="textarea">
          <button type="button">first</button>
          <textarea aria-label="message" />
        </QuickPanel>
      </Tooltip.Provider>,
    );
    const box = screen.getByRole('dialog', { name: 'Quick Ask' });
    expect(box.getAttribute('aria-modal')).toBe('true');
    await waitFor(() => expect(screen.getByRole('textbox', { name: 'message' })).toHaveFocus());
    expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();
  });

  it('a box with no header still has its name, for a reader alone', () => {
    render(<QuickPanel open onClose={() => {}} title="Command palette"><input aria-label="typed" /></QuickPanel>);
    expect(screen.getByRole('dialog', { name: 'Command palette' }).getAttribute('aria-modal')).toBe('true');
    expect(screen.getByText('Command palette').className).toContain('sr-only');
  });
});

/**
 * A tooltip follows the editor's rules (2026-09-23): below its control, and gone on any scroll, key,
 * click or loss of focus — not only when the pointer leaves a trigger that can still say so.
 */
describe('a tip', () => {
  const show = () => render(
    <Tooltip.Provider delayDuration={0}>
      <div style={{ height: 400, overflow: 'auto' }} data-testid="list">
        <Tip content="what this does">
          <button type="button">act</button>
        </Tip>
      </div>
    </Tooltip.Provider>,
  );

  it('opens below its control, aligned to its leading edge', async () => {
    show();
    await userEvent.hover(screen.getByRole('button', { name: 'act' }));

    await screen.findByRole('tooltip');
    const content = document.querySelector('[data-side]');
    expect(content?.getAttribute('data-side')).toBe('bottom');
    expect(content?.getAttribute('data-align')).toBe('start');
  });

  it('goes when the page scrolls under it', async () => {
    show();
    await userEvent.hover(screen.getByRole('button', { name: 'act' }));
    await screen.findByRole('tooltip');

    fireEvent.scroll(screen.getByTestId('list'));

    await waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull());
  });

  it('goes when the window loses focus, and on a key', async () => {
    show();
    const act = screen.getByRole('button', { name: 'act' });

    await userEvent.hover(act);
    await screen.findByRole('tooltip');
    fireEvent(window, new Event('blur'));
    await waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull());

    await userEvent.unhover(act);
    await userEvent.hover(act);
    await screen.findByRole('tooltip');
    fireEvent.keyDown(document.body, { key: 'a' });
    await waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull());
  });
});

/**
 * 🔴 A count is a circle.
 * jsdom has no layout, so what can be held here is the cause: the box's height and its minimum width
 * are ONE size. The oval was two sizes nothing kept equal — a min-width, and a line-height plus a
 * border. The window's own measurement is the other half, taken on the desktop.
 */
describe('the count badge', () => {
  const sizes = (className: string) => ({
    height: /\bh-(\d+(?:\.\d+)?)\b/.exec(className)?.[1],
    minWidth: /\bmin-w-(\d+(?:\.\d+)?)\b/.exec(className)?.[1],
  });

  it('is as tall as it is wide at least, with no line-height to make it taller', () => {
    const { container } = render(<CountBadge count={3} />);
    const badge = container.firstElementChild as HTMLElement;
    const { height, minWidth } = sizes(badge.className);

    expect(height).toBeDefined();
    expect(height).toBe(minWidth);
    expect(badge.className).toContain('leading-none');
    expect(badge.className).toContain('rounded-full');
  });

  it('is nothing at zero — a zero badge is furniture — and caps a large count', () => {
    expect(render(<CountBadge count={0} />).container.firstChild).toBeNull();
    render(<CountBadge count={140} />);
    expect(screen.getByText('99+')).toBeInTheDocument();
  });
});

describe('the segmented choice', () => {
  const Theme = ({ onChange }: { onChange: (value: 'system' | 'light' | 'dark') => void }) => (
    <Segmented
      label="theme" value="system" onChange={onChange}
      options={[{ value: 'system', label: 'System' }, { value: 'light', label: 'Light' }, { value: 'dark', label: 'Dark' }]}
    />
  );

  it('is a radiogroup that says which is chosen, and chooses on a press', async () => {
    const chosen: string[] = [];
    render(<Theme onChange={(value) => chosen.push(value)} />);

    expect(screen.getByRole('radiogroup', { name: 'theme' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'System' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }));
    expect(chosen).toEqual(['dark']);
  });

  it('moves the choice with the arrow keys, wrapping at the ends, like every radiogroup', () => {
    const chosen: string[] = [];
    render(<Theme onChange={(value) => chosen.push(value)} />);

    fireEvent.keyDown(screen.getByRole('radiogroup'), { key: 'ArrowRight' });
    fireEvent.keyDown(screen.getByRole('radiogroup'), { key: 'ArrowLeft' });
    expect(chosen).toEqual(['light', 'dark']);
  });
});
