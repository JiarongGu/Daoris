import { Children, Fragment, type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, Menu, type MenuAct, Pill, Tip, WhyGlyph } from '../ui';
import { type AccountState, type AccountUsage, readLine, stateWhen, stateWord, type WindowCell } from './agents';

/**
 * A window's share of its own allowance, drawn (ACCTUX4): thin and square-ended on a track that shows the whole allowance, as
 * wide as the share and never wider, with no minimum, so 1% stays 1% and is not a blob (the second opinion's mock-ups); a
 * share past the allowance fills the track, and the words beside it say the share as it was said. The warning hue only where
 * its agent said the window is near or reached. For the eye alone: a reader hears the share in words.
 */
function Allowance({ used, warned }: { used: number; warned: boolean }) {
  return (
    <span aria-hidden data-allowance className="inline-block h-1.5 w-14 shrink-0 self-center bg-line">
      <span className={`block h-full ${warned ? 'bg-warn' : 'bg-ink-soft'}`} style={{ width: `${Math.min(Math.max(used, 0), 1) * 100}%` }} />
    </span>
  );
}

/**
 * One window's cell (ACCTUX4): its name, its bar and what a person acts on in the ink (ACCTUX1: at 11 px in the faint ink
 * the line read 3.38:1 in light, as metadata, which it is not); then its reset and its reading's age together in the soft
 * ink, on the same line where the cell is wide enough and whole on the next where it is not, the mock-ups' two lines, never
 * a time stranded on a line of its own. An unknown window says so in the soft ink, with no bar.
 */
function WindowCellView({ cell }: { cell: WindowCell }) {
  return (
    <span data-window={cell.window} className="flex min-w-0 flex-wrap items-baseline gap-x-3">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5">
        <span className="whitespace-nowrap text-ink-soft">{cell.name}</span>
        {cell.used !== null && <Allowance used={cell.used} warned={cell.warned} />}
        <span className={cell.known ? 'text-ink' : 'text-ink-soft'}>{cell.reading}</span>
      </span>
      {cell.times.length > 0 && (
        <span className="min-w-0 text-ink-soft">
          {/* Each time whole; the floor's narrow cell breaks between them. */}
          {cell.times.map((time, at) => (
            <Fragment key={time}>
              {at > 0 && <span className="text-ink-faint"> · </span>}
              <span className="whitespace-nowrap text-ink-soft">{time}</span>
            </Fragment>
          ))}
        </span>
      )}
    </span>
  );
}

// The columns' wide form is the main area at 40rem and over, a container query on it (D118 §3b), never the window: at the
// install's 800 px main area the columns fit, and at 680 px the row stacks. Every class names it whole, `@min-[40rem]/main:`,
// since the stylesheet is built from the class names the sources spell out.

/**
 * The accounts' columns (UX7b, D152 §4.2): *Account*, *State*, *Runs for*, *Now*, the act and the ⋯, on the list itself so
 * every row's cells line up, each row a subgrid of it. Below 40rem of main area the list is no grid and each row stacks.
 */
export const ACCOUNT_COLUMNS = 'm-0 list-none p-0 @min-[40rem]/main:grid @min-[40rem]/main:grid-cols-[minmax(0,1.35fr)_minmax(0,1.25fr)_minmax(0,1fr)_minmax(0,0.6fr)_auto_1.75rem] @min-[40rem]/main:gap-x-3';

/**
 * The columns' names over the rows, drawn only where the row is one line: at 680 px a header would name columns that are not
 * there, and the stacked row names each fact by its place (the UX7 design §7). Hidden from a reader, whose row names each
 * fact itself (`Fact`, ACCTUX2).
 */
export function AccountColumnsHead() {
  const { t } = useTranslation();
  const cell = 'text-meta font-medium uppercase tracking-[0.04em] text-ink-faint';
  return (
    <li aria-hidden className="hidden pb-1 @min-[40rem]/main:col-span-full @min-[40rem]/main:grid @min-[40rem]/main:grid-cols-subgrid">
      <span className={cell}>{t('agents.col.account')}</span>
      <span className={cell}>{t('agents.col.state')}</span>
      <span className={cell}>{t('agents.col.runs')}</span>
      <span className={cell}>{t('agents.col.now')}</span>
    </li>
  );
}

/** A fact's name for a reader, before its value on the row (ACCTUX2): the columns' names are drawn for the eye alone. */
function Fact({ label }: { label: string }) {
  return <span className="sr-only">{label}</span>;
}

/**
 * Why a state reads as it does, where that is more than its word (ACCTUX2): a cool-off's end, how long and whose time it is,
 * the agent's reset or Daoris's default; a key the tool says yes to whatever it is; a key its provider refused and what
 * repairs it. Null where the word says it all.
 */
function stateWhy(t: (key: string) => string, state: AccountState, now?: Date): string | null {
  switch (state.state) {
    case 'cooling': return readLine(state, now);
    case 'keyed': return t('harness.login.keyedTip');
    case 'refused': return t('harness.login.refusedTip');
    default: return null;
  }
}

/**
 * **One account on its agent's page** (UX7b, D152 §4.2): one line at 1546 px, in the list's columns, and three short lines at
 * 680. Its name leads, the account's, with who signed in beside it where the two differ, in the same place on every row and
 * never one as title and the other faint; then its state as last known and when, the workspaces it runs for (*no workspace*
 * said), what runs on it now, its one act by state and its ⋯ with the rest. What its agent last said is under that row alone,
 * a cell a window (ACCTUX4), the session's and the week's on every row whose agent can say them, each unknown in words until
 * it does; *nothing said yet* and *0 sessions* are not said.
 *
 * @remarks
 * **A molecule**: every state arrives as props, and every press goes out. Nothing here asks the agent anything: a state is
 * what was last read, and reading again is the person's press (§5.3, ROSTER1). The panels a press opens on the row (a
 * sign-in, *Remove…*'s ask, *Use in a workspace…*'s question, a rename) arrive as children, so they sit under the row that
 * asked, across every column. It holds one thing of its own: whether its state's explanation is open.
 *
 * **Read without a pointer** (ACCTUX2, the second opinion's account section): who signed in is said whole and wraps, where it
 * was cut into a tip no key reached; each fact is named for a reader on its row, since the columns' names are not read; and
 * why a state reads as it does (a cool-off's whose-time, a key's check or refusal) opens under the row from a press a
 * keyboard reaches, where it was a tip on hover alone.
 */
export function AccountRow({ name, who, id, home, why, state, runs, current, usage, act, menu, now, children }: {
  /** What leads: the person's name for it, a key's handle, who signed in for a fresh id, its id, or *Your own sign-in*. */
  name: string;
  /** Who signed in, beside the name where the two differ. */
  who?: string | null;
  /** Its id, which a terminal types, on the name's tip with its directory; absent for your own sign-in. */
  id?: string;
  /** Where its directory is: the bridge is the one surface that carries a path (D47 §4). */
  home?: string;
  /** What the row is, on its name's ⓘ: your own sign-in's explanation, one press away rather than a paragraph (§4.3). */
  why?: string;
  state: AccountState;
  /** *Runs for*, in words. */
  runs: string;
  /** *Now*: *1 session* while something runs on it; null leaves the cell blank. */
  current?: string | null;
  /** What its agent last said, under the row's line: a cell a window and the account's notes (`usageCells`); null says nothing. */
  usage?: AccountUsage | null;
  /** The one act its state asks for. */
  act?: { label: string; ariaLabel?: string; onPress: () => void; disabled?: boolean; loud?: boolean };
  /** Its ⋯: the rest of what is done to it; none draws no ⋯. */
  menu: MenuAct[];
  /** The moment its read is measured against; now, unless a story's. */
  now?: Date;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [explained, setExplained] = useState(false);
  const explanationId = useId();
  const word = stateWord(state);
  const when = stateWhen(state, now);
  // Why the state reads so, opened under the row from its press; a state its word says whole has none.
  const explanation = stateWhy(t, state, now);
  // The time's tip says it whole where nothing else does: when it was read. A cool-off's is the explanation's.
  const tip = explanation ? null : readLine(state, now);
  const nameTip = id && id !== name ? (home ? `${id} · ${home}` : id) : home;
  const named = <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{name}</span>;
  const time = <span className="text-small text-ink-soft">{when}</span>;
  return (
    <li
      aria-label={name}
      // A line at 1546 px (D152 §4.2, a row at most 36 px): the act's own padding is the row's air, so the row adds little.
      className="grid grid-cols-[minmax(0,1fr)_auto_1.75rem] items-center gap-x-3 gap-y-0.5 border-t border-line py-1 @min-[40rem]/main:col-span-full @min-[40rem]/main:grid-cols-subgrid @min-[40rem]/main:py-0.5"
    >
      <span className="col-start-1 row-start-1 flex min-w-0 flex-wrap items-baseline gap-x-2">
        {nameTip ? <Tip content={nameTip}>{named}</Tip> : named}
        {why && <span className="self-center"><WhyGlyph why={why} /></span>}
        {/* Who signed in, whole, wrapping where the column is narrow: in a tip it was cut, and no key reached it (ACCTUX2). */}
        {who && <span className="min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{who}</span>}
      </span>

      {/* State and *Runs for*: one line under the name when the row stacks, two cells of the row when it does not. */}
      <span className="col-span-3 col-start-1 row-start-2 flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft @min-[40rem]/main:contents">
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 @min-[40rem]/main:col-start-2 @min-[40rem]/main:row-start-1">
          <Fact label={t('agents.col.state')} />
          {/* 🔴 A key is never said to be signed in (AGT3): the tool says so for any key, a wrong one included. One its
              provider refused says so, and its explanation what repairs it (ACCTUX1). */}
          <Pill tone={word.tone}>{word.label}</Pill>
          {/* When it was read, or a hold's end, is a time the person acts on: the soft ink, not the faint (ACCTUX1). */}
          {when && (tip ? <Tip content={tip}>{time}</Tip> : time)}
          {explanation && (
            <Button
              variant="ghost"
              aria-label={t('agents.account.about', { account: name })}
              aria-expanded={explained}
              aria-controls={explained ? explanationId : undefined}
              onClick={() => setExplained((was) => !was)}
              className="h-7 min-h-0 w-7 justify-center self-center px-0 text-ink-faint hover:text-ink"
            >
              <Icon name="info" size={13} />
            </Button>
          )}
        </span>
        <span className="min-w-0 [overflow-wrap:anywhere] before:mr-1.5 before:text-ink-faint before:content-['·'] @min-[40rem]/main:col-start-3 @min-[40rem]/main:row-start-1 @min-[40rem]/main:before:content-none">
          <Fact label={t('agents.col.runs')} />
          {runs}
        </span>
      </span>

      {current && (
        <span className="col-span-3 col-start-1 row-start-3 text-small text-ink-soft @min-[40rem]/main:col-span-1 @min-[40rem]/main:col-start-4 @min-[40rem]/main:row-start-1">
          <Fact label={t('agents.col.now')} />
          {current}
        </span>
      )}

      {act && (
        <span className="col-start-2 row-start-1 @min-[40rem]/main:col-start-5">
          <Button
            variant={act.loud ? 'default' : 'ghost'}
            disabled={act.disabled}
            aria-label={act.ariaLabel}
            onClick={act.onPress}
            className="whitespace-nowrap"
          >
            {act.label}
          </Button>
        </span>
      )}

      {menu.length > 0 && (
        <span className="col-start-3 row-start-1 @min-[40rem]/main:col-start-6">
          <Menu.Root>
            <Tip content={t('agents.account.more', { account: name })}>
              <Menu.Trigger asChild>
                <Button variant="ghost" aria-label={t('agents.account.more', { account: name })} className="h-7 w-7 justify-center px-0">
                  <Icon name="more" size={15} />
                </Button>
              </Menu.Trigger>
            </Tip>
            <Menu.Content side="bottom" align="end" className="min-w-52">
              <Menu.Acts acts={menu} />
            </Menu.Content>
          </Menu.Root>
        </span>
      )}

      {explained && explanation && (
        <p id={explanationId} className="col-span-full col-start-1 m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
          {explanation}
        </p>
      )}
      {/* What its agent last said (ACCTUX4): a cell a window, the session's and the week's in two columns at every width, so
          each sits in the same place on every row and two makers compare cell by cell; then the account's own notes. */}
      {usage && (
        <div data-usage className="col-span-full col-start-1 min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">
          {usage.cells.length > 0 && (
            <div className="grid grid-cols-2 gap-x-4 gap-y-0.5">
              {usage.cells.map((cell) => <WindowCellView key={cell.window} cell={cell} />)}
            </div>
          )}
          {usage.notes.length > 0 && <p className="m-0 text-ink-soft">{usage.notes.join(' · ')}</p>}
        </div>
      )}
      {/* Only a panel that is open takes a line: the row's children are its closed panels' `false` as often as not. */}
      {Children.toArray(children).length > 0 && (
        <div className="col-span-full col-start-1 flex min-w-0 flex-col gap-2 pb-1 pt-1">{children}</div>
      )}
    </li>
  );
}
