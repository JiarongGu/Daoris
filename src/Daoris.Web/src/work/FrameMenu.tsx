import type { ReactNode } from 'react';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { Icon, type IconName } from '../ui';
import { cn } from '../lib/cn';

/** One destination inside a frame — a view in Manage, a surface in Work. */
export interface FrameItem {
  id: string;
  label: string;
  icon?: IconName;
  /** A count worth showing beside it. Zero wears nothing: a zero badge is furniture. */
  badge?: number;
  /** Separated from what came before — the shape VS Code uses for "and also". */
  separated?: boolean;
}

/**
 * A frame, and everything inside it, as one title-bar menu.
 *
 * @remarks
 * **Taken from VS Code's menu bar** (owner, 2026-09-22: *"instead manage/work we also need some
 * better design for those — a menu with sub views"*). It replaces a two-button segmented toggle
 * that could say only which frame you were in.
 *
 * 🔴 **What the toggle cost, and this returns.** Reaching a view from the other frame was two moves
 * — switch frame, then find the icon — and the icon rail has carried no labels since SURF10, which
 * that item named the palette as repaying. This pays it a second way and at the point of use: the
 * destinations are **named**, they carry their own counts, and any of them is one click from either
 * frame. The rail keeps its job for the frame you are in; this is for the one you are not.
 *
 * **Both frames stay frames** (D55). The menu is a way in, not a nav item — choosing an item says
 * *this frame, this view*, so the frame boundary is still the thing being crossed rather than being
 * flattened into a list of seven peers.
 *
 * **Presentational, and it imports no hook** (components §3): open state, the current item, a badge,
 * an unavailable frame — every one reachable by passing props.
 */
export function FrameMenu({
  label, items, current, active, badge = 0, onChoose, trigger,
}: {
  label: string;
  items: FrameItem[];
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
          'inline-flex items-center gap-1.5 rounded-control px-2.5 py-1 text-small',
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
        <Icon name="chevronDown" size={11} className="opacity-60" aria-hidden />
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
                onSelect={() => onChoose(trigger, item.id)}
                className={cn(
                  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small outline-none',
                  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
                  active && current === item.id ? 'text-ink' : 'text-ink-soft',
                )}
              >
                {/* The check column is always reserved, so labels line up whether or not anything
                    is current — a list that shifts by 16px when you change view reads as two lists. */}
                <span className="flex w-3.5 shrink-0 justify-center">
                  {active && current === item.id && <Icon name="check" size={12} aria-hidden />}
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
export function FrameMenus({ children }: { children: ReactNode }) {
  return <nav className="flex items-center gap-0.5">{children}</nav>;
}
