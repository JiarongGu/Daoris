import { type ReactNode } from 'react';
import { cn } from '../../lib/cn';

/* ---------------------------------------------------------------- cards, tiles, headers */

export function Card({ id, warn, accent, className, children }: {
  /** What a link or a menu item brings into view (UX5 U72). */
  id?: string;
  warn?: boolean; accent?: boolean; className?: string; children: ReactNode;
}) {
  return (
    <article id={id} className={cn(
      'rounded-card border border-line border-l-[3px] bg-raised px-[1.15rem] py-4',
      accent && 'border-l-accent',
      warn && 'border-l-st-open',
      className,
    )}
    >
      {children}
    </article>
  );
}

/**
 * A domain's cards, 16 px apart (the UI/UX design R1, UIFIT1): the spacing is the container's, set once, and a card
 * sets no outer margin of its own (`tokens.test.ts`). The `mt-3.5` each card wore was 14 px, off the scale's step, and
 * two cards that forgot it touched.
 */
export function CardStack({ className, children }: { className?: string; children: ReactNode }) {
  return <div data-card-stack="" className={cn('grid min-w-0 gap-4', className)}>{children}</div>;
}

/**
 * Something waiting on the person — a parked session, a parked intake, a folder to trust. The one
 * frame each such card wears: the waiting tone on its left edge and on its heading. Four cards
 * wrote it out (REV3 CLEAN1), so a change of that tone is now one change.
 */
export function WaitingCard({ title, children }: { title: ReactNode; children?: ReactNode }) {
  return (
    <section className="rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold text-ink-open">{title}</h3>
      {children}
    </section>
  );
}

export function CardHeader({ title, aside }: { title: ReactNode; aside?: ReactNode }) {
  return (
    <header className="flex items-baseline justify-between gap-4">
      <span className="text-body font-semibold">{title}</span>
      {aside}
    </header>
  );
}

/**
 * The stat-tile contract: label · value · context. Values wear proportional figures, deliberately. A value not known yet is null:
 * static placeholders on the value's and the context's lines, never a zero (PUSHUX1).
 */
export function Tile({ label, value, note, warn }: {
  label: string; value: ReactNode | null; note: string; warn?: boolean;
}) {
  const known = value !== null;
  return (
    <div
      aria-busy={!known || undefined}
      className={cn(
        'grid content-start gap-0.5 rounded-card border border-line bg-raised px-4 pb-3.5 pt-3',
        warn && known && 'border-l-[3px] border-l-st-open',
      )}
    >
      <span className="text-small text-ink-soft">{label}</span>
      {known
        ? <span className="text-value font-semibold leading-[1.15] tracking-[-0.01em]">{value}</span>
        : <i aria-hidden className="my-1 block h-5 w-12 rounded-[4px] bg-accent-soft opacity-55" />}
      {known
        ? <span className={cn('text-small', warn ? 'text-ink-open' : 'text-ink-faint')}>{note}</span>
        : <i aria-hidden className="my-1 block h-3 w-3/4 rounded-[4px] bg-accent-soft opacity-55" />}
    </div>
  );
}

/** Every view opens with one of these: the title, one line of purpose, one primary action. */
export function PageHeader({ title, description, action }: {
  title: string; description: string; action?: ReactNode;
}) {
  return (
    <header className="mb-5 flex items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="text-view font-[650] tracking-[-0.01em]">{title}</h1>
        <Prose className="mt-1">{description}</Prose>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </header>
  );
}

/**
 * Explanatory text: the console explaining itself, in the secondary ink.
 *
 * @remarks
 * **It wraps at the column's edge, as every block of a page does** (D141, LAYOUT11). It carried
 * Tailwind's prose width, 65ch, from UX5's first pass, when a paragraph in a column nothing stood
 * beside ran to about 190 characters a line. A measure per paragraph gave every page two edges: on
 * the install a quest's title ran the pane while its body stopped at 456 px, and in 中文 the 65
 * Latin digits held about 32 glyphs. The page's column (`ViewMain`) is the one place a line's length
 * is set, and it sets none; the person sizes the pane with the list's edge and the side bar.
 *
 * A quest's body, a knowledge entry and a session's note are content, shown as they are, and they
 * wrap at the same edge.
 */
export function Prose({ className, children }: { className?: string; children: ReactNode }) {
  return <p className={cn('m-0 text-body text-ink-soft', className)}>{children}</p>;
}

/**
 * A sentence whose backticked spans are code: the one markup the catalogues and the service share.
 *
 * @remarks
 * **Nothing rendered it before this, and the installed window said so.** Fourteen catalogue strings
 * and a good many of the service's and the driver's sentences mark a command or a name the way a
 * commit message does, and every one reached the screen with its backticks, in both languages.
 *
 * **The words are unchanged**, so a service's sentence is still verbatim: only the pairs are read,
 * a lone backtick stays the character it is, and nothing else is parsed. A setting's hint, a tip and
 * a toast take this on their own, because each is always a sentence. Anywhere else a caller asks for
 * it, since content is shown as it is.
 *
 * **The marks a sentence goes on with right after a span are drawn by the span** (UXFIX4b), kept on its last word's line.
 */
export function Inline({ text }: { text: string }) {
  const parts = text.split(/`([^`\n]+)`/);
  return (
    <>
      {parts.map((part, i) => {
        // Mono at the sentence's own size and colour, so a command reads as one without shouting.
        if (i % 2 === 1) return <CodeText key={i} text={part} trail={CLOSING.exec(parts[i + 1]!)?.[0]} />;
        // The marks the span before this part drew are not drawn twice.
        return i > 0 ? part.replace(CLOSING, '') : part;
      })}
    </>
  );
}

/**
 * The marks that close what comes before them, which a line never begins with: a closing bracket or quote, and a stop, in
 * either language (UXFIX4b).
 */
const CLOSING = /^[\p{Pe}\p{Pf},.;:!?，。、；：！？]+/u;

/** One word of a code span: moved whole to the next line, and broken inside only when it alone is wider than the line. */
const CODE_WORD = 'inline-block max-w-full wrap-break-word';

/** A group the line never breaks inside: around the last word of a span of several, so the marks after it stay on its line. */
const HELD = 'whitespace-nowrap';

/**
 * A command or a name set as code, which breaks only between its words: every code span the console shows, a
 * setting's terminal twin among them.
 *
 * @remarks
 * **A browser may break a line after any hyphen**, so on the installed window a setting's terminal twin read
 * `--no-` over `keep`, and `--` over `workspace` (LOOK5): in 中文, where a sentence fills its line to the edge, the
 * break fell inside a flag. Each word is a box of its own, as wide as the word and never wider than the line, so it
 * moves to the next line whole, and breaks inside only when that one word is wider than the line (U8). The spaces
 * between the words stay where a line may break. The text is unchanged, so a copy is still the command.
 *
 * `break-word`, not the `anywhere` a path wears: it leaves a sentence's narrowest width what it was, so a flex row
 * cannot squeeze a command to a character a line, where `anywhere` would let it.
 *
 * A span of one word is that word's box itself, and a span of several holds one box per word.
 *
 * **The marks after a span stay on its last word's line** (UXFIX4b): at the branch list's 400 px floor a long name filled
 * its line and the comma after it opened the next, `, whose work…`, since Chromium may break after any inline box, even
 * before a comma. A span of one word followed by marks is a box holding the code and the marks, and inside it the text's
 * own rules hold, which never break before a closing mark in either language. A span of several cannot hold them, since
 * its last box is inside the code and the marks are not code: that box is alone in a group that does not wrap, which
 * leaves the break after it to the same rules, and it breaks inside at its line less an em a mark, so a word filling its
 * line leaves them room rather than pushing them past the edge. A box whose word breaks inside is as wide as its line,
 * so there the marks stand at its edge. A span no mark follows is drawn as it was.
 */
export function CodeText({ text, className, trail }: {
  text: string;
  className?: string;
  /** The closing marks the sentence goes on with right after the span, drawn by it. */
  trail?: string;
}) {
  const words = text.split(/(\s+)/).filter(Boolean);
  if (words.length < 2) {
    if (!trail) return <code className={cn('font-mono', CODE_WORD, className)}>{text}</code>;
    return <span className={CODE_WORD}><code className={cn('font-mono', className)}>{text}</code>{trail}</span>;
  }
  const last = words.reduce((found, word, i) => (/^\s+$/.test(word) ? found : i), -1);
  return (
    <>
      <code className={cn('font-mono', className)}>
        {words.map((word, i) => {
          if (/^\s+$/.test(word)) return word;
          if (!trail || i !== last) return <span key={i} className={CODE_WORD}>{word}</span>;
          return (
            <span key={i} className={HELD}>
              <span className={cn(CODE_WORD, 'whitespace-normal')} style={{ maxWidth: `calc(100% - ${trail.length}em)` }}>{word}</span>
            </span>
          );
        })}
      </code>
      {trail}
    </>
  );
}

/**
 * A section's label. One style, and the heading LEVEL is a prop — because inside the attended
 * session the head is already an `h2`, and a second `h2` under it would flatten the region's
 * outline for the readers who navigate by headings. Everywhere else the default is unchanged.
 */
export function SectionTitle({ level = 2, children }: { level?: 2 | 3; children: ReactNode }) {
  const Heading = level === 3 ? 'h3' : 'h2';
  // `mt-6` separates a section from the one above it — and is dead space when the title IS the first
  // thing in its card, which it usually is. Every card in the console was carrying 24px of it above
  // its own heading, on top of the card's own padding.
  return (
    <Heading className="mb-2 mt-6 text-small font-semibold text-ink-faint first:mt-0">
      {children}
    </Heading>
  );
}
