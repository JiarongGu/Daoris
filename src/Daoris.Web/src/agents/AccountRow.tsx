import { Children, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, Menu, type MenuAct, Pill, Tip, WhyGlyph } from '../ui';
import { type AccountState, readLine, stateWhen, stateWord, type UsagePart } from './agents';

/**
 * The ink each piece of what its agent said wears (ACCTUX1): a share is a reading a person acts on, so the ink; a reset and
 * when it was said are times, so the soft ink, which the line's own colour gives its joins. At 11 px in the faint ink the
 * whole line read 3.38:1 in light, as metadata, which it is not.
 */
const SAID_INK: Record<UsagePart['tone'], string | undefined> = { reading: 'text-ink', when: 'text-ink-soft', join: undefined };

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
 * there, and the stacked row names each fact by its place (the UX7 design §7). Hidden from a reader, whose row says each fact.
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

/**
 * **One account on its agent's page** (UX7b, D152 §4.2): one line at 1546 px, in the list's columns, and three short lines at
 * 680. Its name leads, the account's, with who signed in beside it where the two differ, in the same place on every row and
 * never one as title and the other faint; then its state as last known and when, the workspaces it runs for (*no workspace*
 * said), what runs on it now, its one act by state and its ⋯ with the rest. What its agent last said is a second line under
 * that row alone, and only when it said something: *nothing said yet* and *0 sessions* are not said.
 *
 * @remarks
 * **A molecule**: every state arrives as props, and every press goes out. Nothing here asks the agent anything: a state is
 * what was last read, and reading again is the person's press (§5.3, ROSTER1). The panels a press opens on the row (a
 * sign-in, *Remove…*'s ask, *Use in a workspace…*'s question, a rename) arrive as children, so they sit under the row that
 * asked, across every column.
 */
export function AccountRow({ name, who, id, home, why, state, runs, current, said, act, menu, now, children }: {
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
  /** What its agent last said, the row's second line, in pieces each wearing its ink (`usageLine`); null says nothing. */
  said?: readonly UsagePart[] | null;
  /** The one act its state asks for. */
  act?: { label: string; ariaLabel?: string; onPress: () => void; disabled?: boolean; loud?: boolean };
  /** Its ⋯: the rest of what is done to it; none draws no ⋯. */
  menu: MenuAct[];
  /** The moment its read is measured against; now, unless a story's. */
  now?: Date;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const word = stateWord(state);
  const when = stateWhen(state, now);
  // The tip says the whole of it: when it was read, or a cool-off's until, how long and why.
  const tip = readLine(state, now) ?? when;
  const nameTip = id && id !== name ? (home ? `${id} · ${home}` : id) : home;
  const named = <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{name}</span>;
  return (
    <li
      aria-label={name}
      // A line at 1546 px (D152 §4.2, a row at most 36 px): the act's own padding is the row's air, so the row adds little.
      className="grid grid-cols-[minmax(0,1fr)_auto_1.75rem] items-center gap-x-3 gap-y-0.5 border-t border-line py-1 @min-[40rem]/main:col-span-full @min-[40rem]/main:grid-cols-subgrid @min-[40rem]/main:py-0.5"
    >
      <span className="col-start-1 row-start-1 flex min-w-0 items-baseline gap-x-2">
        {nameTip ? <Tip content={nameTip}>{named}</Tip> : named}
        {why && <span className="self-center"><WhyGlyph why={why} /></span>}
        {who && (
          <Tip content={who}>
            <span className="min-w-0 truncate text-small text-ink-soft">{who}</span>
          </Tip>
        )}
      </span>

      {/* State and *Runs for*: one line under the name when the row stacks, two cells of the row when it does not. */}
      <span className="col-span-3 col-start-1 row-start-2 flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft @min-[40rem]/main:contents">
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 @min-[40rem]/main:col-start-2 @min-[40rem]/main:row-start-1">
          {/* 🔴 A key is never said to be signed in (AGT3): the tool says so for any key, a wrong one included. One its
              provider refused says so, and its tip what repairs it (ACCTUX1). */}
          {state.state === 'keyed' || state.state === 'refused'
            ? (
              <Tip content={t(state.state === 'keyed' ? 'harness.login.keyedTip' : 'harness.login.refusedTip')}>
                <span><Pill tone={word.tone}>{word.label}</Pill></span>
              </Tip>
            )
            : <Pill tone={word.tone}>{word.label}</Pill>}
          {/* When it was read, or a hold's end, is a time the person acts on: the soft ink, not the faint (ACCTUX1). */}
          {when && (
            <Tip content={tip ?? when}>
              <span className="text-small text-ink-soft">{when}</span>
            </Tip>
          )}
        </span>
        <span className="min-w-0 [overflow-wrap:anywhere] before:mr-1.5 before:text-ink-faint before:content-['·'] @min-[40rem]/main:col-start-3 @min-[40rem]/main:row-start-1 @min-[40rem]/main:before:content-none">
          {runs}
        </span>
      </span>

      {current && (
        <span className="col-span-3 col-start-1 row-start-3 text-small text-ink-soft @min-[40rem]/main:col-span-1 @min-[40rem]/main:col-start-4 @min-[40rem]/main:row-start-1">
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

      {said && said.length > 0 && (
        <span className="col-span-full col-start-1 text-small text-ink-soft [overflow-wrap:anywhere]">
          {said.map((part, at) => <span key={at} className={SAID_INK[part.tone]}>{part.text}</span>)}
        </span>
      )}
      {/* Only a panel that is open takes a line: the row's children are its closed panels' `false` as often as not. */}
      {Children.toArray(children).length > 0 && (
        <div className="col-span-full col-start-1 flex min-w-0 flex-col gap-2 pb-1 pt-1">{children}</div>
      )}
    </li>
  );
}
