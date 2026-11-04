import { Fragment, type ReactNode, useContext, useRef } from 'react';
import { Check, ChevronDown, ChevronUp, Layers } from 'lucide-react';
import { cn } from '../../lib/cn';
import * as RadixSelect from '@radix-ui/react-select';
import * as Checkbox from '@radix-ui/react-checkbox';
import { glyphSize, FRAME_HELD, FrameHeld } from './Icon';

/* ---------------------------------------------------------------- form controls */

/** A select's options as runs of one group each, in order: a run with no group draws no heading. */
function selectRuns<T extends { group?: string }>(options: readonly T[]): { group?: string; options: T[] }[] {
  const runs: { group?: string; options: T[] }[] = [];
  for (const option of options) {
    const last = runs.at(-1);
    if (last && last.group === option.group) last.options.push(option);
    else runs.push({ group: option.group, options: [option] });
  }
  return runs;
}

export function SelectField({ value, onChange, options, placeholder, ariaLabel, required, disabled, bar }: {
  value: string;
  onChange: (value: string) => void;
  /**
   * The choices in order. An option's `group` names the heading it falls under, said once above the first option of it.
   */
  options: { value: string; label: string; group?: string }[];
  placeholder?: string;
  ariaLabel?: string;
  required?: boolean;
  /** Nothing can be chosen while it is set: a change already on its way (AGT6b). */
  disabled?: boolean;
  /**
   * 🔴 The status bar's shape rather than a form's. It was already there and looked wrong: a 30px bordered control
   * parked in a 24px bar, which reads as a form that fell out of a dialog. A bar item is
   * borderless, full-height, and lights on hover like every other item beside it — the chooser is
   * the same chooser, and only its trigger belongs to the bar.
   */
  bar?: boolean;
}) {
  const held = useContext(FrameHeld);
  return (
    <RadixSelect.Root value={value || undefined} onValueChange={onChange} required={required} disabled={disabled}>
      <RadixSelect.Trigger
        aria-label={ariaLabel}
        className={bar
          ? 'flex h-full items-center gap-1.5 px-2 text-meta text-ink transition-colors duration-[var(--speed)] hover:bg-accent-soft focus-visible:bg-accent-soft focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-accent'
          : 'inline-flex min-h-[1.9rem] min-w-0 max-w-full items-center justify-between gap-2 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink data-[placeholder]:text-ink-faint data-[disabled]:opacity-60'}
      >
        {bar && <Layers size={glyphSize(12)} aria-hidden className="shrink-0 text-ink-soft" />}
        <span className="min-w-0 truncate"><RadixSelect.Value placeholder={placeholder} /></span>
        <ChevronDown size={glyphSize(bar ? 12 : 14)} aria-hidden className="text-ink-faint" />
      </RadixSelect.Trigger>
      <RadixSelect.Portal>
        {/* SELECT1: a long list is capped at the room the popper measured on its side, and scrolls inside it.
            The viewport hides its own scrollbar, so the arrows at each end are what says there is more. */}
        <RadixSelect.Content
          position="popper" sideOffset={4} collisionPadding={8}
          className={cn(
            'z-30 overflow-hidden rounded-card border border-line bg-overlay shadow-[0_6px_24px_rgb(15_12_8/0.12)]',
            // Opened from the frame, it keeps the frame's size (ZOOM1); the room the popper measured is the window's, so it is
            // read back through the frame's zoom.
            held
              ? cn(FRAME_HELD, 'max-h-[calc(var(--radix-select-content-available-height)/var(--frame-zoom,1))] min-w-[calc(var(--radix-select-trigger-width)/var(--frame-zoom,1))]')
              : 'max-h-[var(--radix-select-content-available-height)] min-w-[var(--radix-select-trigger-width)]',
          )}
        >
          <RadixSelect.ScrollUpButton className="flex h-5 shrink-0 cursor-default items-center justify-center text-ink-soft">
            <ChevronUp size={glyphSize(14)} aria-hidden />
          </RadixSelect.ScrollUpButton>
          <RadixSelect.Viewport className="p-1">
            {selectRuns(options).map((run, at) => {
              const items = run.options.map((option) => (
                <RadixSelect.Item
                  key={option.value}
                  value={option.value}
                  className="flex cursor-pointer items-center justify-between gap-3 rounded-[4px] px-2 py-1.5 text-body outline-none data-[highlighted]:bg-accent-soft data-[highlighted]:text-ink"
                >
                  <RadixSelect.ItemText>{option.label}</RadixSelect.ItemText>
                  <RadixSelect.ItemIndicator><Check size={glyphSize(14)} aria-hidden /></RadixSelect.ItemIndicator>
                </RadixSelect.Item>
              ));
              return run.group ? (
                <Fragment key={`${run.group}:${at}`}>
                  {at > 0 && <RadixSelect.Separator className="my-1 h-px bg-line" />}
                  <RadixSelect.Group>
                    <RadixSelect.Label className="px-2 pb-1 pt-1 text-meta text-ink-faint">{run.group}</RadixSelect.Label>
                    {items}
                  </RadixSelect.Group>
                </Fragment>
              ) : <Fragment key={`:${at}`}>{items}</Fragment>;
            })}
          </RadixSelect.Viewport>
          <RadixSelect.ScrollDownButton className="flex h-5 shrink-0 cursor-default items-center justify-center text-ink-soft">
            <ChevronDown size={glyphSize(14)} aria-hidden />
          </RadixSelect.ScrollDownButton>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  );
}

/**
 * One choice of a single-select group, drawn as a row that reads whole (QUESTIONUI1a; D208, the supervisor design §8.1-§8.2): a real
 * radio inside the row's label, so the group's Tab, arrows and Space are the browser's own and no roving focus is built, drawn in the
 * platform's palette rather than the OS's (UX5 U5). The rows sharing a `name` are one group; a caller wraps them in its
 * `role="radiogroup"` with the group's name. The row's name is what `labelledBy` names, so the whole row is the press without its
 * every line being the name; `describedBy` adds the rest. Selected, the row wears the accent's border and wash, apart from any mark
 * drawn inside it.
 */
export function ChoiceRow({ name, value, checked, onChange, labelledBy, describedBy, children }: {
  name: string;
  value: string;
  checked: boolean;
  onChange: () => void;
  /** The ids whose text is the choice's accessible name, in order. */
  labelledBy: string;
  /** The ids whose text describes it, or none. */
  describedBy?: string;
  children: ReactNode;
}) {
  return (
    <label
      className={cn(
        'flex min-w-0 cursor-pointer items-start gap-2 rounded-control border px-3 py-2',
        'has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent/60',
        checked ? 'border-accent bg-accent-soft' : 'border-line bg-raised hover:border-line-strong',
      )}
    >
      <input
        type="radio"
        name={name}
        value={value}
        checked={checked}
        onChange={onChange}
        aria-labelledby={labelledBy}
        aria-describedby={describedBy}
        className="mt-1 size-4 shrink-0 cursor-pointer appearance-none rounded-full border border-line-strong bg-raised outline-none checked:border-accent checked:bg-accent checked:shadow-[inset_0_0_0_3px_var(--color-raised)]"
      />
      {children}
    </label>
  );
}

/**
 * A checkbox with its sentence. `hideLabel` keeps the sentence for the accessibility tree and takes
 * it off the screen — for a setting row, where the label already stands at the left and a second
 * copy beside the box would say everything twice.
 */
export function CheckField({ checked, onChange, label, hideLabel, disabled, className }: {
  checked: boolean; onChange: (checked: boolean) => void; label: string; hideLabel?: boolean; disabled?: boolean;
  /** The label's size and ink where the box sits in a quieter row — a review file's *viewed*. */
  className?: string;
}) {
  return (
    <label className={cn('flex cursor-pointer items-center gap-2 whitespace-nowrap text-body text-ink-soft', className)}>
      <Checkbox.Root
        checked={checked}
        disabled={disabled}
        onCheckedChange={(state) => onChange(state === true)}
        className="grid size-4 shrink-0 place-items-center rounded-[4px] border border-line-strong bg-raised data-[state=checked]:border-accent data-[state=checked]:bg-accent data-[state=checked]:text-accent-ink"
      >
        <Checkbox.Indicator><Check size={glyphSize(12)} strokeWidth={2.2} aria-hidden /></Checkbox.Indicator>
      </Checkbox.Root>
      <span className={cn(hideLabel && 'sr-only')}>{label}</span>
    </label>
  );
}

/**
 * A choice of a few, all in view (D66: the theme, the language) — a radiogroup of buttons, the arrow
 * keys moving the choice the way every radiogroup's do. For two to four options that fit a line: a
 * select would hide what a person is choosing between.
 *
 * @remarks
 * **The focus moves with the choice** (UXFIX1), as the WAI-ARIA radio group's does: the chosen option is
 * the group's one tab stop, and an arrow chooses the next and focuses it. Left on the option it came from,
 * Space chose that one again and a screen reader stayed on an option no longer chosen (Knowledge's
 * *Search · Convergence*).
 */
export function Segmented<T extends string>({ label, value, options, onChange, fill = false }: {
  /** The group's accessible name — the row's label, since the options alone say nothing of what. */
  label: string;
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
  /** Spread over its row, each option an equal share: a list's head, Knowledge's two modes (UX6i). */
  fill?: boolean;
}) {
  const radios = useRef<(HTMLButtonElement | null)[]>([]);

  const move = (from: EventTarget, by: number) => {
    // From the option holding the focus, so a second arrow before the owner has taken the first choice moves on from it.
    const focused = radios.current.indexOf(from as HTMLButtonElement);
    const at = focused >= 0 ? focused : options.findIndex((option) => option.value === value);
    const next = (at + by + options.length) % options.length;
    onChange(options[next]!.value);
    radios.current[next]?.focus();
  };

  return (
    <div
      role="radiogroup"
      aria-label={label}
      onKeyDown={(event) => {
        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') move(event.target, 1);
        else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') move(event.target, -1);
        else return;
        event.preventDefault();
      }}
      className={cn('rounded-control border border-line-strong bg-raised p-0.5', fill ? 'flex w-full' : 'inline-flex')}
    >
      {options.map((option, index) => (
        <button
          key={option.value}
          ref={(node) => { radios.current[index] = node; }}
          type="button"
          role="radio"
          aria-checked={value === option.value}
          // Roving focus: the group is one tab stop, and the chosen option is where it lands.
          tabIndex={value === option.value ? 0 : -1}
          onClick={() => onChange(option.value)}
          className={cn(
            'inline-flex items-center rounded-[4px] px-2.5 py-1 text-small transition-colors duration-(--speed)',
            fill && 'min-w-0 flex-1 justify-center',
            value === option.value ? 'bg-accent text-accent-ink' : 'text-ink-soft hover:text-ink',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
