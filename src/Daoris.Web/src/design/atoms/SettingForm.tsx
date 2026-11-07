import { createContext, type CSSProperties, type ReactNode, useContext } from 'react';
import { cn } from '../../lib/cn';
import { Inline } from './Card';
import { WhyGlyph } from './Tip';

/**
 * The label track's bounds: the track is the
 * widest label of the card's rows, content-sized between `floor` and `cap`, and below `stack` of the card's own width
 * it goes, each value under its words. The classes below spell the same numbers; these are for the tests and the records.
 */
export const SETTING_TRACK = { floor: '12rem', cap: '22rem', stack: '32rem' } as const;

/* The two columns every row reads: the track, then the value column, which is the rest. A `fit-content` track is the
   labels' max-content up to the cap, never less than the label cell's floor; the cap is half the width where the card
   is narrower than twice it, so a value keeps room down to the stack. */
const TRACK_COLUMNS = 'grid-cols-[fit-content(min(22rem,50%))_minmax(0,1fr)]';

const SettingListContext = createContext(false);

/**
 * A card's rows on one label track. A layout wrapper, never a second row: it is the
 * one grid, each `SettingRow` in it a subgrid across its two columns, so the track is the widest of the card's labels
 * and every value starts at its edge, the values forming one column the eye runs down. Below 32rem of its own width the
 * track goes and each value sits under its words at the card's full width: the floor belongs to the card, because the
 * track is the card's. What is not a row (a fold, a sentence under the rows) spans both columns.
 *
 * jsdom lays nothing out, so `ui.test.tsx` holds the structure and `measured/settings.spec.ts` the geometry in a browser.
 */
export function SettingList({ id, className, children }: { id?: string; className?: string; children: ReactNode }) {
  return (
    <SettingListContext.Provider value>
      <div
        id={id}
        data-setting-list=""
        className={cn(
          '@container grid gap-x-6 @max-[32rem]:grid-cols-1 [&>*:not([data-setting-row])]:col-span-full',
          TRACK_COLUMNS,
          className,
        )}
      >
        {children}
      </div>
    </SettingListContext.Provider>
  );
}

export function SettingRow({ label, hint, why, control, children }: {
  label: ReactNode; hint?: ReactNode; why?: string; control?: ReactNode; children?: ReactNode;
}) {
  const listed = useContext(SettingListContext);
  const cells = (
    <>
      {/* 🔴 The label's cell keeps a floor, and the value gives way to it. With `minmax(0,1fr)` a path as the control
          took its whole width first, and at 888 the label beside it came out 24px wide, one character a line.
          A path breaks at its separators. */}
      <div data-setting-label="" className="min-w-[12rem]">
        <div className="flex items-center gap-1.5 text-body font-medium text-ink">
          <span>{label}</span>
          {why && <WhyGlyph why={why} />}
        </div>
        {/* The hint takes the track's width and sets none of it (`contain-inline-size`): the track is the widest LABEL
            of the card, and a hint is a sentence, which would have widened every track to the cap (seen in the measured
            run of 2026-10-20: a card of 12-character 中文 labels had a 22rem track). */}
        {hint && (
          <div className="mt-0.5 contain-inline-size text-small text-ink-faint">
            {typeof hint === 'string' ? <Inline text={hint} /> : hint}
          </div>
        )}
      </div>
      {/* The value starts at the track's edge, and the row's own acts follow it, never the pane's far edge (R1): no
          `justify-end` here, and none in a control's own wrapper. The notice sits under the value, in its column. */}
      {(control || children) && (
        <div data-setting-value="" className="grid min-w-0 gap-y-1.5">
          {control && <div className="flex min-w-0 flex-wrap items-center justify-start gap-2">{control}</div>}
          {children && <div className="min-w-0">{children}</div>}
        </div>
      )}
    </>
  );
  const rule = 'border-t border-line py-2.5 first:border-t-0 first:pt-0 last:pb-0';
  if (listed) {
    // In a list the row reads the list's track as a subgrid, and stacks when the list does (R1).
    return (
      <div data-setting-row="" className={cn('col-span-2 grid grid-cols-subgrid items-start gap-y-1.5 @max-[32rem]:col-span-1 @max-[32rem]:grid-cols-1', rule)}>
        {cells}
      </div>
    );
  }
  return (
    // A row alone is its own container: below 32rem the value goes under the label at the row's full width.
    // Beside a 155px label floor a 309px row left the control 130px, and a branch pattern showed eleven characters.
    <div data-setting-row="" className={cn('@container', rule)}>
      <div className={cn('grid items-start gap-x-6 gap-y-1.5 @max-[32rem]:grid-cols-1', TRACK_COLUMNS)}>
        {cells}
      </div>
    </div>
  );
}

/* ---------------------------------------------------------------- forms */

/**
 * What a field holds, and the column it takes for it: a number 4.5rem, a short name 8rem,
 * a name 16rem, a branch pattern 20rem, and an address, a command or a folder the rest of the form's width; `acts` the
 * row's own presses. A fixed one is a `minmax`, so it yields to its container rather than overflow it.
 */
export type FieldKind = 'number' | 'short' | 'name' | 'pattern' | 'long' | 'acts';
export const FIELD_WIDTHS: Record<FieldKind, string> = {
  number: 'minmax(0,4.5rem)',
  short: 'minmax(0,8rem)',
  name: 'minmax(0,16rem)',
  pattern: 'minmax(0,20rem)',
  long: 'minmax(0,1fr)',
  acts: 'auto',
};

/**
 * The width of the form's own container below which its rows stack, each spelled whole so Tailwind sees the classes: a
 * form of two fields at 32rem, the heavy gates' four columns at 40rem.
 */
export type FormFloor = '24rem' | '32rem' | '40rem';
const STACKS: Record<FormFloor, { grid: string; row: string; head: string; label: string }> = {
  '24rem': { grid: '@max-[24rem]:grid-cols-1', row: '@max-[24rem]:col-span-1 @max-[24rem]:grid-cols-1', head: '@max-[24rem]:hidden', label: 'hidden @max-[24rem]:block' },
  '32rem': { grid: '@max-[32rem]:grid-cols-1', row: '@max-[32rem]:col-span-1 @max-[32rem]:grid-cols-1', head: '@max-[32rem]:hidden', label: 'hidden @max-[32rem]:block' },
  '40rem': { grid: '@max-[40rem]:grid-cols-1', row: '@max-[40rem]:col-span-1 @max-[40rem]:grid-cols-1', head: '@max-[40rem]:hidden', label: 'hidden @max-[40rem]:block' },
};
const FormFloorContext = createContext<FormFloor>('32rem');

/**
 * A form laid by its own width: sized to its content up to the pane, never capped
 * below it, and never laid out by the window's width. It is the one grid, its columns by what each field holds
 * (`FIELD_WIDTHS`), and each `FormRow` a subgrid of it, so a form of rows (the heavy gates) keeps its columns aligned
 * while its container holds them; below the form's floor, by a container query of its own, each row stacks, a `head`
 * row goes and each `FormField` asked to show its label only then does. The 40rem and 48rem caps the settings forms
 * wore, and the `md:` columns that read the window, are what this replaces: a form at 630 px of pane kept its three
 * columns because the window measured 1546, and a capped form left half a wide pane empty.
 */
export function FormGrid({ columns, floor = '32rem', className, children }: {
  columns: readonly FieldKind[]; floor?: FormFloor; className?: string; children: ReactNode;
}) {
  return (
    <FormFloorContext.Provider value={floor}>
      <div data-form-grid="" data-form-floor={floor} className={cn('@container min-w-0', className)}>
        <div
          className={cn('grid grid-cols-(--form-columns) items-end gap-x-2 gap-y-2', STACKS[floor].grid)}
          style={{ '--form-columns': columns.map((kind) => FIELD_WIDTHS[kind]).join(' ') } as CSSProperties}
        >
          {children}
        </div>
      </div>
    </FormFloorContext.Provider>
  );
}

/** One row of a `FormGrid`: its fields in the grid's columns, stacked below the floor; a `head` row is the columns' names, gone then. */
export function FormRow({ head = false, className, children }: { head?: boolean; className?: string; children: ReactNode }) {
  const floor = useContext(FormFloorContext);
  return (
    <div
      data-form-row=""
      data-form-head={head || undefined}
      className={cn('col-span-full grid grid-cols-subgrid items-end gap-y-1', STACKS[floor].row, head && STACKS[floor].head, className)}
    >
      {children}
    </div>
  );
}

/**
 * A field with its name over it: shown always, or only when its row is stacked (`when="stacked"`), where the head row
 * that named the column is gone. The control keeps its own accessible name either way.
 */
export function FormField({ label, when = 'always', className, children }: {
  label: ReactNode; when?: 'always' | 'stacked'; className?: string; children: ReactNode;
}) {
  const floor = useContext(FormFloorContext);
  return (
    <label className={cn('grid min-w-0 gap-1', className)}>
      <span className={cn('text-small text-ink-faint', when === 'stacked' && STACKS[floor].label)}>{label}</span>
      {children}
    </label>
  );
}
