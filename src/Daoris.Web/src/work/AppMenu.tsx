import type { ReactNode } from 'react';
import { Icon, type IconName, Menu } from '../ui';
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
  /** Its key, shown at the right as VS Code's menus show one (DOCK1c). */
  shortcut?: string;
}

/**
 * One menu in the application's menu bar — the title bar's left, as every IDE has it.
 *
 * @remarks
 * **What it is for**, as an IDE's is. A title-bar
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
 * **Presentational, and it imports no hook** (components §3): an item's check and its count are
 * props on the item. The frame-era props — a current item, a selected trigger, a badge on the menu
 * itself — went with the frames (REV3 CLEAN1: nothing passed them).
 */
export function AppMenu({
  label, items, onChoose, trigger,
}: {
  label: string;
  items: MenuItem[];
  onChoose: (frame: string, item: string) => void;
  /** Its own name, for the callback — the menu does not know which frame it is otherwise. */
  trigger: string;
}) {
  // Never modal, as no menu is: the window stays draggable under a menu in the title bar (`Menu.Root`).
  return (
    <Menu.Root>
      <Menu.Trigger
        className={cn(
          // 🔴 No chevron, and tight. Measured against a real VS Code window: its menu bar is plain
          // words at small gaps — File Edit Selection View Go Run Terminal Help — and carries no
          // disclosure arrows at all. A menu bar is a convention strong enough not to need marking,
          // and eight chevrons in a title bar is eight pieces of furniture.
          'inline-flex items-center gap-1.5 rounded-control px-2 py-1 text-small',
          'transition-colors duration-(--speed)',
          'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
          'text-ink-soft hover:bg-raised hover:text-ink',
        )}
      >
        {label}
      </Menu.Trigger>

      <Menu.Content align="start" className="min-w-56">
        {items.map((item) => (
          <div key={item.id}>
            {item.separated && <Menu.Separator />}
            {/* The tick column is always reserved, so the labels line up whether or not anything is ticked. */}
            <Menu.Item tick={Boolean(item.checked)} disabled={item.disabled} onSelect={() => onChoose(trigger, item.id)}>
              {item.icon && <Icon name={item.icon} size={13} className="shrink-0 opacity-70" />}
              <span className="truncate">{item.label}</span>
              {item.badge !== undefined && item.badge > 0 && (
                <span className="ml-auto shrink-0 font-mono text-meta tabular-nums text-ink-faint">
                  {item.badge}
                </span>
              )}
              {item.shortcut && (
                <kbd className="ml-auto shrink-0 pl-4 font-mono text-meta text-ink-faint">{item.shortcut}</kbd>
              )}
            </Menu.Item>
          </div>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}

/** The two frames, side by side — what the app strip shows where the toggle used to be. */
export function AppMenuBar({ children }: { children: ReactNode }) {
  return <nav className="flex items-center gap-0.5">{children}</nav>;
}
