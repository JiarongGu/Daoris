import { type KeyboardEvent, type ReactNode, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { Button, Inline } from '../ui';

/**
 * How an act pressed in a question hears how it ended (ACCTEDIT1, D152): `done` once it landed, and `refused` with the
 * sentence the person reads, said in the question where it was pressed. The one contract every inline ask keeps (UXFIX2).
 */
export type Answered = { done: () => void; refused: (sentence: string) => void };

/**
 * Why an act was refused, in the sentence the person reads (ACCTEDIT1): said whole, where it was pressed, as a refused start
 * is said in its form (UX5 U68). A toast is for an error with no form to stand in (the platform language §4).
 *
 * @remarks
 * It shrinks with its column and breaks anywhere (UXFIX2c): a refusal carries what nothing breaks, a long id or a raw error,
 * which otherwise widened its flex or grid item past the main area's 400 px floor. Its own line holds it, so breaking inside
 * a word cannot squeeze it to a character a line.
 */
export function Refused({ sentence, id, className }: { sentence: string; id?: string; className?: string }) {
  return (
    <p
      id={id}
      role="alert"
      className={cn(
        'm-0 min-w-0 basis-full whitespace-pre-wrap wrap-anywhere border-l-[3px] border-warn pl-2.5 text-small text-ink-soft',
        className,
      )}
    >
      <Inline text={sentence} />
    </p>
  );
}

/** Where the focus was when an ask opened: the element, its name, and what held it, nearest first. */
type Opener = { node: HTMLElement; label: string; around: HTMLElement[] };

const labelOf = (node: Element) => (node.getAttribute('aria-label') ?? node.textContent ?? '').trim();

function noted(node: Element | null): Opener | null {
  if (!(node instanceof HTMLElement) || node === node.ownerDocument.body) return null;
  const around: HTMLElement[] = [];
  for (let at = node.parentElement; at && at !== node.ownerDocument.body; at = at.parentElement) around.push(at);
  return { node, label: labelOf(node), around };
}

/** A surface that gives the focus back by itself as it closes: a menu, or a box over the page (the palette). */
const CLOSES_ITSELF = '[role="menu"], [role="dialog"]';

/**
 * Where an ask opened from a menu's item came from: the menu's trigger, which names the menu (`aria-labelledby`); the
 * item itself where the menu names itself (the right-click's), until the menu gives the focus back.
 */
function openedBy(node: Element | null): Opener | null {
  const menu = node?.closest('[role="menu"]');
  const trigger = menu?.getAttribute('aria-labelledby');
  const named = trigger ? node!.ownerDocument.getElementById(trigger) : null;
  return noted(named ?? node);
}

/** A list: once the opener's own item went with its record, a press of the same name in it is a neighbour's. */
const LIST = 'ul, ol, [role="list"], [role="listbox"]';

/** What a page holds its records in: where the focus goes once no press is drawn again (UXFIX2c). */
const HOLDS = `section, [role="region"], article, main, [role="main"], [role="tabpanel"], ${LIST}`;

/**
 * The opener, or the press drawn again in its place: a first press is not offered twice while its ask is open (*Delete…*,
 * *Discard branch…*, *Clear history…*), so a cancel or a landed act draws it anew, and the nearest of its old surroundings
 * still on the page holds the new one by its name.
 */
function drawnAgain(opener: Opener): HTMLElement | null {
  if (opener.node.isConnected) return opener.node;
  const near = opener.around.find((at) => at.isConnected);
  if (!near || near.matches(LIST)) return null;
  return [...near.querySelectorAll<HTMLElement>(opener.node.tagName)]
    .find((each) => labelOf(each) === opener.label && !each.hasAttribute('disabled')) ?? null;
}

/** A holder drawn again in place of one gone: the one element in what still holds it of the same kind and name. */
function likeOf(gone: HTMLElement, near: HTMLElement): HTMLElement | null {
  const same = [...near.querySelectorAll<HTMLElement>(gone.tagName)].filter((each) =>
    each.getAttribute('role') === gone.getAttribute('role') && each.getAttribute('aria-label') === gone.getAttribute('aria-label'));
  return same.length === 1 ? same[0]! : null;
}

/**
 * The nearest section or list the opener sat in that is still drawn (UXFIX2c): what still holds it, where that is a list or
 * a section; else a holder that went and was drawn again in its place (a page's main area, drawn anew for *nothing chosen*
 * once its record went); else a holder further up; else whatever still held it.
 */
function heldIn(opener: Opener): HTMLElement | null {
  const at = opener.around.findIndex((each) => each.isConnected);
  if (at < 0) return null;
  const near = opener.around[at]!;
  if (near.matches(HOLDS)) return near;
  for (const gone of opener.around.slice(0, at)) {
    const like = gone.matches(HOLDS) ? likeOf(gone, near) : null;
    if (like) return like;
  }
  return opener.around.slice(at).find((each) => each.matches(HOLDS)) ?? near;
}

/**
 * Where the focus goes as an ask is put down (UXFIX2c): the press drawn again; where none is (a delete took its record, or
 * nothing more is offered), the nearest section or list that held it. Never the page's body, where a keyboard starts again
 * from the top.
 */
function giveBack(opener: Opener) {
  const again = drawnAgain(opener);
  again?.focus();
  if (again && again === document.activeElement) return;
  const held = heldIn(opener);
  if (!held) return;
  if (!held.hasAttribute('tabindex')) {
    // Focusable while it holds the focus, and no longer once the focus leaves, so a click inside it later lands as before.
    held.setAttribute('tabindex', '-1');
    held.addEventListener('blur', () => held.removeAttribute('tabindex'), { once: true });
  }
  held.focus({ preventScroll: true });
}

/**
 * **The one inline confirmation for a destructive act** (UXFIX2; the second-opinion review's cross-cutting note; the
 * platform language §4, a destructive edit asks once): a first press opens it in the page, never a modal, saying what the
 * second press does; then the move, in danger's hue, and *Never mind*, the move first as every confirming pair is (U38).
 *
 * @remarks
 * **A molecule**: what it says arrives as props and the press goes out with how to answer it; it holds only its own wait,
 * the refusal said in it and where the focus came from.
 *
 * - **The explanation takes the focus on opening and describes the move** (`aria-describedby`), so a keyboard reaches the
 *   move having heard what goes. A first press made from a menu leaves with its menu, which gives the focus back to its
 *   trigger a beat later; the ask waits that beat and takes the trigger as what opened it.
 * - **It opens whole in view** (UXFIX2d): the ask is scrolled into view, nearest edge first, before the explanation takes
 *   the focus without scrolling again, so an ask opened near the page's foot shows its presses, not one line.
 * - **Escape and *Never mind* put it down** and give the focus back to what opened it, or to the same press drawn again
 *   where it was not offered twice while the ask was open.
 * - **The press keeps it open and waiting** until the act answers (ACCTEDIT1's `done`/`refused`): `done` closes it, and
 *   `refused` says the sentence inside it, whole (`role="alert"`), and lets the move be pressed again. *Never mind* and
 *   Escape wait with the move, so a refusal is never said to nobody. An answer to an ask put down changes nothing; a `done`
 *   still closes an ask the page drew away while it waited, so the page's own hold on it lets go.
 * - **While it waits it says so** (UXFIX2c): a status beside the presses, drawn from the first so a reader hears it speak
 *   (`role="status"`, polite), and nothing around it busy, since a reader may hold back what a busy element says until the
 *   wait is over and the status quiet again; the waiting presses and the status are the wait's signs. The move keeps its
 *   name; the page's own `busy` is another act, so it says nothing.
 * - **Landed, the focus goes where a cancel's does** (UXFIX2c): the press drawn again, and where none is, since a delete took
 *   its record or nothing more is offered, the nearest section or list that held it, never the page's body.
 * - **A rich body** (a clear's *What goes* and *What stays*) is its explanation too: `block` lays it out as rows.
 * - **The move wears danger's hue**, since it is a destructive edit's; an outward-facing act that destroys nothing (a
 *   landing, LAND4) asks the same way with the primary's (`tone`), so the hue never says something goes when nothing does.
 */
export function InlineConfirm({
  label, says, meanIt, block = false, busy = false, ready = true, refused = null, children, onConfirm, onClose, className,
  tone = 'danger',
}: {
  /** The move's hue: danger's for a destructive edit, the default; the primary's for an act that destroys nothing. */
  tone?: 'danger' | 'primary';
  /** The ask's name, as a reader hears the group: *delete this quest*. */
  label: string;
  /** What the second press does: a sentence, or a body of rows. It takes the focus on opening and describes the move. */
  says: ReactNode;
  /** The move's name: *Delete quest*. Absent, nothing can be confirmed (a clear with nothing to take): only *Close*. */
  meanIt?: string;
  /** Lay it out as rows, for a body longer than a sentence; one line otherwise. */
  block?: boolean;
  /** Another act on the record is on its way: both presses wait for it. */
  busy?: boolean;
  /** What the move needs first is given (a reason typed); the move alone waits for it. */
  ready?: boolean;
  /** A refusal it opens saying, as a story draws one; after that, the refusals are its own act's. */
  refused?: string | null;
  /** A field the move needs, between the explanation and the presses. */
  children?: ReactNode;
  /** The second press, and how to tell the ask how it ended. */
  onConfirm?: (answered: Answered) => void;
  /** Put down: by *Never mind*, Escape or *Close*, or once the act landed. */
  onClose: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  const saysId = useId();
  const refusalId = useId();
  const own = useRef<HTMLDivElement>(null);
  const told = useRef<HTMLDivElement>(null);
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(refused);
  // Read while it is first drawn, before the commit that draws it can take the press away (a press not offered twice, a
  // menu's item).
  const [first] = useState(() => {
    const now = typeof document === 'undefined' ? null : document.activeElement;
    return { opener: openedBy(now), closing: Boolean(now?.closest(CLOSES_ITSELF)) };
  });
  const opener = useRef(first.opener);
  const alive = useRef(false);
  const turn = useRef(0);
  // Put down by the person or by its act landing, rather than drawn away by the page: only then is the focus its to give.
  const leaving = useRef(false);
  const waiting = busy || pending;

  // The whole ask comes into view, then the explanation takes the focus without scrolling again (UXFIX2d): a focus alone
  // scrolled only the explanation's line into view, and an ask opened near the page's foot left its list and presses below
  // the fold. `nearest` moves the page least: an ask already in view stays put, and one taller than the view shows its top.
  const show = () => {
    own.current?.scrollIntoView?.({ block: 'nearest' });
    told.current?.focus({ preventScroll: true });
  };

  // Pressed in a menu, the ask waits for the menu to close and give the focus back, which it does a beat after it goes, and
  // only where nothing took the focus while it was open: taking it at once would leave the focus nowhere to return to.
  // Whatever the menu gave it to is what opened the ask.
  useEffect(() => {
    if (!first.closing) {
      show();
      return undefined;
    }
    let after = 0;
    const gone = window.setTimeout(() => {
      after = window.setTimeout(() => {
        const now = document.activeElement;
        if (now && now !== document.body && !own.current?.contains(now) && !now.closest(CLOSES_ITSELF)) opener.current = noted(now);
        show();
      }, 0);
    }, 0);
    return () => {
      window.clearTimeout(gone);
      window.clearTimeout(after);
    };
  }, [first]);

  // Put down, it gives the focus back where its closing dropped it; a focus the person moved elsewhere stays there.
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      const from = opener.current;
      if (!leaving.current || !from) return;
      const now = document.activeElement;
      if (now && now !== document.body && now.isConnected) return;
      giveBack(from);
    };
  }, []);

  const cancel = () => {
    // An answer still on its way to a press before this one is no longer the person's question.
    turn.current += 1;
    leaving.current = true;
    onClose();
  };

  const confirm = () => {
    if (!onConfirm) return;
    const mine = ++turn.current;
    // The move waits disabled, which would drop the focus to the page: it goes to what the move is about instead.
    if (own.current?.contains(document.activeElement)) told.current?.focus({ preventScroll: true });
    setRefusal(null);
    setPending(true);
    onConfirm({
      // Landed, it closes, even where the page drew it away meanwhile, so what holds it open lets it go.
      done: () => {
        if (mine !== turn.current) return;
        if (alive.current) {
          leaving.current = true;
          setPending(false);
        }
        onClose();
      },
      // Refused where nobody is asking any more, there is no one to say it to.
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    // A reason typed through an input method drops its composition on Escape, and the ask stays (IME1).
    if (isComposing(event) || event.key !== 'Escape') return;
    // Its own Escape: nothing behind it closes on the same key.
    event.stopPropagation();
    event.preventDefault();
    if (!waiting) cancel();
  };

  const presses = (
    <>
      {meanIt && (
        <Button
          variant={tone}
          disabled={waiting || !ready}
          aria-describedby={refusal ? `${saysId} ${refusalId}` : saysId}
          onClick={confirm}
        >
          {meanIt}
        </Button>
      )}
      <Button variant="ghost" disabled={meanIt ? waiting : false} onClick={cancel}>
        {t(meanIt ? 'common.cancel' : 'common.close')}
      </Button>
      {/* Drawn from the first, empty and out of the row, so it is a live region before it speaks (UXFIX2c). */}
      <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
        {pending ? t('work.confirm.pending') : null}
      </span>
    </>
  );

  return (
    <div
      ref={own}
      role="group"
      aria-label={label}
      onKeyDown={onKeyDown}
      className={cn(
        'rounded-control border border-line bg-sunken',
        block ? 'grid gap-3 px-3 py-2.5' : 'flex flex-wrap items-center gap-2 px-2.5 py-2',
        className,
      )}
    >
      <div
        ref={told}
        id={saysId}
        tabIndex={-1}
        className={block ? 'grid min-w-0 gap-3' : 'min-w-0 flex-1 basis-64 text-small text-ink-soft'}
      >
        {says}
      </div>
      {children}
      {block ? <div className="flex flex-wrap gap-2">{presses}</div> : presses}
      {refusal && <Refused id={refusalId} sentence={refusal} />}
    </div>
  );
}
