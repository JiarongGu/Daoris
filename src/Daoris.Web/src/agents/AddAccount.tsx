import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { shellWord } from '../lib/shellWord';
import { Button, Inline, Pill } from '../design/ui';
// Why a question's act was refused, said under it, whole (ACCTEDIT1): the inline confirmation's, so every ask says it alike.
import { Refused } from '../work/InlineConfirm';
import { type AccountState, stateWhen, stateWord } from './agents';

// *Add an account…* (UX7b, the UX7 design §4.5; ACCT1's screen half; POOL1c, the account-pool design §4.1): three steps under
// the agent's header, one live control at a time, since a step in a task happens where it was started (the platform language
// §4). Step 1 steers a person signing an account back in to its own row, step 2 is the tool's own sign-in (`SignIn`, handed in
// by the organism), and step 3 says the account joined Daoris's accounts and asks only its name (ACCT2): every account serves
// every workspace (D197), so nothing is asked about where it runs. Molecules: every state arrives as props and every press goes out.

const FIELD = 'min-w-0 max-w-full flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint';
const PANEL = 'rounded-card border border-line bg-raised p-3';

/** An account step 1 offers back: its id, its name and its state as last known. */
export type BackAccount = { id: string; name: string; state: AccountState };

/**
 * Step 1, before anything starts (§4.5, the account-pool design §4.1): *Signing an account back in? Use its row, so it stays the
 * same account*, each account that reads signed out or unknown with its state and *Sign in to account-2*, in the set's order,
 * then *Add a new account* and *Never mind*. On the install the owner pressed *Sign in to another account* to bring one back
 * and made a new account (ACCT1); this is the step that would have stopped them.
 */
export function ReSignIn({ accounts, busy = false, now, onSignIn, onNew, onClose }: {
  accounts: readonly BackAccount[];
  busy?: boolean;
  now?: Date;
  onSignIn: (id: string) => void;
  onNew: () => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('agents.add.title')} className={PANEL}>
      <p className="m-0 text-body text-ink">{t('agents.add.back')}</p>
      <ul className="m-0 mt-2 list-none p-0">
        {/* In the set's order, as the organism hands them, the first the one loud press (the platform language §4). */}
        {accounts.map((account, at) => {
          const word = stateWord(account.state);
          return (
            <li key={account.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
              <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{account.name}</span>
              <span className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft">
                <Pill tone={word.tone}>{word.label}</Pill>
                <span className="text-ink-faint">{stateWhen(account.state, now)}</span>
              </span>
              <Button variant={at === 0 ? 'primary' : 'default'} disabled={busy} onClick={() => onSignIn(account.id)}>
                {t('agents.act.signInTo', { account: account.name })}
              </Button>
            </li>
          );
        })}
      </ul>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Button disabled={busy} onClick={onNew}>{t('agents.add.new')}</Button>
        <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
      </div>
    </section>
  );
}

/**
 * Step 3 (§4.5; POOL1c, the account-pool design §4.1): *Signed in as …*, or a key's *Added the API key …*; then that it joined
 * Daoris's accounts, every workspace's work may run on it; then *Its name*, who signed in offered and the person's to change
 * (ACCT2); then one press, *Done*, which keeps the name in the field (an emptied field gives none). Nothing is asked about where
 * it runs, since every account serves every workspace (D197), so there is nothing *Not now* could lose. The terminal's twin
 * is the rename, where a name was typed. It stays open until the organism says the name landed: refused, it keeps the name and
 * says why under it (ACCTEDIT1).
 */
export function NameAccount({
  agent, account, title, signedIn, lead, offered, fallback = account, busy = false, refusal = null, onDone,
}: {
  /** The agent's id and the account's, which the terminal's twin names. */
  agent: string;
  account: string;
  /** What the panel is called, for a reader: *Add an account*, or *Add an API key*. */
  title: string;
  /** Who the sign-in reached, for the step's first line; null when the tool did not say; absent draws no line. */
  signedIn?: string | null;
  /** The step's first line where no sign-in reached the account: a key added (ACCTUX3), *Added the API key …wxyz.* */
  lead?: string;
  /** The name offered in its field. */
  offered: string;
  /**
   * What it is called with its field left empty (ACCTUX1): the row's namer with no name given, which for a fresh id is who
   * signed in or a key's handle; its id where nothing else names it, as the hint said of every account before.
   */
  fallback?: string;
  busy?: boolean;
  /** Why the last answer was refused, said under the question until it is answered again; null when none was. */
  refusal?: string | null;
  /** The answer: the name in the field (empty is none). */
  onDone: (answer: { name: string }) => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(offered);
  // Each argument spelled for whichever shell the twin is pasted into (ACCTQUOTE1).
  const twin = name.trim()
    ? `\`daoris agent profile rename ${agent} ${shellWord(account, '<account>')} ${shellWord(name.trim(), '<name>')}\``
    : null;
  // The account as it is called as the step opens: the name offered, else what it is called unnamed.
  const called = offered.trim() || fallback;
  return (
    <section aria-label={title} className={PANEL}>
      <form
        className="grid gap-2.5"
        onSubmit={(event) => {
          event.preventDefault();
          onDone({ name: name.trim() });
        }}
      >
        {lead !== undefined
          ? <p className="m-0 text-body text-ink">{lead}</p>
          : signedIn !== undefined && (
            <p className="m-0 text-body text-ink">
              {signedIn ? t('agents.add.signedIn', { account: signedIn }) : t('agents.add.signedInUnnamed')}
            </p>
          )}
        <p className="m-0 text-body text-ink">{t('agents.add.joined', { account: called })}</p>
        <label className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <span className="text-body font-medium text-ink">{t('agents.add.name')}</span>
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            spellCheck={false}
            autoComplete="off"
            placeholder={fallback}
            className={FIELD}
          />
          {/* The name it is called left empty, and where that is not its id, the id a terminal types (ACCTUX1). */}
          <span className="basis-full text-meta text-ink-faint">
            {t(fallback === account ? 'agents.add.nameHint' : 'agents.add.nameHintId', { name: fallback, id: account })}
          </span>
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" variant="primary" disabled={busy}>{t('agents.add.done')}</Button>
        </div>
        {refusal && <Refused sentence={refusal} />}
        {twin && (
          <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]">
            <Inline text={t('agents.add.terminal', { commands: twin })} />
          </p>
        )}
      </form>
    </section>
  );
}

/**
 * A rename, under its row (ACCT2's screen twin of `daoris agent profile rename`): the name in a field, *Save the name* and
 * *Never mind*. Emptying it gives the account no name of its own, and it reads as it would unnamed (`fallback`, ACCTUX1):
 * who signed in or a key's handle for a fresh id, else its id. It stays open until the
 * organism says the name landed: refused, the name typed stays in the field and the refusal is said under it (ACCTEDIT1),
 * and *Never mind* is the one other way out, held while the answer is on its way so a refusal is never said to nobody.
 */
export function RenameAccount({ agent, account, current, fallback = account, busy = false, refusal = null, onSave, onCancel }: {
  agent: string;
  account: string;
  /** Its name now, null where it has none. */
  current: string | null;
  /** What it is called with no name of its own, its empty field's placeholder; its id unless the row's namer says otherwise. */
  fallback?: string;
  busy?: boolean;
  /** Why the last save was refused, said under the field until it is saved again or closed; null when none was. */
  refusal?: string | null;
  /** The name to keep, or null for none. */
  onSave: (name: string | null) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(current ?? '');
  // Spelled for whichever shell the twin is pasted into (ACCTQUOTE1).
  const twin = `\`daoris agent profile rename ${agent} ${shellWord(account, '<account>')} `
    + `${shellWord(name.trim() || account, '<name>')}\``;
  return (
    <form
      aria-label={t('agents.rename.title', { account: current ?? account })}
      className="flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
      onSubmit={(event) => {
        event.preventDefault();
        onSave(name.trim() || null);
      }}
    >
      <label className="flex min-w-0 flex-1 basis-72 flex-wrap items-center gap-x-3 gap-y-1">
        <span className="text-small font-medium text-ink">{t('agents.add.name')}</span>
        <input
          autoFocus
          value={name}
          onChange={(event) => setName(event.target.value)}
          spellCheck={false}
          autoComplete="off"
          placeholder={fallback}
          className={FIELD}
        />
      </label>
      <Button type="submit" variant="primary" disabled={busy || name.trim() === (current ?? '')}>{t('agents.rename.save')}</Button>
      <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      {refusal && <Refused sentence={refusal} />}
      <span className="basis-full text-meta text-ink-faint [overflow-wrap:anywhere]">
        <Inline text={t('agents.add.terminal', { commands: twin })} />
      </span>
    </form>
  );
}
