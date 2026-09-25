import { Icon } from '../ui';
import { cn } from '../lib/cn';

/**
 * The command center: the strip's middle, and the way into everything the application can do.
 *
 * @remarks
 * **Taken from the shape VS Code settled on** (owner, 2026-09-22: *"the application topbar you can
 * take more example from application like vscode"*), and it answers two measured problems in the
 * strip it joins.
 *
 * 🔴 **The strip had ~1,400px of nothing in it.** Wordmark and mode switch sat left, a lone 14px
 * search glyph sat right against the caption buttons, and the whole middle was empty at any real
 * window width. A title bar that carries no information is a title bar paying rent for a wordmark.
 *
 * 🔴 **The palette was a glyph.** SURF10 replaced a labelled sidebar with a 48px icon rail and
 * named the palette as where that discoverability would be paid back — then shipped the palette as
 * an unlabelled icon in the corner, which pays nothing back. A wide, centred, obviously-pressable
 * target with its shortcut printed on it is the same affordance made findable.
 *
 * **What it says is where you are**, because that is the other half of what the space was for: the
 * strip never named the workspace or what was attended, so the window could not answer "which of
 * these am I looking at" without reading the page.
 *
 * **Presentational, and it imports no hook** (components §3): every state — a scope, no scope, a
 * long name, a browser with no shortcut to offer — is reachable by passing props, which is what
 * makes the story and the test able to reach them.
 */
export function CommandCenter({ scope, detail, shortcut, onOpen, label }: {
  /** Where you are: the workspace, or the deployment's name when it holds one circle. */
  scope: string;
  /** What is attended, when anything is — the second line VS Code gives to the file name. */
  detail?: string;
  /** The keyboard way in, printed so it is learnable. Absent where there is none to offer. */
  shortcut?: string;
  onOpen: () => void;
  label: string;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onOpen}
      // The width is bounded rather than fluid — a centred control that grows with the window stops
      // reading as a control and starts reading as a bar. The strip lays it out (UX5 U15): it is
      // centred on the strip while there is room, and narrows rather than covering the menus when
      // there is not. It is a container so what it holds can give way in order as it narrows.
      className={cn(
        '@container flex h-6 w-full min-w-0 max-w-md items-center gap-2 overflow-hidden rounded-md px-2.5',
        'border border-line bg-sunken text-small text-ink-soft',
        'transition-colors hover:border-line-strong hover:bg-raised hover:text-ink',
        'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
      )}
    >
      <Icon name="search" size={12} className="shrink-0 text-ink-faint" />

      {/* The scope is the stable half: the detail yields first, because "which circle" survives a
          narrow window better than "which session". A shrink a hundredth of the detail's is what
          makes that the order, and the scope then ends in an ellipsis rather than a cut: at a 500px
          window it read `defau`, clipped mid-letter (UX5 U15). */}
      <span className="min-w-0 shrink-[0.01] truncate font-medium">{scope}</span>
      {detail && (
        <>
          <span aria-hidden className="shrink-0 text-ink-faint">·</span>
          <span className="min-w-0 truncate">{detail}</span>
        </>
      )}

      {/* The shortcut goes first of all, since the pill still opens the palette without it. */}
      {shortcut && (
        <kbd className="ml-auto shrink-0 rounded border border-line px-1 font-mono text-meta text-ink-faint @max-[12rem]:hidden">
          {shortcut}
        </kbd>
      )}
    </button>
  );
}
