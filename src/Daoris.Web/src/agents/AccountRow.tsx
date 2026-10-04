import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Chip, Icon, Menu, type MenuAct, Pill, Tip } from '../ui';
import { type AccountState, readLine, stateWord } from './agents';

/**
 * **One account on its agent's page** (UX6e, D150 §5.2): who it is, its state as last known with when it was read, the
 * workspaces that may run on it, what runs on it now and what its agent last said; then the one act its state asks for
 * (*Sign in* while signed out, *Try now* while cooling) and its ⋯ with the rest.
 *
 * @remarks
 * **A molecule**: every state arrives as props, and every press goes out. Nothing here asks the agent anything: a state is
 * what was last read, and *Read again* is the section's, on the person's press (§5.3). The panels a press opens on the row
 * (a sign-in, *Remove…*'s ask, the model and effort form) arrive as children, so they sit under the row that asked.
 */
export function AccountRow({ label, name, home, state, chip, lines, act, menu, now, children }: {
  /** What a person calls it: who signed in, a key's handle, the directory, or the tool's own sign-in. */
  label: string;
  /** The directory a terminal names, said beside who where the two differ; absent for the tool's own sign-in. */
  name?: string;
  /** Where its directory is, on the name's tip: the bridge is the one surface that carries a path (D47 §4). */
  home?: string;
  state: AccountState;
  /** A chip beside its state: *used by sessions* where the starts begin on it. */
  chip?: string;
  /** The lines under it: the workspaces that may run on it, what runs on it now, what its agent last said. */
  lines: string[];
  /** The one act its state asks for. */
  act?: { label: string; ariaLabel?: string; onPress: () => void; disabled?: boolean; loud?: boolean };
  /** Its ⋯: the rest of what is done to it; none draws no ⋯. */
  menu: MenuAct[];
  /** The moment *read 10:42* is measured against; now, unless a story's. */
  now?: Date;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const word = stateWord(state);
  const read = readLine(state, now);
  return (
    <li aria-label={label} className="flex flex-wrap items-start gap-x-3 gap-y-1 border-t border-line py-2 first:border-t-0">
      <span className="flex min-w-0 flex-1 basis-60 flex-col gap-0.5">
        <span className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <Icon name="account" size={13} className="text-ink-faint" />
          <span className="min-w-0 text-body font-medium text-ink [overflow-wrap:anywhere]">{label}</span>
          {name && name !== label && (
            home
              ? <Tip content={home}><span className="font-mono text-meta text-ink-faint">{name}</span></Tip>
              : <span className="font-mono text-meta text-ink-faint">{name}</span>
          )}
          {/* 🔴 A key is never said to be signed in (AGT3): the tool says so for any key, a wrong one included, and the tip
              says when a key is checked. */}
          {state.state === 'keyed'
            ? <Tip content={t('harness.login.keyedTip')}><span><Pill tone={word.tone}>{word.label}</Pill></span></Tip>
            : <Pill tone={word.tone}>{word.label}</Pill>}
          {read && <span className="text-meta text-ink-faint">{read}</span>}
          {chip && <Chip accent>{chip}</Chip>}
        </span>
        {lines.filter(Boolean).map((line) => (
          <span key={line} className="text-meta text-ink-faint [overflow-wrap:anywhere]">{line}</span>
        ))}
      </span>
      <span className="ml-auto flex shrink-0 items-center gap-1">
        {act && (
          <Button
            variant={act.loud ? 'default' : 'ghost'}
            disabled={act.disabled}
            aria-label={act.ariaLabel}
            onClick={act.onPress}
          >
            {act.label}
          </Button>
        )}
        {menu.length > 0 && (
          <Menu.Root>
            <Tip content={t('agents.account.more', { account: label })}>
              <Menu.Trigger asChild>
                <Button variant="ghost" aria-label={t('agents.account.more', { account: label })} className="h-7 w-7 justify-center px-0">
                  <Icon name="more" size={15} />
                </Button>
              </Menu.Trigger>
            </Tip>
            <Menu.Content side="bottom" align="end" className="min-w-52">
              <Menu.Acts acts={menu} />
            </Menu.Content>
          </Menu.Root>
        )}
      </span>
      {children}
    </li>
  );
}
