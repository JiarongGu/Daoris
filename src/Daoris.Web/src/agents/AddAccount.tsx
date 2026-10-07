import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { list } from '../format';
import { shellWord } from '../shellWord';
import { Button, CheckField, Inline, Pill } from '../ui';
// Why a question's act was refused, said under it, whole (ACCTEDIT1): the inline confirmation's, so every ask says it alike.
import { Refused } from '../work/InlineConfirm';
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
        {/* The accounts a list holds first, the first of them the one loud press (the platform language §4). */}
        {[...accounts].sort((a, b) => Number(b.state.holdsWork) - Number(a.state.holdsWork)).map((account, at) => {
          const word = stateWord(account.state);
          return (
            <li key={account.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
              <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{account.name}</span>
              <span className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft">
                <Pill tone={word.tone}>{word.label}</Pill>
                <span className="text-ink-faint">{stateWhen(account.state, now)}</span>
                <span className="before:mr-1.5 before:text-ink-faint before:content-['·']">{account.runs}</span>
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
 * What a list's box says beside its name: who it holds, the workspaces this machine's runs, and, where ticking it moves where
 * a workspace's starts begin, that it does (*used by forge; its starts run on account-3 instead of your own sign-in*).
 */
function choiceDetail(t: ReturnType<typeof useTranslation>['t'], choice: JoinChoice, account: string): string {
  if (choice.moves) {
    return choice.usedBy.length > 0
      ? t('agents.add.usedByMoves', { workspaces: list(choice.usedBy), account })
      : t('agents.add.movesMachine', { account });
  }
  const holds = choice.holds.join(', ');
  if (choice.workspace !== null) return t(choice.listed ? 'agents.add.ownList' : 'agents.add.ownDefault', { accounts: holds, account: holds });
  return choice.usedBy.length > 0 ? t('agents.add.usedBy', { workspaces: list(choice.usedBy), accounts: holds }) : holds;
}

/**
 * Step 3, and a row's *Use in a workspace…* (§4.5): the account's name, its email offered and the person's to change (ACCT2),
 * where a name is asked; then *Let it run work in:*, a box per list, each saying who it holds and, where ticking it moves
 * where a workspace's work starts, that it does; *Add to the lists* and the way out, with the terminal's twin under them.
 * An account placed in no list keeps *Use in a workspace…* on its row, so the question is never lost by *Not now*. It stays
 * open until the organism says its answer landed: refused, it keeps the name and the boxes as they were and says why under
 * them (ACCTEDIT1).
 */
export function PlaceAccount({
  agent, account, title, signedIn, offered, choices, busy = false, refusal = null, cancelLabel, onDone, onCancel,
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
  /** Why the last answer was refused, said under the question until it is answered again or closed; null when none was. */
  refusal?: string | null;
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
  // Each argument spelled for whichever shell the twin is pasted into (ACCTQUOTE1).
  const id = shellWord(account, '<account>');
  const commands = [
    ...(asksName && name.trim() ? [`\`daoris agent profile rename ${agent} ${id} ${shellWord(name.trim(), '<name>')}\``] : []),
    ...(choices.length > 0
      ? [`\`daoris agent profile join ${agent} ${id} ${joining
        ? [
          ...join.filter((each): each is string => each !== null).map((workspace) => shellWord(workspace, '<workspace>')),
          ...(join.includes(null) ? ['--machine'] : []),
        ].join(' ')
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
          // Each box's name in one column and what it says in the next, so the sentences line up (the UX7 design §4.7).
          <fieldset className="m-0 grid grid-cols-1 gap-x-4 gap-y-1.5 border-0 p-0 @min-[36rem]/main:grid-cols-[max-content_minmax(0,1fr)]">
            <legend className="mb-1 p-0 text-body font-medium text-ink">{t('agents.add.lists')}</legend>
            {choices.map((choice) => (
              <div key={choice.workspace ?? ''} className="col-span-full grid grid-cols-subgrid items-baseline gap-y-0.5 pl-1">
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
                <span className="min-w-0 pl-6 text-small text-ink-soft [overflow-wrap:anywhere] @min-[36rem]/main:pl-0">
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
        {refusal && <Refused sentence={refusal} />}
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
 * *Never mind*. Emptying it gives the account no name of its own, and it reads as its id again. It stays open until the
 * organism says the name landed: refused, the name typed stays in the field and the refusal is said under it (ACCTEDIT1),
 * and *Never mind* is the one other way out, held while the answer is on its way so a refusal is never said to nobody.
 */
export function RenameAccount({ agent, account, current, busy = false, refusal = null, onSave, onCancel }: {
  agent: string;
  account: string;
  /** Its name now, null where it has none. */
  current: string | null;
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
          placeholder={account}
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
