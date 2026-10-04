import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { list } from '../format';
import { Button, CheckField, Inline, Pill } from '../ui';
import { type AccountState, type JoinChoice, stateWhen, stateWord } from './agents';

// *Add an account…* (UX7b, the UX7 design §4.5; ACCT1's screen half): three steps under the agent's header, one live control
// at a time, since a step in a task happens where it was started (the platform language §4). Step 1 steers a person signing
// an account back in to its own row, step 2 is the tool's own sign-in (`SignIn`, handed in by the organism), and step 3 asks
// the new account's name (ACCT2) and the lists it joins. Molecules: every state arrives as props and every press goes out.

const FIELD = 'min-w-0 max-w-full flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint';
const PANEL = 'rounded-card border border-line bg-raised p-3';

/** An account step 1 offers back: its id, its name, its state as last known and where it runs. */
export type BackAccount = { id: string; name: string; state: AccountState; runs: string };

/**
 * Step 1, before anything starts (§4.5): *Signing an account back in? Use its row, so the lists that hold it keep it*, each
 * account that reads signed out or unknown with its state, its lists and *Sign in to account-2*, then *Add a new account* and
 * *Never mind*. On the install the owner pressed *Sign in to another account* to bring one back and made a new account that
 * no list held (ACCT1); this is the step that would have stopped them.
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
        {accounts.map((account) => {
          const word = stateWord(account.state);
          return (
            <li key={account.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
              <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{account.name}</span>
              <span className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft">
                <Pill tone={word.tone}>{word.label}</Pill>
                <span className="text-ink-faint">{stateWhen(account.state, now)}</span>
                <span className="before:mr-1.5 before:text-ink-faint before:content-['·']">{account.runs}</span>
              </span>
              <Button variant="primary" disabled={busy} onClick={() => onSignIn(account.id)}>
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

/** What a list's box says beside its name: who it holds, the workspaces this machine's runs, and where ticking moves starts. */
function choiceDetail(t: ReturnType<typeof useTranslation>['t'], choice: JoinChoice, account: string): string {
  const own = t('agents.use.summary.own');
  const holds = choice.holds.length > 0 ? choice.holds.join(', ') : own;
  const said = choice.workspace === null
    ? (choice.usedBy.length > 0 ? t('agents.add.usedBy', { workspaces: list(choice.usedBy), accounts: holds }) : holds)
    : t(choice.listed ? 'agents.add.ownList' : 'agents.add.ownDefault', { accounts: holds, account: holds });
  if (!choice.moves) return said;
  const moves = choice.usedBy.length > 0
    ? t('agents.add.moves', { workspaces: list(choice.usedBy), account })
    : t('agents.add.movesMachine', { account });
  return `${said}; ${moves}`;
}

/**
 * Step 3, and a row's *Use in a workspace…* (§4.5): the account's name, its email offered and the person's to change (ACCT2),
 * where a name is asked; then *Let it run work in:*, a box per list, each saying who it holds and, where ticking it moves
 * where a workspace's work starts, that it does; *Add to the lists* and the way out, with the terminal's twin under them.
 * An account placed in no list keeps *Use in a workspace…* on its row, so the question is never lost by *Not now*.
 */
export function PlaceAccount({
  agent, account, title, signedIn, offered, choices, busy = false, cancelLabel, onDone, onCancel,
}: {
  /** The agent's id and the account's, which the terminal's twin names. */
  agent: string;
  account: string;
  /** What the panel is called, for a reader: *Add an account*, or *Where account-3 runs work*. */
  title: string;
  /** Who the sign-in reached, for step 3's first line; null when the tool did not say; absent draws no line. */
  signedIn?: string | null;
  /** The name offered in its field (step 3); absent asks no name. */
  offered?: string;
  choices: readonly JoinChoice[];
  busy?: boolean;
  /** The way out's word: *Not now* at a sign-in's end, *Never mind* on a row. */
  cancelLabel: string;
  /** The answer: the name in the field where one was asked (empty is none), and the lists ticked, null for this machine's. */
  onDone: (answer: { name?: string; join: (string | null)[] }) => void;
  /** The way out: the name in the field kept where one was asked, the lists left as they are. */
  onCancel: (answer: { name?: string }) => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(offered ?? '');
  const [ticked, setTicked] = useState<ReadonlySet<string | null>>(new Set());
  const asksName = offered !== undefined;
  const join = choices.map((choice) => choice.workspace).filter((workspace) => ticked.has(workspace));
  const joining = join.length > 0;
  const commands = [
    ...(asksName && name.trim() ? [`\`daoris agent profile rename ${agent} ${account} ${name.trim()}\``] : []),
    ...(choices.length > 0
      ? [`\`daoris agent profile join ${agent} ${account} ${joining
        ? [...join.filter((each): each is string => each !== null), ...(join.includes(null) ? ['--machine'] : [])].join(' ')
        : '<workspace>…|--machine'}\``]
      : []),
  ];
  const answer = () => (asksName ? { name: name.trim() } : {});
  return (
    <section aria-label={title} className={PANEL}>
      <form
        className="grid gap-2.5"
        onSubmit={(event) => {
          event.preventDefault();
          if (joining || (asksName && choices.length === 0)) onDone({ ...answer(), join });
        }}
      >
        {signedIn !== undefined && (
          <p className="m-0 text-body text-ink">
            {signedIn ? t('agents.add.signedIn', { account: signedIn }) : t('agents.add.signedInUnnamed')}
          </p>
        )}
        {asksName && (
          <label className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="text-body font-medium text-ink">{t('agents.add.name')}</span>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              spellCheck={false}
              autoComplete="off"
              placeholder={account}
              className={FIELD}
            />
            <span className="basis-full text-meta text-ink-faint">{t('agents.add.nameHint', { id: account })}</span>
          </label>
        )}
        {choices.length > 0 && (
          <fieldset className="m-0 grid gap-1.5 border-0 p-0">
            <legend className="mb-1 p-0 text-body font-medium text-ink">{t('agents.add.lists')}</legend>
            {choices.map((choice) => (
              <div key={choice.workspace ?? ''} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 pl-1">
                <CheckField
                  checked={ticked.has(choice.workspace)}
                  onChange={(on) => setTicked((was) => {
                    const next = new Set(was);
                    if (on) next.add(choice.workspace);
                    else next.delete(choice.workspace);
                    return next;
                  })}
                  label={choice.workspace ?? t('agents.add.machine')}
                  className="text-ink"
                />
                <span className="min-w-0 flex-1 basis-60 text-small text-ink-soft [overflow-wrap:anywhere]">
                  {choiceDetail(t, choice, name.trim() || account)}
                </span>
              </div>
            ))}
          </fieldset>
        )}
        <div className="flex flex-wrap items-center gap-2">
          {choices.length > 0
            ? <Button type="submit" variant="primary" disabled={busy || !joining}>{t('agents.add.join')}</Button>
            : <Button type="submit" variant="primary" disabled={busy}>{t('agents.rename.save')}</Button>}
          <Button variant="ghost" disabled={busy} onClick={() => onCancel(answer())}>{cancelLabel}</Button>
        </div>
        {commands.length > 0 && (
          <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]">
            <Inline text={t('agents.add.terminal', { commands: commands.join(' · ') })} />
          </p>
        )}
      </form>
    </section>
  );
}

/**
 * A rename, under its row (ACCT2's screen twin of `daoris agent profile rename`): the name in a field, *Save the name* and
 * *Never mind*. Emptying it gives the account no name of its own, and it reads as its id again.
 */
export function RenameAccount({ agent, account, current, busy = false, onSave, onCancel }: {
  agent: string;
  account: string;
  /** Its name now, null where it has none. */
  current: string | null;
  busy?: boolean;
  /** The name to keep, or null for none. */
  onSave: (name: string | null) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(current ?? '');
  const twin = `\`daoris agent profile rename ${agent} ${account} ${name.trim() || account}\``;
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
          placeholder={account}
          className={FIELD}
        />
      </label>
      <Button type="submit" variant="primary" disabled={busy || name.trim() === (current ?? '')}>{t('agents.rename.save')}</Button>
      <Button variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      <span className="basis-full text-meta text-ink-faint [overflow-wrap:anywhere]">
        <Inline text={t('agents.add.terminal', { commands: twin })} />
      </span>
    </form>
  );
}
