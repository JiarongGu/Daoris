import { Fragment, type ReactNode, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Tip } from './Tip';

/* ---------------------------------------------------------------- dots, wells, meta lines */

export const DOT_TONE: Record<string, { mark: string; word: string }> = {
  live: { mark: 'bg-accent motion-safe:animate-pulse', word: 'text-accent' },
  // Waiting on a person is the status palette's waiting hue, the one `WaitingCard` and the map wear.
  // It wore declined's red, and a session waiting on its person read as one that had failed (UX5 U1).
  parked: { mark: 'bg-st-open', word: 'text-ink-open' },
  // A failure is an outcome, and red is its hue: a tool call that failed, never a session's liveness.
  failed: { mark: 'bg-st-declined', word: 'text-ink-danger' },
  // Ended is completed, failed, declined and stopped at once, so it wears no outcome's hue: done's
  // green put a success mark beside a failed session in the rail. The pill beside it names the outcome.
  ended: { mark: 'bg-ink-faint', word: 'text-ink-soft' },
  idle: { mark: 'bg-line', word: 'text-ink-faint' },
  // A fact's mark (the UI/UX design §4, MILESTONEUX1): what is reached or kept wears done's hue, the one the status palette's
  // done already is; what waits on another is a neutral, neither the person's open hue nor an outcome's.
  done: { mark: 'bg-st-done', word: 'text-ink-done' },
  waits: { mark: 'bg-ink-soft', word: 'text-ink-soft' },
  // Work under way at a place of the activity bar (BADGEUX1, the UI/UX design §3.10): taken's hue, the status palette's *in progress*,
  // which a working session's pill and a taken quest's wear. Not done's green, an outcome's, and not the accent, which reads as open's
  // amber at badge size beside a wait's number (UX5 U24); still, since it stands for as long as the work runs.
  running: { mark: 'bg-st-taken', word: 'text-ink-taken' },
};

/**
 * A liveness mark and the word for it — one component, because the label is not optional (D41 §6).
 *
 * @remarks
 * The mark is `aria-hidden`: the label already IS the accessible name, and a screen reader that
 * announces the state twice is worse served than one that hears it once. Hue distinguishes the tones
 * at a glance for the people hue works for; the word is what everyone else reads.
 */
export function Dot({ tone = 'idle', label, className }: {
  tone?: keyof typeof DOT_TONE; label: string; className?: string;
}) {
  const shade = DOT_TONE[tone];
  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 whitespace-nowrap text-meta', shade.word, className,
    )}
    >
      <DotMark tone={tone} />
      {label}
    </span>
  );
}

/**
 * The mark alone, for a surface too narrow for the word — the rail's strip (FRAME6). The word goes
 * where the mark's control names itself, so it is never hue alone (D41 §6); anywhere with room for the
 * word, `Dot` is the component.
 */
export function DotMark({ tone = 'idle', className, ...data }: {
  tone?: keyof typeof DOT_TONE; className?: string;
  /** A mark a test or a measure finds it by (`data-running`, BADGEUX1). */
  [attribute: `data-${string}`]: string | undefined;
}) {
  return <span aria-hidden {...data} className={cn('inline-block size-1.5 shrink-0 rounded-full', DOT_TONE[tone].mark, className)} />;
}

/**
 * One item on a list closed to its strip (FRAME6's session strip row, made general by D118 §5): the first
 * character of its name, or the letters it is given, and its mark, one press away. What it is and how it stands are its accessible
 * name and its tip, since the strip has no room for the words and a mark is never hue alone (D41 §6).
 *
 * @remarks
 * A row of its list (`data-list-row`), so the list's arrows move along the strip too (`work/frame/listKeys`).
 * The first character is taken whole, by code point, so a 中文 name shows its first character rather than
 * half of one.
 */
export function StripMark({ label, initialOf, letters, face, tone, dimmed = false, current = false, onPress }: {
  /** Its accessible name and its tip: what it is and how it stands. */
  label: string;
  /** The name whose first character the strip shows. */
  initialOf: string;
  /**
   * A short mark drawn as written in place of that character, its case kept, where neighbours' first characters would be
   * one: an agent's *CC* beside *Cx* (ACCTUX2, the UX7 design §4.6).
   */
  letters?: string;
  /**
   * What it is drawn as instead of its initial, decoration its label names: a plugin's icon (PLUGUI2, D140 §2). The
   * face is drawn faint itself where it is dimmed.
   */
  face?: ReactNode;
  /** Its mark; absent, it wears none, as a plugin that is simply on does (D119). */
  tone?: keyof typeof DOT_TONE;
  /** Its initial drawn faint: a plugin switched off (D119). */
  dimmed?: boolean;
  /** The one chosen in the list, marked as the activity bar marks its current place. */
  current?: boolean;
  onPress?: () => void;
}) {
  return (
    <li data-list-row="">
      <Tip content={label} side="right">
        <button
          type="button"
          aria-label={label}
          aria-current={current || undefined}
          onClick={onPress}
          className={cn(
            'relative flex h-8 w-10 items-center justify-center rounded-control transition-colors duration-(--speed)',
            current ? 'bg-accent-soft text-ink' : 'text-ink-soft hover:bg-accent-soft/50',
          )}
        >
          {/* The same 2px accent rail the activity bar gives its current place. */}
          {current && <span aria-hidden className="absolute inset-y-1 left-0 w-0.5 rounded-full bg-accent" />}
          {face ?? (
            <span aria-hidden className={cn('text-small font-semibold', !letters && 'uppercase', dimmed && 'text-ink-faint')}>
              {letters || (Array.from(initialOf)[0] ?? '?')}
            </span>
          )}
          {tone && <DotMark tone={tone} className="absolute right-1 top-1" />}
        </button>
      </Tip>
    </li>
  );
}

/**
 * A verbatim monospace region with a "what fell out" footer — a session's console, a build log, a
 * diff hunk.
 *
 * @remarks
 * **Never translated**: what a tool said is data, and that is where the platform's i18n boundary
 * sits. The chrome around it — the label, the live word, the footer — speaks the active language.
 *
 * The window is bounded and the footer states what the bound cost, because a well that silently
 * skipped the middle of a build log would be a worse lie than one that showed nothing.
 *
 * The tail is followed only **while live**; scrolling a finished log out from under a reader is
 * rude, and a person who scrolled up did so on purpose.
 */
export function MonoWell({ text, label, live = false, dropped = 0, tall = false, fill = false }: {
  text: string; label?: ReactNode; live?: boolean; dropped?: number; tall?: boolean;
  /**
   * Fill the height the caller gives instead of capping at one. What a growable output panel needs
   * (D55): a well with a maximum of its own can be put in a taller box and simply not use it.
   */
  fill?: boolean;
}) {
  const { t } = useTranslation();
  const well = useRef<HTMLPreElement>(null);

  useEffect(() => {
    if (live && well.current) well.current.scrollTop = well.current.scrollHeight;
  }, [text, live]);

  return (
    <div className={cn(fill && 'flex min-h-0 flex-1 flex-col')}>
      {(label || live) && (
        <p className="mb-1 flex items-baseline gap-2 text-meta text-ink-faint">
          {label && <span>{label}</span>}
          {live && <Dot tone="live" label={t('console.live')} />}
        </p>
      )}
      <pre
        ref={well}
        className={cn(
          'm-0 overflow-auto whitespace-pre-wrap break-words rounded-control border border-line',
          'bg-raised px-3 py-2.5 font-mono text-small leading-relaxed text-ink-soft',
          fill ? 'min-h-0 flex-1' : tall ? 'max-h-[26rem] min-h-40' : 'max-h-72',
        )}
      >
        {text}
      </pre>
      {dropped > 0 && (
        <p className="mt-1 text-meta text-ink-faint">{t('console.dropped', { count: dropped })}</p>
      )}
    </div>
  );
}

/**
 * The `label · value` pairs of a record's head.
 *
 * @remarks
 * **A pair with no value is absent, never blank.** On a session record every absence means something
 * real — no profile means the harness's own configuration home, no version means the record predates
 * the toolchain, no tree means a browser is reading over a remote — and a dash in the value slot
 * reads as a bug in all three. The caller says what it knows; this renders what it was given.
 */
export function MetaLine({ items, className }: {
  items: { label: string; value: ReactNode; mono?: boolean }[]; className?: string;
}) {
  const shown = items.filter((item) => item.value !== null && item.value !== undefined && item.value !== '');
  if (!shown.length) return null;
  return (
    <dl className={cn('m-0 flex flex-wrap items-baseline gap-x-4 gap-y-1 text-small', className)}>
      {shown.map((item) => (
        <div key={item.label} className="flex min-w-0 items-baseline gap-1.5">
          <dt className="shrink-0 text-ink-faint">{item.label}</dt>
          <dd className={cn('m-0 min-w-0 text-ink-soft', item.mono && 'font-mono text-small')}>
            {item.mono && typeof item.value === 'string' ? <PathText path={item.value} /> : item.value}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * A path or a URL that may have to wrap: it breaks after a separator, and inside a name only when
 * that one name is wider than the line. `break-all` broke anywhere, so a session's tree read
 * `family\g` over `ame` (UX5 U8). The text is unchanged: a `<wbr>` adds a place to break and no
 * character, so a copy is still the path.
 */
export function PathText({ path, className }: { path: string; className?: string }) {
  const parts = path.split(/(?<=[\\/])/);
  return (
    // A right-click copies the path whole (CTX1, D138).
    <span data-copy={path} className={cn('font-mono wrap-anywhere', className)}>
      {parts.map((part, index) => (
        <Fragment key={index}>{part}{index < parts.length - 1 && <wbr />}</Fragment>
      ))}
    </span>
  );
}
