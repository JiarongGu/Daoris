import { type ReactElement, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { SessionState } from './api';
import i18n from './i18n';
import * as Tooltip from '@radix-ui/react-tooltip';
import {
  Button, CodeText, CountBadge, Dot, Drawer, EmptyState, Inline, Menu, MetaLine, MonoWell, PathText, Pill, QuickPanel, Segmented,
  SelectField, SESSION_ACTIVE, SESSION_DOT, SESSION_TONE, SettingRow, shownKey, type ShownState, shownState, StripMark, Tile,
  Tip, WaitingCard,
} from './ui';
import { code } from './test/code';
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
    expect(screen.getByText('oldest has sat 12d').className).toContain('ink-open');
    expect(screen.getByText('3').className).not.toMatch(/(?:st|ink)-open/);
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

  it('reserves selected state for checkbox and radio rows, leaving acts without a tick', async () => {
    render(
      <Menu.Root>
        <Menu.Trigger>Size</Menu.Trigger>
        <Menu.Content>
          <Menu.Item tick={false}>Zoom in</Menu.Item>
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
    expect(item('Zoom in').firstElementChild?.className).toContain('w-3.5');
    expect(item('Zoom in').querySelector('svg')).toBeNull();
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
    expect(screen.getByText(code(/daoris driver strikes/))).toBeTruthy();
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

  /**
   * ACCTUX2: Claude Code and Codex both showed `C`. A mark of its own (UX7's *CC*, *Cx*) is drawn as given, its case kept,
   * in place of the first character, and faint where it is dimmed.
   */
  it('draws letters it is given as written, in place of the initial', () => {
    const { container } = inStrip(<>
      <StripMark label="Claude Code" initialOf="Claude Code" letters="CC" />
      <StripMark label="Codex" initialOf="Codex" letters="Cx" dimmed />
    </>);
    expect(screen.getByRole('button', { name: 'Claude Code' })).toHaveTextContent(/^CC$/);
    const codex = screen.getByText('Cx');
    expect(screen.getByRole('button', { name: 'Codex' })).toContainElement(codex);
    expect(codex).not.toHaveClass('uppercase');
    expect(codex).toHaveClass('text-ink-faint');
    expect(codex).toHaveAttribute('aria-hidden', 'true');
    expect(container.querySelectorAll('button')).toHaveLength(2);
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

/**
 * LOOK5, seen on the install in Settings → Agents in 中文: a browser may break after any hyphen, so a setting's
 * terminal twin read `--no-` over `keep` and `--` over `workspace`. jsdom lays nothing out, so what is held is the
 * structure that leaves a line nowhere to break inside a word: each word is an inline box no wider than its line,
 * which moves whole and breaks inside only when it alone is wider (`break-word`, U8), and the spaces between the
 * boxes are the span's only loose text.
 */
describe('a code span breaks only between its words', () => {
  const COMMAND = 'daoris agent profile use claude-code --keep <account>|--no-keep --workspace work';

  /** Each word's box, or the span itself where it is one word, with what lies loose between them. */
  const boxesOf = (code: Element) => ({
    words: code.children.length > 0 ? [...code.children] : [code],
    loose: [...code.childNodes].filter((node) => node.nodeType === Node.TEXT_NODE).map((node) => node.textContent ?? ''),
  });
  const unbreakable = (box: Element) => {
    expect(box.className).toContain('inline-block');
    expect(box.className).toContain('max-w-full');
    expect(box.className).toContain('wrap-break-word');
    expect(box.className).not.toContain('break-all');
  };

  it('sets each word of a command in its own box, the spaces between them, the text unchanged', () => {
    const { container } = render(<SettingRow label="keep" hint={`\`${COMMAND}\``} />);
    const code = container.querySelector('code')!;
    expect(code.textContent).toBe(COMMAND);
    const { words, loose } = boxesOf(code);
    expect(words.map((word) => word.textContent)).toEqual(COMMAND.split(' '));
    words.forEach(unbreakable);
    // Nothing but a space lies outside a box, so no hyphen is ever where the line may break.
    expect(loose.every((text) => /^\s+$/.test(text))).toBe(true);
    expect(words.find((word) => word.textContent === '<account>|--no-keep')).toBeTruthy();
    expect(words.find((word) => word.textContent === '--workspace')).toBeTruthy();
  });

  it('makes a span of one word its own box, so a flag alone never breaks either', () => {
    const { container } = render(<p><Inline text="Or `--no-keep` as a flag." /></p>);
    const code = container.querySelector('code')!;
    expect(code.textContent).toBe('--no-keep');
    expect(code.children).toHaveLength(0);
    unbreakable(code);
  });

  it('makes a span of one word and the comma after it one box, the flag still whole', () => {
    const { container } = render(<p><Inline text="Or `--no-keep`, a flag." /></p>);
    const code = container.querySelector('code')!;
    expect(code.textContent).toBe('--no-keep');
    expect(code.children).toHaveLength(0);
    unbreakable(code.parentElement!);
    expect(code.parentElement!.textContent).toBe('--no-keep,');
  });

  it('is the same span a screen sets as code itself, with its own size and ink', () => {
    const { container } = render(<CodeText text="daoris agent settings claude-code --account work" className="text-meta" />);
    const code = container.querySelector('code')!;
    expect(code.className).toContain('text-meta');
    expect(boxesOf(code).words.map((word) => word.textContent)).toEqual(['daoris', 'agent', 'settings', 'claude-code', '--account', 'work']);
    boxesOf(code).words.forEach(unbreakable);
  });
});

/**
 * UXFIX4b, seen at the branch list's 400 px floor: a long branch name filled its line and the comma after it opened the
 * next one, `, whose work…`, since Chromium may break after any inline box, even before a comma. jsdom lays nothing out,
 * so what is held is the structure that leaves the line nowhere to break there, measured in Chromium through the Sweep
 * floor stories: a word and the marks after it are one box, as text inside which no line breaks before a closing mark;
 * and where the marks cannot share the word's box, the last of a command's words, its box is the only box in a no-wrap
 * group, which leaves the break after it to the same rules, and it leaves the marks room on its line.
 */
describe('a code span keeps the punctuation after it on its line', () => {
  const LONG = 'feature/0fda18-fix-the-api-gap-before-the-quarter-closes';

  /** The box a one-word span and the marks after it share: the code, then the marks as text, nothing else. */
  const sharing = (code: Element, marks: string) => {
    const box = code.parentElement!;
    expect(box).toHaveClass('inline-block', 'max-w-full', 'wrap-break-word');
    expect([...box.childNodes]).toHaveLength(2);
    expect(box.firstChild).toBe(code);
    expect(code.nextSibling?.nodeType).toBe(Node.TEXT_NODE);
    expect(code.nextSibling!.textContent).toBe(marks);
    // The code is the word alone: the marks are drawn beside it, never as code.
    expect(code).not.toHaveClass('inline-block');
    return box;
  };

  it('draws a one-word span and the comma after it as one box, the sentence unchanged', () => {
    const { container } = render(<p><Inline text={`Inside \`${LONG}\`, whose work is on the line.`} /></p>);
    const code = container.querySelector('code')!;
    expect(code.textContent).toBe(LONG);
    expect(sharing(code, ',').textContent).toBe(`${LONG},`);
    expect(container.textContent).toBe(`Inside ${LONG}, whose work is on the line.`);
  });

  it('keeps 中文 punctuation with its span too', () => {
    const { container } = render(<p><Inline text={`它生长自 \`${LONG}\`，而后者的工作尚未到达主线。`} /></p>);
    const code = container.querySelector('code')!;
    expect(sharing(code, '，').textContent).toBe(`${LONG}，`);
    expect(container.textContent).toBe(`它生长自 ${LONG}，而后者的工作尚未到达主线。`);
  });

  it('keeps every closing mark that follows', () => {
    const { container } = render(<p><Inline text={`见 (\`${LONG}\`).`} /></p>);
    const code = container.querySelector('code')!;
    expect(sharing(code, ').').textContent).toBe(`${LONG}).`);
    expect(container.textContent).toBe(`见 (${LONG}).`);
  });

  it('groups a command’s last word, its other words still breaking between them, the marks right after it', () => {
    const { container } = render(<p><Inline text="Also `daoris agent rules`: the same file." /></p>);
    const code = container.querySelector('code')!;
    expect(code.textContent).toBe('daoris agent rules');
    const group = code.querySelector('.whitespace-nowrap')!;
    expect(group.parentElement).toBe(code);
    expect(group.children).toHaveLength(1);
    const last = group.firstElementChild as HTMLElement;
    expect(last.textContent).toBe('rules');
    // Still a word that breaks inside when it alone is wider than its line, less the room its mark takes.
    expect(last).toHaveClass('inline-block', 'wrap-break-word', 'whitespace-normal');
    expect(last.style.maxWidth).toBe('calc(100% - 1em)');
    // The words before it are boxes of their own with the spaces loose between them, so the line breaks between words.
    const [daoris, agent] = [...code.children];
    expect([daoris!.textContent, agent!.textContent]).toEqual(['daoris', 'agent']);
    expect(daoris).toHaveClass('inline-block', 'max-w-full');
    expect(daoris!.parentElement).toBe(code);
    expect(code.nextSibling!.textContent!.startsWith(':')).toBe(true);
    expect(container.textContent).toBe('Also daoris agent rules: the same file.');
  });

  it('leaves a span no mark follows as it was: its own box, in no group', () => {
    const { container } = render(<p><Inline text={`Inside \`${LONG}\` whose work is on the line, then \`a b\``} /></p>);
    const [one, two] = [...container.querySelectorAll('code')];
    expect(one!.parentElement).toBe(container.firstElementChild);
    expect(one).toHaveClass('inline-block', 'max-w-full');
    expect(two!.querySelector('.whitespace-nowrap')).toBeNull();
    expect(two!.nextSibling).toBeNull();
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

  // The nine the wire carries, the one the page shows a live chat between turns (UX5 U17), the two words the
  // session list's reader derives from the session's quest (D126 §2.2), the park the person answered (ANSWER1c), and a
  // record going on with their words (MSG1f).
  const SHOWN = [...STATES, 'idle', 'parked', 'awaiting-reply', 'answered', 'going-on'];

  it('tones every session state, and awaiting-person wears waiting, never an outcome', () => {
    for (const state of SHOWN) expect(SESSION_TONE[state as keyof typeof SESSION_TONE]).toBeTruthy();
    expect(Object.keys(SESSION_TONE).sort()).toEqual([...SHOWN].sort());
    // Declined's red beside the word "awaiting you" read as a session that had failed (UX5 U1).
    expect(SESSION_TONE['awaiting-person']).toBe('open');
    // Idle is quiet: no status hue, and no live mark (U17, the reference console's own reading).
    expect(SESSION_TONE.idle).toBe('neutral');
    expect(SESSION_DOT.idle).toBe('idle');
  });

  /**
   * D126 §2.3, audit M6: open's hue means it waits on you, and nothing else in the list wears it. `queued` shared it
   * and left the word to tell the two apart; it keeps its idle mark and wears no status hue.
   */
  it("keeps open's hue for what waits on the person alone", () => {
    const open = SHOWN.filter((state) => SESSION_TONE[state as keyof typeof SESSION_TONE] === 'open').sort();
    expect(open).toEqual(['awaiting-person', 'parked']);
    expect(SESSION_TONE.queued).toBe('neutral');
    expect(SESSION_DOT.queued).toBe('idle');
    // A parked quest's last session waits on the person: the waiting mark, never the failure's red (§2.3).
    expect(SESSION_DOT.parked).toBe('parked');
    // Awaiting a reply, nothing runs and nothing waits on the person: quiet.
    expect(SESSION_TONE['awaiting-reply']).toBe('neutral');
    expect(SESSION_DOT['awaiting-reply']).toBe('idle');
    // ANSWER1c: answered, it waits on the driver's next look, as a queued session does, and no longer on the person.
    expect(SESSION_TONE.answered).toBe('neutral');
    expect(SESSION_DOT.answered).toBe('idle');
  });

  /** Each shown state has its word: the record's in `sessionState`, the reader's two in `work.shown` (D126 §8). */
  it('names every shown state by a key both catalogues hold', () => {
    expect(shownKey('queued')).toBe('sessionState.queued');
    expect(shownKey('idle')).toBe('sessionState.idle');
    expect(shownKey('parked')).toBe('work.shown.parked');
    expect(shownKey('awaiting-reply')).toBe('work.shown.awaitingReply');
    expect(shownKey('answered')).toBe('work.shown.answered');
    for (const state of SHOWN) {
      expect(i18n.exists(shownKey(state as ShownState), { lng: 'en' })).toBe(true);
      expect(i18n.exists(shownKey(state as ShownState), { lng: 'zh' })).toBe(true);
    }
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
   * ANSWER1c (D131): a park the person answered stays parked until the driver's next look, with the answer set, and the
   * same session goes on then. Its state still says it waits on the person; it shows as answered, so no surface asks
   * the person again. An answer this machine was not told (a teammate's, or none) leaves it waiting.
   */
  it('shows a park the person answered as answered until the driver takes it up', () => {
    expect(shownState({ kind: 'driven', state: 'awaiting-person', answer: 'Use the second.' }, undefined)).toBe('answered');
    expect(shownState({ kind: 'driven', state: 'awaiting-person', answer: null }, undefined)).toBe('awaiting-person');
    expect(shownState({ kind: 'driven', state: 'awaiting-person', answer: '' }, undefined)).toBe('awaiting-person');
    // Once the driver takes it up the record says so: going on while the words still wait for its first prompt (MSG1f).
    expect(shownState({ kind: 'driven', state: 'working', answer: 'Use the second.' }, undefined)).toBe('going-on');
  });

  /**
   * MSG1f (D137 §3.2): a record that went on with the person's words and whose run is opening shows *going on*, as an
   * answered park shows *answered*: it keeps the words until its run's first prompt takes them, and then it is working.
   * An ended record with words waiting is the reader's to say, since words it cannot take wait there too.
   */
  it('shows a record whose words wait for its run to open as going on', () => {
    for (const state of ['queued', 'starting', 'working'] as const) {
      expect(shownState({ kind: 'driven', state, answer: 'also cap it' }, undefined)).toBe('going-on');
    }
    expect(shownState({ kind: 'chat', state: 'working', answer: 'also cap it' }, false)).toBe('going-on');
    expect(shownState({ kind: 'driven', state: 'working', answer: null }, undefined)).toBe('working');
    expect(shownState({ kind: 'driven', state: 'completed', answer: 'also cap it' }, undefined)).toBe('completed');
    expect(SESSION_TONE['going-on']).toBe('neutral');
    expect(SESSION_DOT['going-on']).toBe('idle');
    expect(shownKey('going-on')).toBe('work.shown.goingOn');
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
 * IME1: Radix closes a dialog on any Escape, heard on the document before the field it was pressed in, so dropping an
 * input method's composition in Quick Ask's box closed the box. The atoms that open one keep that Escape.
 */
describe('a dialog keeps the Escape an input method is composing with', () => {
  it('the box at the palette\'s place stays open on it, and closes on a plain one', () => {
    const onClose = vi.fn();
    render(
      <Tooltip.Provider>
        <QuickPanel open onClose={onClose} title="Quick Ask" header={{ icon: 'help', closeLabel: 'Close' }}>
          <textarea aria-label="message" />
        </QuickPanel>
      </Tooltip.Provider>,
    );
    const field = screen.getByRole('textbox', { name: 'message' });

    fireEvent.keyDown(field, { key: 'Escape', isComposing: true });
    fireEvent.keyDown(field, { key: 'Escape', keyCode: 229 });
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.keyDown(field, { key: 'Escape', keyCode: 27 });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('the drawer does too', () => {
    const onClose = vi.fn();
    render(
      <Tooltip.Provider>
        <Drawer title="New quest" onClose={onClose}><input aria-label="title" /></Drawer>
      </Tooltip.Provider>,
    );
    const field = screen.getByRole('textbox', { name: 'title' });

    fireEvent.keyDown(field, { key: 'Escape', isComposing: true });
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.keyDown(field, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
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

  /**
   * UXFIX1: the focus moves with the choice, as the WAI-ARIA radio group's does. Left on the option it came from, Space
   * chose that one again and a screen reader stayed on an option no longer chosen. Every `Segmented` is this atom (only
   * `ui.tsx` builds a radio), so this holds Knowledge's mode, Settings' rows and the review's choices alike.
   */
  it('moves the focus with the choice on every arrow, so the chosen option holds it and Space keeps it', async () => {
    function Held() {
      const [value, setValue] = useState<'system' | 'light' | 'dark'>('system');
      return (
        <Segmented
          label="theme" value={value} onChange={setValue}
          options={[{ value: 'system', label: 'System' }, { value: 'light', label: 'Light' }, { value: 'dark', label: 'Dark' }]}
        />
      );
    }
    render(<Held />);
    const user = userEvent.setup();
    const radio = (name: string) => screen.getByRole('radio', { name });
    await user.tab();
    expect(document.activeElement).toBe(radio('System'));

    const steps = [
      ['{ArrowRight}', 'Light'], ['{ArrowDown}', 'Dark'], ['{ArrowRight}', 'System'], ['{ArrowLeft}', 'Dark'], ['{ArrowUp}', 'Light'],
    ] as const;
    for (const [key, name] of steps) {
      await user.keyboard(key);
      expect(document.activeElement, `after ${key}`).toBe(radio(name));
      expect(radio(name)).toHaveAttribute('aria-checked', 'true');
      expect(radio(name)).toHaveAttribute('tabindex', '0');
    }

    await user.keyboard(' ');
    expect(radio('Light')).toHaveAttribute('aria-checked', 'true');
    expect(document.activeElement).toBe(radio('Light'));
  });

  /** UXFIX1: an arrow moves on from the option that holds the focus, so a choice its owner has not taken yet is not lost. */
  it('moves on from the focused option while the value it chose is still on its way', async () => {
    const chosen: string[] = [];
    render(<Theme onChange={(value) => chosen.push(value)} />);
    const user = userEvent.setup();
    await user.tab();
    await user.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Light' }));
    await user.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Dark' }));
    expect(chosen).toEqual(['light', 'dark']);
  });

  /** UX6i: a list's head spreads its choice over the row, each option an equal share (Knowledge's Search · Convergence). */
  it('fills its row where asked, each option an equal share, and sits at its own width otherwise', () => {
    render(
      <Segmented
        label="mode" value="a" onChange={() => {}} fill
        options={[{ value: 'a', label: 'Search' }, { value: 'b', label: 'Convergence' }]}
      />,
    );
    expect(screen.getByRole('radiogroup', { name: 'mode' })).toHaveClass('flex', 'w-full');
    for (const option of screen.getAllByRole('radio')) expect(option).toHaveClass('flex-1', 'justify-center');
    render(<Theme onChange={() => {}} />);
    expect(screen.getByRole('radiogroup', { name: 'theme' })).toHaveClass('inline-flex');
    expect(screen.getByRole('radio', { name: 'Dark' })).not.toHaveClass('flex-1');
  });
});
