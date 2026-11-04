import { type ComponentProps, createContext, type ReactNode, useContext } from 'react';
import { cn } from '../../lib/cn';
import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { type IconName, Icon, FRAME_HELD, FrameHeld } from './Icon';

/* ---------------------------------------------------------------- menus */

/**
 * What a menu's rows light with under the pointer and the keys (MENU1). Two looks were already in the window when the
 * menus became one atom, and the atom kept both rather than change what a person sees: the title bar's menus and most
 * others light a row with the raised surface and lift its ink from soft, as a menu bar's do; a list's ⋯ and `＋` and a
 * session row's menu, which open from among a list's rows, light it with the accent's soft field, as those rows do.
 */
type MenuHighlight = 'raised' | 'accent';

const MenuHighlightContext = createContext<MenuHighlight>('raised');

/** A row's shape: one density for every menu, the highlight by the menu's look, a disabled row a fact rather than an act. */
const MENU_ROW = 'flex items-center gap-2 rounded-control px-2 py-1.5 text-small outline-none data-[disabled]:cursor-default data-[disabled]:text-ink-faint';
const MENU_LOOK: Record<MenuHighlight, string> = {
  raised: 'cursor-pointer text-ink-soft data-[highlighted]:bg-raised data-[highlighted]:text-ink',
  accent: 'cursor-default text-ink data-[highlighted]:bg-accent-soft',
};

/** A row's classes, by its menu's look: what a ticked row adds is the full ink, which an accent row already wears. */
function useMenuRow(className: string | undefined, ...extra: (string | false | undefined)[]): string {
  return cn(MENU_ROW, MENU_LOOK[useContext(MenuHighlightContext)], ...extra, className);
}

/**
 * The tick's column, always the same width, so the labels line up whether or not anything is ticked: a list that shifts
 * by 16px when the tick moves reads as two lists.
 */
function MenuTickColumn({ children }: { children?: ReactNode }) {
  return <span className="flex w-3.5 shrink-0 justify-center">{children}</span>;
}

/**
 * 🔴 `modal={false}`, for every menu. A modal menu makes the rest of the page inert (Radix puts `pointer-events: none`
 * on the body while it is open), and a menu in the title bar has no business doing that: VS Code's do not, and the
 * window must stay draggable beneath it. It also locks the scroll, which shifts the layout by the scrollbar's width on
 * every open. Found by two tests that could not click anything after an earlier test left a menu open (AppMenu's rule,
 * which every menu had written out on its own).
 */
function MenuRoot(props: ComponentProps<typeof DropdownMenu.Root>) {
  return <DropdownMenu.Root modal={false} {...props} />;
}

/** A menu's layer and cap, its own size or the frame's (ZOOM1), as `MenuContent` says. */
const menuCap = (held: boolean) => cn(
  'z-30 overflow-y-auto overflow-x-hidden',
  held
    ? cn(FRAME_HELD, 'max-h-[calc(var(--radix-dropdown-menu-content-available-height)/var(--frame-zoom,1))]')
    : 'max-h-[var(--radix-dropdown-menu-content-available-height)]',
);

/**
 * A menu's content, in its portal: the overlay surface, the control's radius and the menus' shadow, 4px off its
 * trigger and 8px off the window's edges. Its width is the caller's (`className`), since a menu bar's and a status
 * item's menus are sized to what they hold.
 *
 * @remarks
 * **Capped at the room the popper measured on its side, and scrolled inside it** (MENU1, as SELECT1 for the select):
 * a long menu (a receiver filter, a long workspace list) ran off the window with nothing to scroll it by. The bar is
 * the theme's (`tokens.css`), and the keys still reach every row, since focusing one scrolls it into view.
 *
 * **Opened from the frame, it keeps the frame's size** (ZOOM1): the room the popper measured is the window's, so its cap is
 * read back through the frame's zoom.
 */
function MenuContent({ highlight = 'raised', className, children, ...props }: ComponentProps<typeof DropdownMenu.Content> & {
  /** What its rows light with: the raised surface by default, the accent's field for a list's own menus. */
  highlight?: MenuHighlight;
}) {
  const held = useContext(FrameHeld);
  return (
    <DropdownMenu.Portal>
      <DropdownMenu.Content
        sideOffset={4}
        collisionPadding={8}
        {...props}
        className={cn(menuCap(held), 'rounded-control border border-line bg-overlay p-1 text-small shadow-lg', className)}
      >
        <MenuHighlightContext.Provider value={highlight}>{children}</MenuHighlightContext.Provider>
      </DropdownMenu.Content>
    </DropdownMenu.Portal>
  );
}

/**
 * An act, with no selected state. A toggle is a `CheckboxItem` and a choice among rows a `RadioItem` in a `RadioGroup`,
 * each with `aria-checked`, so the current choice is heard as well as drawn (UXFIX1c). `tick={false}` reserves their
 * column for alignment; a plain act cannot draw a selected tick.
 */
function MenuRow({ tick, className, children, ...props }: ComponentProps<typeof DropdownMenu.Item> & { tick?: false }) {
  const row = useMenuRow(className);
  return (
    <DropdownMenu.Item {...props} className={row}>
      {tick === false && <MenuTickColumn />}
      {children}
    </DropdownMenu.Item>
  );
}

/** A toggle, ticked while it is on (a filter's *Include closed*, a kind of line drawn). */
function MenuCheckboxRow({ className, children, ...props }: ComponentProps<typeof DropdownMenu.CheckboxItem>) {
  const row = useMenuRow(className, 'data-[state=checked]:text-ink');
  return (
    <DropdownMenu.CheckboxItem {...props} className={row}>
      <MenuTickColumn><DropdownMenu.ItemIndicator><Icon name="check" size={12} /></DropdownMenu.ItemIndicator></MenuTickColumn>
      {children}
    </DropdownMenu.CheckboxItem>
  );
}

/** One value among several, in a `Menu.RadioGroup` named for what it chooses, the chosen one ticked. */
function MenuRadioRow({ className, children, ...props }: ComponentProps<typeof DropdownMenu.RadioItem>) {
  const row = useMenuRow(className, 'data-[state=checked]:text-ink');
  return (
    <DropdownMenu.RadioItem {...props} className={row}>
      <MenuTickColumn><DropdownMenu.ItemIndicator><Icon name="check" size={12} /></DropdownMenu.ItemIndicator></MenuTickColumn>
      {children}
    </DropdownMenu.RadioItem>
  );
}

/** A group's name, small and faint; its padding above is the caller's where it opens the menu or follows a rule. */
function MenuLabel({ className, ...props }: ComponentProps<typeof DropdownMenu.Label>) {
  return <DropdownMenu.Label {...props} className={cn('px-2 pb-1 pt-1 text-meta text-ink-faint', className)} />;
}

/** The rule between a menu's groups. */
function MenuSeparator({ className, ...props }: ComponentProps<typeof DropdownMenu.Separator>) {
  return <DropdownMenu.Separator {...props} className={cn('my-1 h-px bg-line', className)} />;
}

/**
 * A row that opens a menu to its side (UX7a, D152 §3.3): View's *Theme* and *Language*, on hover or →. Its tick column
 * is kept, so its name lines up with the rows around it, and the chevron says it opens rather than acts.
 */
function MenuSubRow({ className, children, ...props }: ComponentProps<typeof DropdownMenu.SubTrigger>) {
  const row = useMenuRow(className, 'data-[state=open]:bg-raised data-[state=open]:text-ink');
  return (
    <DropdownMenu.SubTrigger {...props} className={row}>
      <MenuTickColumn />
      {children}
      <Icon name="chevronRight" size={12} className="ml-auto shrink-0 text-ink-faint" />
    </DropdownMenu.SubTrigger>
  );
}

/** The menu a sub-row opens, in its portal, with a parent's surface and cap, 4px off the row. */
function MenuSubContent({ className, children, ...props }: ComponentProps<typeof DropdownMenu.SubContent>) {
  const highlight = useContext(MenuHighlightContext);
  const held = useContext(FrameHeld);
  return (
    <DropdownMenu.Portal>
      <DropdownMenu.SubContent
        sideOffset={4}
        collisionPadding={8}
        {...props}
        className={cn(menuCap(held), 'rounded-control border border-line bg-overlay p-1 text-small shadow-lg', className)}
      >
        <MenuHighlightContext.Provider value={highlight}>{children}</MenuHighlightContext.Provider>
      </DropdownMenu.SubContent>
    </DropdownMenu.Portal>
  );
}

/**
 * One act a menu offers (CTX1, D138): its name, its glyph, and what choosing it does. A surface's ⋯ and its right-click
 * draw the same list, so the two never offer a different set.
 */
export type MenuAct = {
  /** Its name in the surface's own rule (`answer`, `take`, `copy`), unique in its menu. */
  id: string;
  /** What it is called, in the reader's language: the same words its button says. */
  label: string;
  icon?: IconName;
  /** Offered and not choosable now: an act on its way. */
  disabled?: boolean;
  /** What choosing it does. */
  onSelect?: () => void;
  /** Or the words it copies, which the menu's owner copies and says it copied, since nothing on the screen shows it. */
  copy?: string;
};

/**
 * A list of acts as a menu's rows (CTX1): its glyph in a column of its own, so the names line up whether or not each act
 * has one, then its name. A copy goes to `onCopy`, the menu owner's.
 */
function MenuActRows({ acts, onCopy }: { acts: readonly MenuAct[]; onCopy?: (text: string) => void }) {
  return (
    <>
      {acts.map((act) => (
        <MenuRow
          key={act.id}
          disabled={act.disabled}
          onSelect={() => {
            if (act.onSelect) act.onSelect();
            else if (act.copy !== undefined) onCopy?.(act.copy);
          }}
        >
          {act.icon
            ? <Icon name={act.icon} size={12} className="shrink-0 text-ink-faint" />
            : <span aria-hidden className="w-3 shrink-0" />}
          {act.label}
        </MenuRow>
      ))}
    </>
  );
}

/**
 * **The menu** (MENU1): every dropdown in the window, on Radix's dropdown menu, as `SelectField` is every select. Its
 * parts are Radix's names, so a menu reads as one did before it had an atom, and every pixel is here: the surface,
 * the cap, the row's density and look, the tick's column, a label and a rule.
 *
 * @remarks
 * Eight files had each styled the primitive on their own, and none capped its height, so a long menu ran off the
 * window. `primitives.test.ts` now holds that no file but the atoms imports a primitive. The sub-menu came with the menu
 * bar's View (UX7a), whose *Theme* and *Language* each open one. `Acts` draws a surface's acts (CTX1), as its ⋯ and the
 * right-click menu both do.
 */
export const Menu = {
  Root: MenuRoot,
  Trigger: DropdownMenu.Trigger,
  Content: MenuContent,
  Item: MenuRow,
  CheckboxItem: MenuCheckboxRow,
  RadioGroup: DropdownMenu.RadioGroup,
  RadioItem: MenuRadioRow,
  Label: MenuLabel,
  Separator: MenuSeparator,
  Sub: DropdownMenu.Sub,
  SubTrigger: MenuSubRow,
  SubContent: MenuSubContent,
  Acts: MenuActRows,
};
