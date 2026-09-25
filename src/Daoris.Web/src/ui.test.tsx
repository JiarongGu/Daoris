import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { SessionState } from './api';
import './i18n';
import * as Tooltip from '@radix-ui/react-tooltip';
import {
  Button, CountBadge, Dot, Drawer, EmptyState, Inline, MetaLine, MonoWell, Pill, Segmented, SESSION_ACTIVE,
  SESSION_DOT, SESSION_TONE, SettingRow, Tile, Tip, WaitingCard,
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
        action={<Button>ask for something</Button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'ask for something' })).toBeInTheDocument();
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
    render(<Button variant="primary" disabled>publish quest</Button>);
    expect(screen.getByRole('button', { name: 'publish quest' })).toBeDisabled();
  });
});

/**
 * A setting is a row (2026-09-23): label, one-line hint, the control at the right — and the why on
 * the glyph, for the reader who asks, rather than as the paragraph that used to sit above every
 * control on the Machine view.
 */
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

  it('lets a long path break rather than pushing the line off the surface', () => {
    render(<MetaLine items={[{ label: 'tree', value: 'a/very/long/checkout/path', mono: true }]} />);
    expect(screen.getByText('a/very/long/checkout/path').className).toContain('break-all');
  });
});

describe('the session tone map', () => {
  // The twin of QUEST_TONE, and the reason that one exists: the compiler holds the completeness, so
  // a tenth state cannot ship half-toned. The runtime half is that every state resolves to a tone.
  const STATES: SessionState[] = [
    'queued', 'starting', 'working', 'awaiting-person',
    'completed', 'declined', 'stood-down', 'failed', 'stopped',
  ];

  it('tones every session state, and awaiting-person wears waiting, never an outcome', () => {
    for (const state of STATES) expect(SESSION_TONE[state]).toBeTruthy();
    expect(Object.keys(SESSION_TONE).sort()).toEqual([...STATES].sort());
    // Declined's red beside the word "awaiting you" read as a session that had failed (UX5 U1).
    expect(SESSION_TONE['awaiting-person']).toBe('open');
  });

  it('marks every session state as a dot too — the pill says which, the dot says whether', () => {
    for (const state of STATES) expect(SESSION_DOT[state]).toBeTruthy();
    expect(Object.keys(SESSION_DOT).sort()).toEqual([...STATES].sort());
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
 * 🔴 A count is a circle (owner, 2026-09-23: *"the notification number is not even circle border"*).
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
