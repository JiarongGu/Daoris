import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { SessionState } from './api';
import './i18n';
import * as Tooltip from '@radix-ui/react-tooltip';
import {
  Button, Dot, Drawer, EmptyState, MetaLine, MonoWell, Pill, SESSION_ACTIVE, SESSION_DOT,
  SESSION_TONE, SettingRow, Tile,
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

  it('spends a different hue per meaning, so the four are told apart with the label AND without', () => {
    const hue = (tone: 'live' | 'parked' | 'ended' | 'idle') => {
      const { container } = render(<Dot tone={tone} label={tone} />);
      return container.querySelector('[aria-hidden="true"]')!.className;
    };
    const hues = [hue('live'), hue('parked'), hue('ended'), hue('idle')];
    expect(new Set(hues).size).toBe(4);
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

  it('tones every session state, and awaiting-person wears the one only a person can clear', () => {
    for (const state of STATES) expect(SESSION_TONE[state]).toBeTruthy();
    expect(Object.keys(SESSION_TONE).sort()).toEqual([...STATES].sort());
    expect(SESSION_TONE['awaiting-person']).toBe('declined');
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
