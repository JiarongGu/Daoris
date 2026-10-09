import { type FocusEvent, type KeyboardEvent, type ReactNode, useEffect, useRef, useState } from 'react';
import { Icon, type IconName, Menu, Tip } from '../ui';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';

/** One row of a menu: an act, a choice, or a row that opens a submenu. */
export interface MenuItem {
  id: string;
  label: string;
  icon?: IconName;
  /** A count worth showing beside it. Zero wears nothing: a zero badge is furniture. */
  badge?: number;
  /** Separated from what came before — the shape VS Code uses for "and also". */
  separated?: boolean;
  /**
   * Ticked whatever is on screen: the workspace the window is scoped to (D75), the place in front, a region shown. Said,
   * not only drawn (UXFIX1): a toggle (a region shown) as a checkbox item, with `radio` one choice among its rows.
   */
  checked?: boolean;
  /** Its tick is one choice among the rows beside it (the place, the scope): a radio item, in one group with them. */
  radio?: true;
  /** Said, and not choosable: "no workspace yet" is a fact in a menu, and *Stop…* with nothing running keeps its place. */
  disabled?: boolean;
  /** Its key, shown at the right as VS Code's menus show one (DOCK1c, D152 §3). */
  shortcut?: string;
  /** Its group's name over it (Run's *This session*), with the tip that says what to open while its acts are off. */
  heading?: { label: string; tip?: string };
  /** The rows it opens to its side (View's *Theme*, *Language*): a radio group, the one in force ticked. */
  sub?: MenuItem[];
}

/** A menu's name, its letter shown while Alt is held: underlined where the name holds it, after the name where not. */
function Name({ label, letter, shown }: { label: string; letter?: string; shown: boolean }) {
  if (!shown || !letter) return <>{label}</>;
  const at = label.toLowerCase().indexOf(letter.toLowerCase());
  // 中文's names hold no Latin letter, so Windows writes it after the name: 工作区(W) (the design §3.3).
  if (at < 0) return <>{label}({letter})</>;
  return <>{label.slice(0, at)}<u className="underline-offset-2">{label[at]}</u>{label.slice(at + 1)}</>;
}

/** A row's own parts: its glyph's column, its name, then its count or its key. */
function Row({ item, glyphs }: { item: MenuItem; glyphs: boolean }) {
  return (
    <>
      {item.icon
        ? <Icon name={item.icon} size={13} className="shrink-0 opacity-70" />
        // The glyphs' column is kept for a row with none, so every name in the menu starts at one edge.
        : glyphs && <span aria-hidden className="w-[13px] shrink-0" />}
      <span className="truncate">{item.label}</span>
      {item.badge !== undefined && item.badge > 0 && (
        <span className="ml-auto shrink-0 font-mono text-meta tabular-nums text-ink-faint">{item.badge}</span>
      )}
      {item.shortcut && <kbd className="ml-auto shrink-0 pl-4 font-mono text-meta text-ink-faint">{item.shortcut}</kbd>}
    </>
  );
}

/**
 * One menu of the application's menu bar — the title bar's left, as every IDE has it.
 *
 * @remarks
 * **What it holds**, since D152: the application's verbs and places, as VS Code's bar does. It is built from the one
 * table (`commands.ts`, through `appMenus.ts`), so a row here is a row of the palette and a key of the drawer.
 *
 * **Open or not is the bar's**, where a bar holds it (`open`, `onOpenChange`), so one menu open moves with the pointer
 * and the arrows walk the bar; alone it holds its own.
 *
 * **Presentational, and it imports no hook from the data** (components §3): a row's check, its count, its key and its
 * group's name are props on the row.
 */
/**
 * A menu's rows in blocks: a row alone, or a run of choices (`radio`) together, since one radio group holds the rows a
 * choice is among. A rule or a group's name ends a run, as it ends a group.
 */
function blocksOf(items: readonly MenuItem[]): MenuItem[][] {
  const blocks: MenuItem[][] = [];
  for (const item of items) {
    const run = blocks.at(-1);
    if (item.radio && run?.[0]?.radio && !item.separated && !item.heading) run.push(item);
    else blocks.push([item]);
  }
  return blocks;
}

/**
 * A menu's rows (D152 §3.2): a rule before a new group, a record group's name over its first row, a row that opens a
 * submenu to its side, and every other row an act. The bar's menus and the fold's (UX7a2) draw the same rows.
 *
 * @remarks
 * **A tick is said, not only drawn** (UXFIX1): a toggle (a region shown) is a checkbox item and a choice (the place, the
 * scope) a radio item among its rows, each with `aria-checked`, so a screen reader hears the workspace in force and
 * whether a region is shown. Every row is still chosen through `choose`, the table's run, whatever its kind, and the tick
 * column is reserved on every row so the names line up.
 */
function MenuRows({ items, choose }: { items: readonly MenuItem[]; choose: (id: string) => void }) {
  const glyphs = items.some((item) => item.icon);
  return (
    <>
      {blocksOf(items).map((block) => {
        const item = block[0]!;
        return (
          <div key={item.id}>
            {item.separated && <Menu.Separator />}
            {item.heading && (
              item.heading.tip
                ? (
                  <Tip content={item.heading.tip} side="right">
                    <span className="block"><Menu.Label>{item.heading.label}</Menu.Label></span>
                  </Tip>
                )
                : <Menu.Label>{item.heading.label}</Menu.Label>
            )}
            {item.radio ? (
              <Menu.RadioGroup value={block.find((choice) => choice.checked)?.id ?? ''}>
                {block.map((choice) => (
                  <Menu.RadioItem key={choice.id} value={choice.id} disabled={choice.disabled} onSelect={() => choose(choice.id)}>
                    <Row item={choice} glyphs={glyphs} />
                  </Menu.RadioItem>
                ))}
              </Menu.RadioGroup>
            ) : item.sub ? (
              <Menu.Sub>
                <Menu.SubTrigger>
                  {glyphs && <span aria-hidden className="w-[13px] shrink-0" />}
                  <span className="truncate">{item.label}</span>
                </Menu.SubTrigger>
                <Menu.SubContent className="min-w-40">
                  <Menu.RadioGroup value={item.sub.find((choice) => choice.checked)?.id ?? ''} onValueChange={choose}>
                    {item.sub.map((choice) => (
                      <Menu.RadioItem key={choice.id} value={choice.id} disabled={choice.disabled}>
                        <span className="truncate">{choice.label}</span>
                      </Menu.RadioItem>
                    ))}
                  </Menu.RadioGroup>
                </Menu.SubContent>
              </Menu.Sub>
            ) : item.checked !== undefined ? (
              <Menu.CheckboxItem checked={item.checked} disabled={item.disabled} onSelect={() => choose(item.id)}>
                <Row item={item} glyphs={glyphs} />
              </Menu.CheckboxItem>
            ) : (
              <Menu.Item tick={false} disabled={item.disabled} onSelect={() => choose(item.id)}>
                <Row item={item} glyphs={glyphs} />
              </Menu.Item>
            )}
          </div>
        );
      })}
    </>
  );
}

/** A menu name's look on the strip: plain words at small gaps, as VS Code's bar is, and the fold's glyph beside them. */
const NAME = cn(
  // 🔴 No chevron, and tight. Measured against a real VS Code window: its menu bar is plain
  // words at small gaps — File Edit Selection View Go Run Terminal Help — and carries no
  // disclosure arrows at all. A menu bar is a convention strong enough not to need marking,
  // and eight chevrons in a title bar is eight pieces of furniture.
  'inline-flex items-center gap-1.5 rounded-control px-2 py-1 text-small',
  // A target of 28 px at least (UXFIX1, the platform language §6): `py-1` alone left the fold's ☰ about 23 px tall and a
  // name 24. A minimum, not a height, so the strip (`h-9`, 36 px) stays as tall as it was and centres the 28 in it.
  'min-h-7 min-w-7 justify-center',
  'transition-colors duration-(--speed)',
  'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
  'text-ink-soft hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
);

export function AppMenu({
  label, items, onChoose, trigger, open, onOpenChange, letter, mnemonic = false, onTriggerKey, onHover, onWalk, onCloseFocus, onChosen,
}: {
  label: string;
  items: MenuItem[];
  onChoose: (menu: string, item: string) => void;
  /** Its own name, for the callback. */
  trigger: string;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  /** The letter Alt opens it by (the design §3.3). */
  letter?: string;
  /** Whether Alt is held, which shows the letter. */
  mnemonic?: boolean;
  /** A key on its name while it is closed: the bar's ← and →, and Escape back to where the focus was. */
  onTriggerKey?: (event: KeyboardEvent<HTMLButtonElement>) => void;
  /** The pointer came onto its name: with another menu open, this one opens in its place. */
  onHover?: () => void;
  /** ← or → inside it, off a submenu: the menu beside it opens. */
  onWalk?: (by: 1 | -1) => void;
  /** Its close is about to hand the focus back: the bar decides where. */
  onCloseFocus?: (event: Event) => void;
  /** A row was chosen: the close that follows hands the focus back to where it was. */
  onChosen?: () => void;
}) {
  const choose = (id: string) => {
    onChosen?.();
    onChoose(trigger, id);
  };

  // Never modal, as no menu is: the window stays draggable under a menu in the title bar (`Menu.Root`).
  return (
    <Menu.Root {...(open !== undefined ? { open } : {})} {...(onOpenChange ? { onOpenChange } : {})}>
      <Menu.Trigger
        {...(letter ? { 'aria-keyshortcuts': `Alt+${letter}` } : {})}
        onPointerEnter={onHover}
        onKeyDown={onTriggerKey}
        className={NAME}
      >
        <Name label={label} letter={letter} shown={mnemonic} />
      </Menu.Trigger>

      <Menu.Content
        align="start"
        className="min-w-56"
        onCloseAutoFocus={onCloseFocus}
        onKeyDown={(event) => {
          if (!onWalk || (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft')) return;
          const target = event.target as HTMLElement;
          // A submenu's own arrows are its own: → opens one from its row, ← in it closes it.
          if (!event.currentTarget.contains(target) || target.closest('[aria-haspopup="menu"]')) return;
          event.preventDefault();
          onWalk(event.key === 'ArrowRight' ? 1 : -1);
        }}
      >
        <MenuRows items={items} choose={choose} />
      </Menu.Content>
    </Menu.Root>
  );
}

/** A menu of the bar: its id, its name, the letter Alt opens it by, and its rows. */
export type BarMenu = { id: string; label: string; letter?: string; items: MenuItem[] };

/** The attribute the bar's names carry, which `focusMenuBar` finds the first of. */
export const MENU_BAR = 'data-menu-bar';

/** Put the focus on the bar's first name (Alt released alone, or F10): a menu bar is reached from the keyboard. */
export function focusMenuBar(root: ParentNode): boolean {
  const first = root.querySelector<HTMLElement>(`[${MENU_BAR}] button`);
  first?.focus();
  return Boolean(first);
}

/**
 * The window's width, in CSS px, below which the seven menus fold into one (UX7a2, the design §3.3). UX7a measured a
 * shell's strip on its stories at about 590 px for the seven names beside the command center's glyph and the window's
 * buttons (D152's UX7a note); those stories drew no browser door, which the window's strip holds, 28 px more. 39rem keeps
 * that clear. A browser's six names fit a narrower window and fold at the same width, which was not measured apart.
 */
export const MENU_FOLD_BELOW = 624;

/** Whether the bar's names fold at a window this wide. A width nothing measured (0) folds nothing. */
export function foldsMenus(width: number): boolean {
  return width > 0 && width < MENU_FOLD_BELOW;
}

/** Whether the window is narrower than the bar's names need, followed as it resizes (the window decides, as FRAME6's does). */
export function useMenuFold(): boolean {
  const [width, setWidth] = useState(() => window.innerWidth);
  useEffect(() => {
    const onResize = () => setWidth(window.innerWidth);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);
  return foldsMenus(width);
}

/** What the caller's `open` holds while the fold is open with none of its menus open in it (UX7a2). */
export const FOLD_OPEN = 'fold';

/**
 * The application's menu bar (UX7a, D152 §3.3): the menus side by side, behaving as every Windows menu bar does.
 *
 * @remarks
 * - **One menu open moves with the pointer**: with a menu open, coming onto another's name opens it, and ← and → walk the
 *   bar from inside a menu and between the names. Escape closes a menu onto its name; Escape on a name hands the focus
 *   back to where it was before the bar took it, as choosing a row does.
 * - **Which menu is open is the caller's** (`open`), which builds a menu's rows as it opens, so a row reads what is in
 *   front of the person at that moment rather than when the strip last drew.
 * - Built on the dropdown the strip already had, so its names stay buttons that open a menu (`aria-haspopup`): a
 *   menubar primitive would have changed every name's role, and the tests across the page that open the View menu by it.
 * - **Narrower than its names need, it folds** (`compact`, UX7a2): one button, VS Code's ☰, whose menu's rows are the
 *   menus, each opening its own rows to its side. The keys reach it as they reach the bar: Alt alone and F10 focus it,
 *   Alt with a letter opens it at that menu, ↓ and ↑ walk the menus, → opens one onto its first row and ← goes back to
 *   its name, Escape closes onto the button and Escape there hands the focus back, as choosing a row does.
 */
export function AppMenuBar({ menus, open, onOpen, onChoose, mnemonics = false, label, compact = false, foldLabel }: {
  menus: readonly BarMenu[];
  /** The menu open, by its id; folded, `FOLD_OPEN` while the fold is open with no menu open in it. */
  open: string | null;
  onOpen: (menu: string | null) => void;
  onChoose: (menu: string, item: string) => void;
  /** Alt is held: each menu's letter is shown. */
  mnemonics?: boolean;
  /** The bar's own name, for a screen reader. */
  label?: string;
  /** The window is narrower than the names need (`useMenuFold`): the menus fold into one. */
  compact?: boolean;
  /** The fold's name, its tip and what a screen reader calls it. */
  foldLabel?: string;
}): ReactNode {
  const bar = useRef<HTMLElement>(null);
  // Where the focus was before the bar took it, which Escape on a name and a chosen row hand it back to.
  const before = useRef<HTMLElement | null>(null);
  const chose = useRef(false);
  const openNow = useRef(open);
  openNow.current = open;

  // The names are the bar's own buttons; a menu's rows are in a portal, never inside the bar.
  const names = () => [...(bar.current?.querySelectorAll<HTMLButtonElement>('button') ?? [])];
  const neighbour = (id: string, by: 1 | -1) => {
    const at = menus.findIndex((menu) => menu.id === id);
    return menus[(at + by + menus.length) % menus.length]!.id;
  };
  // Another menu in this one's place. Its name takes the focus first, so the row that held it is never removed from
  // under it: a menu closes when its window loses the focus, and the engine may say so when a focused row goes.
  const switchTo = (id: string) => {
    names()[menus.findIndex((menu) => menu.id === id)]?.focus();
    onOpen(id);
  };
  const giveBack = () => {
    const was = before.current;
    before.current = null;
    if (was?.isConnected) was.focus();
    else (document.activeElement as HTMLElement | null)?.blur();
  };
  // A row was chosen: the focus goes back to where it was, as a menu bar's does.
  const closeFocus = (event: Event) => {
    if (!chose.current) return;
    chose.current = false;
    event.preventDefault();
    giveBack();
  };

  // Widened with the fold open and none of its menus: no menu of the bar is open, so none is said to be.
  useEffect(() => {
    if (!compact && open === FOLD_OPEN) onOpen(null);
  }, [compact, open, onOpen]);

  return (
    <nav
      ref={bar}
      aria-label={label}
      {...{ [MENU_BAR]: '' }}
      className="flex items-center gap-0.5"
      // React's focus events travel the component tree, so a menu's rows, in their portal, are the bar's here: the focus
      // that came onto a name or into a menu from outside both is where it goes back to.
      onFocus={(event: FocusEvent<HTMLElement>) => {
        // The focus came onto the bar from outside it: remember from where.
        const from = event.relatedTarget;
        if (from instanceof HTMLElement && !event.currentTarget.contains(from) && !from.closest('[role="menu"]')) before.current = from;
      }}
    >
      {compact ? (
        <Fold
          menus={menus}
          open={open}
          label={foldLabel ?? label ?? ''}
          mnemonics={mnemonics}
          onOpen={onOpen}
          isOpen={(id) => openNow.current === id}
          anyOpen={() => openNow.current !== null}
          onEscape={giveBack}
          onChoose={(menu, item) => {
            chose.current = true;
            onChoose(menu, item);
          }}
          onCloseFocus={closeFocus}
        />
      ) : menus.map((menu, index) => (
        <AppMenu
          key={menu.id}
          trigger={menu.id}
          label={menu.label}
          letter={menu.letter}
          mnemonic={mnemonics}
          items={menu.items}
          open={open === menu.id}
          onOpenChange={(next) => {
            if (next) onOpen(menu.id);
            else if (openNow.current === menu.id) onOpen(null);
          }}
          onHover={() => { if (openNow.current !== null && openNow.current !== menu.id) switchTo(menu.id); }}
          onWalk={(by) => switchTo(neighbour(menu.id, by))}
          onTriggerKey={(event) => {
            // A menu's name is no field, and still asks, as every handler of Enter or Escape does (IME1).
            if (isComposing(event)) return;
            if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
              event.preventDefault();
              const all = names();
              all[(index + (event.key === 'ArrowRight' ? 1 : -1) + all.length) % all.length]?.focus();
            } else if (event.key === 'Escape' && openNow.current === null) {
              event.preventDefault();
              giveBack();
            }
          }}
          onChosen={() => { chose.current = true; }}
          onCloseFocus={(event) => {
            // Another menu took its place: the focus is that one's.
            if (openNow.current !== null) {
              event.preventDefault();
              return;
            }
            closeFocus(event);
          }}
          onChoose={onChoose}
        />
      ))}
    </nav>
  );
}

/**
 * The bar folded into one menu (UX7a2): VS Code's ☰ on a narrow window, its rows the menus in the bar's order, each one's
 * rows opening to its side. Which is open is still the bar's caller's, so Alt with a letter opens the fold at that menu
 * and the pointer moving down the rows opens each in turn, one at a time.
 */
function Fold({ menus, open, label, mnemonics, onOpen, isOpen, anyOpen, onEscape, onChoose, onCloseFocus }: {
  menus: readonly BarMenu[];
  open: string | null;
  label: string;
  mnemonics: boolean;
  onOpen: (menu: string | null) => void;
  /** Whether this menu is the one open now, read when Radix says one closed: a newer open may have taken its place. */
  isOpen: (id: string) => boolean;
  anyOpen: () => boolean;
  /** Escape on the button: the focus goes back to where it was. */
  onEscape: () => void;
  onChoose: (menu: string, item: string) => void;
  onCloseFocus: (event: Event) => void;
}) {
  // 🔴 Whether the fold's own rows hold the focus yet. Alt with a letter asks for the fold and a menu in it at once, and a
  // menu whose rows open before the fold's take the focus is closed by them: the fold, opening, focuses its rows, and a
  // submenu shuts on any focus outside it. So a menu opens in the fold only once the fold has taken the focus.
  const [settled, setSettled] = useState(false);
  if (open === null && settled) setSettled(false);

  return (
    // Never modal, as no menu is: the window stays draggable under a menu in the title bar (`Menu.Root`).
    <Menu.Root
      open={open !== null}
      onOpenChange={(next) => {
        if (next) onOpen(FOLD_OPEN);
        else if (anyOpen()) onOpen(null);
      }}
    >
      <Tip content={label}>
        <Menu.Trigger asChild>
          <button
            type="button"
            aria-label={label}
            className={NAME}
            onKeyDown={(event) => {
              if (isComposing(event) || event.key !== 'Escape' || anyOpen()) return;
              event.preventDefault();
              onEscape();
            }}
          >
            <Icon name="menu" size={15} />
          </button>
        </Menu.Trigger>
      </Tip>

      <Menu.Content
        align="start"
        className="min-w-48"
        // The fold takes the focus as it opens, its own or its first row's; a menu asked for opens after that.
        onFocus={() => { if (!settled) setSettled(true); }}
        onCloseAutoFocus={onCloseFocus}
      >
        {menus.map((menu) => (
          <Menu.Sub
            key={menu.id}
            open={settled && open === menu.id}
            onOpenChange={(next) => {
              // One menu open at a time, as on the bar; a menu that closed with another already open leaves that one be.
              if (next) onOpen(menu.id);
              else if (isOpen(menu.id)) onOpen(FOLD_OPEN);
            }}
          >
            <Menu.SubTrigger {...(menu.letter ? { 'aria-keyshortcuts': `Alt+${menu.letter}` } : {})}>
              <span className="truncate"><Name label={menu.label} letter={menu.letter} shown={mnemonics} /></span>
            </Menu.SubTrigger>
            <Menu.SubContent className="min-w-56">
              <MenuRows items={menu.items} choose={(item) => onChoose(menu.id, item)} />
            </Menu.SubContent>
          </Menu.Sub>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}
