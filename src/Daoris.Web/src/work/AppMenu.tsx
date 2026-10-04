import { type FocusEvent, type KeyboardEvent, type ReactNode, useRef } from 'react';
import { Icon, type IconName, Menu, Tip } from '../ui';
import { cn } from '../lib/cn';

/** One row of a menu: an act, a choice, or a row that opens a submenu. */
export interface MenuItem {
  id: string;
  label: string;
  icon?: IconName;
  /** A count worth showing beside it. Zero wears nothing: a zero badge is furniture. */
  badge?: number;
  /** Separated from what came before — the shape VS Code uses for "and also". */
  separated?: boolean;
  /** Ticked whatever is on screen: the workspace the window is scoped to (D75), the place in front, a region shown. */
  checked?: boolean;
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
  const glyphs = items.some((item) => item.icon);
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
        className={cn(
          // 🔴 No chevron, and tight. Measured against a real VS Code window: its menu bar is plain
          // words at small gaps — File Edit Selection View Go Run Terminal Help — and carries no
          // disclosure arrows at all. A menu bar is a convention strong enough not to need marking,
          // and eight chevrons in a title bar is eight pieces of furniture.
          'inline-flex items-center gap-1.5 rounded-control px-2 py-1 text-small',
          'transition-colors duration-(--speed)',
          'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
          'text-ink-soft hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
        )}
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
        {items.map((item) => (
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
            {item.sub ? (
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
            ) : (
              // The tick column is always reserved, so the labels line up whether or not anything is ticked.
              <Menu.Item tick={Boolean(item.checked)} disabled={item.disabled} onSelect={() => choose(item.id)}>
                <Row item={item} glyphs={glyphs} />
              </Menu.Item>
            )}
          </div>
        ))}
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
 */
export function AppMenuBar({ menus, open, onOpen, onChoose, mnemonics = false, label }: {
  menus: readonly BarMenu[];
  open: string | null;
  onOpen: (menu: string | null) => void;
  onChoose: (menu: string, item: string) => void;
  /** Alt is held: each menu's letter is shown. */
  mnemonics?: boolean;
  /** The bar's own name, for a screen reader. */
  label?: string;
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

  return (
    <nav
      ref={bar}
      aria-label={label}
      {...{ [MENU_BAR]: '' }}
      className="flex items-center gap-0.5"
      onFocus={(event: FocusEvent<HTMLElement>) => {
        // The focus came onto the bar from outside it: remember from where.
        const from = event.relatedTarget;
        if (from instanceof HTMLElement && !event.currentTarget.contains(from) && !from.closest('[role="menu"]')) before.current = from;
      }}
    >
      {menus.map((menu, index) => (
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
            // A row was chosen: the focus goes back to where it was, as a menu bar's does.
            if (chose.current) {
              chose.current = false;
              event.preventDefault();
              giveBack();
            }
          }}
          onChoose={onChoose}
        />
      ))}
    </nav>
  );
}
