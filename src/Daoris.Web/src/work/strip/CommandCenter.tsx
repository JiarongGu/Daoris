import { Icon } from '../../design/ui';
import { cn } from '../../lib/cn';

/**
 * The command center: the strip's middle, and the way into everything the application can do.
 *
 * @remarks
 * **Taken from the shape VS Code settled on.** A wide, centred, obviously-pressable target with its
 * shortcut printed on it makes the palette findable and fills the strip's middle with information.
 *
 * **What it says is where you are**: the workspace and what is attended, so the window answers "which of
 * these am I looking at" without reading the page.
 *
 * **Presentational, and it imports no hook**: every state — a scope, no scope, a
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
      // reading as a control and starts reading as a bar. The strip lays it out: it is
      // centred on the strip while there is room, and narrows rather than covering the menus when
      // there is not. It is a container so what it holds can give way in order as it narrows.
      className={cn(
        // Below 8rem it is its glyph alone, centred: the seven menus keep their room, and a scope cut
        // to a few letters said nothing the status bar does not.
        '@container flex w-full max-w-md items-center gap-2 overflow-hidden rounded-md px-2.5',
        // A target of 28 px at least, the platform language's floor, where `h-6` would draw it 24 px tall. A
        // minimum, not a height, as the menu bar's names have: the strip (`h-9`, 36 px) keeps its height and
        // centres it. Its width's floor is the same 28 px, its glyph alone, where `min-w-0` would let it narrow below that.
        'min-h-7 min-w-7',
        'border border-line bg-sunken text-small text-ink-soft',
        'transition-colors hover:border-line-strong hover:bg-raised hover:text-ink',
        'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
      )}
    >
      {/* Centred once it is alone: the button is the container its children query, so the glyph centres itself. */}
      <Icon name="search" size={12} className="shrink-0 text-ink-faint @max-[8rem]:mx-auto" />

      {/* The scope is the stable half: the detail yields first, because "which circle" survives a
          narrow window better than "which session". A shrink a hundredth of the detail's is what
          makes that the order, and the scope then ends in an ellipsis rather than a cut: at a 500px
          window a cut would read `defau`, clipped mid-letter. */}
      <span className="min-w-0 shrink-[0.01] truncate font-medium @max-[8rem]:hidden">{scope}</span>
      {detail && (
        <>
          <span aria-hidden className="shrink-0 text-ink-faint @max-[8rem]:hidden">·</span>
          <span className="min-w-0 truncate @max-[8rem]:hidden">{detail}</span>
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
