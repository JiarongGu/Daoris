import type { ReactNode } from 'react';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { Icon, type IconName } from '../ui';
import { cn } from '../lib/cn';

/** One destination inside a frame — a view in Manage, a surface in Work. */
export interface MenuItem {
  id: string;
  label: string;
  icon?: IconName;
  /** A count worth showing beside it. Zero wears nothing: a zero badge is furniture. */
  badge?: number;
  /** Separated from what came before — the shape VS Code uses for "and also". */
  separated?: boolean;
  /** Ticked whatever is on screen: the workspace the window is scoped to (D75). */
  checked?: boolean;
  /** Said, and not choosable: "no workspace yet" is a fact in a menu, not an act. */
  disabled?: boolean;
}

/**
 * One menu in the application's menu bar — the title bar's left, as every IDE has it.
 *
 * @remarks
 * **What it is for** (owner, 2026-09-22: *"the manage/work menu should follow the vscode or other
 * IDE design so a lot setting config can be there, and this is not manage/work menu"*). A title-bar
 * menu bar holds what the **application** can do and be configured to do — settings, the machine's
 * wiring, the accounts it runs as, help. It is not where you switch what you are looking at: that
 * went to the activity rail, which is what a rail is for.
 *
 * 🔴 **This replaced its own predecessor twice, and the second correction is the instructive one.**
 * It began as a segmented Manage/Work toggle, became menus *of the frames*, and is now the
 * application's menus — because "a menu with sub-views" and "the frame switcher" were two different
 * asks, and building the first as the second kept navigation in the one place an IDE reserves for
 * configuration.
 *
 * **Presentational, and it imports no hook** (components §3): open state, the current item, a badge
 * — every one reachable by passing props.
 */
export function AppMenu({
  label, items, current, active, badge = 0, onChoose, trigger,
}: {
  label: string;
  items: MenuItem[];
  /** The item showing now, when this frame is the one on screen. */
  current?: string;
  /** Whether this frame is the one on screen — what the trigger shows as selected. */
  active: boolean;
  /** Attention riding on the frame itself, not on one of its items. */
  badge?: number;
  onChoose: (frame: string, item: string) => void;
  /** Its own name, for the callback — the menu does not know which frame it is otherwise. */
  trigger: string;
}) {
  // 🔴 `modal={false}`. A modal menu makes the rest of the page inert — Radix puts
  // `pointer-events: none` on the body while it is open — and a menu in the TITLE BAR has no
  // business doing that: VS Code's do not, and the window still has to be draggable underneath it.
  // It also locks scroll, which shifts the layout by the scrollbar's width on every open. Found by
  // two tests that could not click anything after an earlier test left a menu open.
  return (
    <Menu.Root modal={false}>
      <Menu.Trigger
        className={cn(
          // 🔴 No chevron, and tight. Measured against a real VS Code window: its menu bar is plain
          // words at small gaps — File Edit Selection View Go Run Terminal Help — and carries no
          // disclosure arrows at all. A menu bar is a convention strong enough not to need marking,
          // and eight chevrons in a title bar is eight pieces of furniture.
          'inline-flex items-center gap-1.5 rounded-control px-2 py-1 text-small',
          'transition-colors duration-(--speed)',
          'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
          active ? 'bg-accent text-accent-ink' : 'text-ink-soft hover:bg-raised hover:text-ink',
        )}
      >
        {label}
        {badge > 0 && (
          <span className="rounded-full border border-st-open bg-st-open/15 px-1.5 font-mono text-meta tabular-nums text-st-open">
            {badge}
          </span>
        )}
      </Menu.Trigger>

      <Menu.Portal>
        <Menu.Content
          align="start"
          sideOffset={4}
          className="z-30 min-w-56 rounded-control border border-line bg-overlay p-1 shadow-lg"
        >
          {items.map((item) => (
            <div key={item.id}>
              {item.separated && <Menu.Separator className="my-1 h-px bg-line" />}
              <Menu.Item
                disabled={item.disabled}
                onSelect={() => onChoose(trigger, item.id)}
                className={cn(
                  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small outline-none',
                  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
                  'data-[disabled]:cursor-default data-[disabled]:text-ink-faint',
                  (active && current === item.id) || item.checked ? 'text-ink' : 'text-ink-soft',
                )}
              >
                {/* The check column is always reserved, so labels line up whether or not anything
                    is current — a list that shifts by 16px when you change view reads as two lists. */}
                <span className="flex w-3.5 shrink-0 justify-center">
                  {((active && current === item.id) || item.checked) && <Icon name="check" size={12} aria-hidden />}
                </span>
                {item.icon && <Icon name={item.icon} size={13} className="shrink-0 opacity-70" aria-hidden />}
                <span className="truncate">{item.label}</span>
                {item.badge !== undefined && item.badge > 0 && (
                  <span className="ml-auto shrink-0 font-mono text-meta tabular-nums text-ink-faint">
                    {item.badge}
                  </span>
                )}
              </Menu.Item>
            </div>
          ))}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}

/** The two frames, side by side — what the app strip shows where the toggle used to be. */
export function AppMenuBar({ children }: { children: ReactNode }) {
  return <nav className="flex items-center gap-0.5">{children}</nav>;
}
