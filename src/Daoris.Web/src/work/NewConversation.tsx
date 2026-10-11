import { type KeyboardEvent, type ReactNode, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountName, accountWho } from '../agents/agents';
import { MAX_FILE_BYTES, MAX_FILES } from '../attachments';
import { type Carry, NO_CARRY, useCarry, useFileChooser } from '../compose/carry';
import { size } from '../format';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import type { Account } from '../tools';
import { Button, Icon, Inline, Menu, Tip } from '../ui';
import { CHIP, RepositoryPicker, type StartRepository } from './RepositoryPicker';

export type { StartRepository } from './RepositoryPicker';

/**
 * Where a conversation works (D162 point 2): the driver's choice, the repository's standing opt-in, sends nothing; the
 * checkout and its own tree are said exactly.
 */
export type TreeChoice = 'driver' | 'checkout' | 'own';

/** What *Start* hands over: the words and their files, and only the choices the person made. */
export type NewStart = {
  repository: string;
  text: string;
  files: File[];
  adapter?: string;
  profile?: string;
  /** Absent is the driver's choice; false the checkout, whatever the repository's opt-in; true a tree of its own. */
  ownTree?: boolean;
};

/** "Leave it to the driver", as a radio row's value: a row's value is never empty, and a default stays choosable. */
const DEFAULT = '*';

/**
 * The repository a start opens on: the one last chosen where it can still be chosen, else the newest recent one nothing works
 * in, else the first nothing works in, else the first with a checkout here (UX5 U68: it opened on a busy one and was refused).
 */
export function openingRepository(
  repositories: readonly StartRepository[], busy: Readonly<Record<string, string>>, recent: readonly string[], remembered?: string | null,
): string | null {
  const here = repositories.filter((row) => row.checkout).map((row) => row.name);
  if (remembered && here.includes(remembered)) return remembered;
  return recent.find((name) => here.includes(name) && !busy[name])
    ?? here.find((name) => !busy[name])
    ?? here[0]
    ?? null;
}

/**
 * *New conversation* (NEW1b, D162 point 1, the entry design §1): what the person wants worked on, in their words, then the
 * repository, the tree and the agent with its account, and *Start*. The conversation opens on those words.
 *
 * @remarks
 * **It replaced the *Start session* drawer** (D55's), which started an empty conversation the person typed into after. What
 * the drawer knew is the chips': repositories with a checkout here, a busy checkout and what lets it start, the agents by the
 * names Settings gives them (UX5 U67), and the accounts in the workspace's list's order (D130 §3.2).
 *
 * **Every chip may be left alone.** Silence resolves the way a driven session resolves (D49 §4, D51): the agent from
 * `driver.json`, the account from the workspace's default then the machine's, the tree from the repository's standing opt-in.
 * So the defaults say *driver's*, and send nothing.
 *
 * **Enter starts; Shift+Enter is a new line; an input method's Enter is its own** (IME1). Files go with the words, through
 * the `carry` molecule every composer uses. *Start* waits for words and a repository, and presses once while a start is on
 * its way.
 *
 * **A refusal is the driver's sentence, whole, under *Start*** (UX5 U68), and the words and the choices stay: the composer
 * clears nothing itself. Its holder clears the draft once a conversation took it.
 *
 * **A browser has no shell** (D46 §7): the composer is absent, and the place says where a conversation starts.
 *
 * A molecule: the rosters arrive as props, so a first run, a busy checkout and a signed-out account are reached without a
 * machine in that state.
 */
export function NewConversation({
  attached = true, repositories, busy = {}, recent = [], remembered, onChoose, harnesses, labels = {}, names = {},
  defaultHarness = '', accounts, scopeOf, draft, onDraft, carried, onCarry, pending = false, refusal, focus = 0, onStart,
  onOpenSession, pickerFoot,
}: {
  /** Whether a shell is here; false is a browser. */
  attached?: boolean;
  repositories: readonly StartRepository[];
  /** Each repository whose checkout an active session on this machine holds, with that session (UX5 U68). */
  busy?: Readonly<Record<string, string>>;
  /** This machine's last successful starts, newest first. */
  recent?: readonly string[];
  /** The repository last chosen on this machine. */
  remembered?: string | null;
  /** Told each repository the person chooses, to remember. */
  onChoose?: (repository: string) => void;
  /** The agents installed here; empty offers no choice. */
  harnesses: readonly string[];
  /** What a person calls each way in, as Settings names it (UX5 U67); absent, its short name, then its id. */
  labels?: Readonly<Record<string, string>>;
  /** Each agent's short name for the chip (*Claude Code*); absent, its label, then its id. */
  names?: Readonly<Record<string, string>>;
  /** The driver's own agent, whose accounts are offered while no other is chosen. */
  defaultHarness?: string;
  /** Each agent's accounts, with their sign-in state (D49 §4), by agent: the accounts follow the agent chosen (REV3). */
  accounts: Readonly<Record<string, Pick<Account, 'name' | 'login' | 'account' | 'displayName' | 'key'>[]>>;
  /**
   * The list of accounts a start in this repository may run on, for this agent (D130 §3.2): its workspace's own, or this
   * machine's, with the workspace it is, or null for this machine's. Null where neither names a list.
   */
  scopeOf?: (repository: string, harness: string) => { workspace: string | null; list: string[] } | null;
  /** The words, where the holder keeps them across a visit elsewhere. Absent, the composer holds its own. */
  draft?: string;
  onDraft?: (text: string) => void;
  /** The files with the words, where the holder keeps them. Absent, the composer holds its own. */
  carried?: Carry;
  onCarry?: (next: Carry) => void;
  /** A start is on its way. */
  pending?: boolean;
  /** Why the last start opened nothing, in the driver's own words. */
  refusal?: string | null;
  /** Bumped to put the focus in the prompt: each *New conversation* pressed. Zero is no ask. */
  focus?: number;
  onStart: (start: NewStart) => void;
  /** Attend a session: the one holding a busy checkout. Absent, none is offered. */
  onOpenSession?: (session: string) => void;
  /** What the repository picker offers at its foot (NEW1c). */
  pickerFoot?: ReactNode;
}) {
  const { t } = useTranslation();
  const promptId = useId();
  const field = useRef<HTMLTextAreaElement>(null);
  const [ownText, setOwnText] = useState('');
  const text = draft ?? ownText;
  const setText = onDraft ?? setOwnText;
  const [ownCarry, setOwnCarry] = useState<Carry>(NO_CARRY);
  const carry = carried ?? ownCarry;
  const setCarry = onCarry ?? setOwnCarry;
  const attach = useCarry(carry, setCarry, attached);
  const chooser = useFileChooser(attach.attach, t('carry.choose'));
  const [chosen, setChosen] = useState<string | null>(null);
  const [tree, setTree] = useState<TreeChoice>('driver');
  const [adapter, setAdapter] = useState('');
  const [profile, setProfile] = useState('');

  useEffect(() => {
    if (focus > 0) field.current?.focus();
  }, [focus]);

  if (!attached) {
    return <p className="m-0 px-4 py-7 text-center text-body text-ink-soft">{t('work.start.browser')}</p>;
  }

  const here = repositories.filter((row) => row.checkout);
  const repository = chosen && here.some((row) => row.name === chosen)
    ? chosen
    : openingRepository(repositories, busy, recent, remembered);
  const holder = repository ? busy[repository] : undefined;
  const held = Boolean(holder) && tree !== 'own';

  const agent = adapter || defaultHarness;
  const named = (id: string) => labels[id] ?? names[id] ?? id;
  const profiles = accounts[agent] ?? [];
  const account = profiles.find((choice) => choice.name === profile) ?? null;
  // The scope's accounts first, in its list's order, then every other under *not in its list* (D130 §3.2).
  const scope = repository && agent ? scopeOf?.(repository, agent) ?? null : null;
  const listed = scope && scope.list.length > 0 ? scope.list : null;
  const ordered = listed
    ? [
      ...listed.map((name) => profiles.find((choice) => choice.name === name)).filter((choice) => choice !== undefined),
      ...profiles.filter((choice) => !listed.includes(choice.name)),
    ]
    : profiles;
  const outside = scope?.workspace ? t('work.start.outside', { workspace: scope.workspace }) : t('work.start.outsideMachine');

  const words = text.trim();
  const ready = Boolean(words && repository && !pending);

  const go = () => {
    if (!ready || !repository) return;
    onStart({
      repository,
      text: words,
      files: carry.files,
      ...(adapter ? { adapter } : {}),
      ...(profile ? { profile } : {}),
      ...(tree === 'own' ? { ownTree: true } : tree === 'checkout' ? { ownTree: false } : {}),
    });
  };

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (isComposing(event)) return;
    // Enter starts, as a conversation's box sends; a new line needs the modifier.
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      go();
    }
  };

  const chooseAgent = (next: string) => {
    const harness = next === DEFAULT ? '' : next;
    setAdapter(harness);
    // An account is an agent's own: one this agent does not have means nothing here.
    if (!(accounts[harness || defaultHarness] ?? []).some((choice) => choice.name === profile)) setProfile('');
  };

  const chip = (field: string, value: string) => t('work.start.chip', { field, value });
  const treeName = t(`work.start.tree.${tree}`);
  const agentName = agent ? names[agent] ?? labels[agent] ?? agent : t('work.start.harness');
  const accountShown = account ? accountName(account) : t('work.start.accountDriver');

  return (
    <form
      // A form at what it holds (platform language §4, D141 §4): the prompt reads as one, centred in the main area.
      className="mx-auto grid w-full max-w-[48rem] grid-cols-[minmax(0,1fr)] gap-3 pt-[8vh] max-md:pt-6"
      onSubmit={(event) => { event.preventDefault(); go(); }}
      {...attach.handlers}
    >
      <label htmlFor={promptId} className="text-view font-semibold text-ink">{t('work.start.prompt')}</label>

      {carry.files.length > 0 && (
        <ul aria-label={t('work.composer.attached')} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
          {carry.files.map((file, index) => (
            <li
              key={`${index}:${file.name}`}
              className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-small text-ink"
            >
              <Icon name="attach" size={12} className="shrink-0 text-ink-faint" />
              <span className="min-w-0 truncate">{file.name}</span>
              <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.size)}</span>
              <Button
                variant="ghost"
                aria-label={t('carry.remove', { name: file.name })}
                onClick={() => attach.remove(index)}
                className="h-5 w-5 justify-center px-0"
              >
                <Icon name="x" size={11} />
              </Button>
            </li>
          ))}
        </ul>
      )}
      {attach.leftOff && (
        <p role="status" className="m-0 text-small text-ink-danger">
          {t(`carry.${attach.leftOff}`, { max: MAX_FILES, size: size(MAX_FILE_BYTES) })}
        </p>
      )}

      <textarea
        id={promptId}
        ref={field}
        value={text}
        rows={4}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={onKeyDown}
        className={cn(
          'min-h-24 resize-y rounded-control border bg-raised px-3 py-2 text-body text-ink transition-colors duration-(--speed)',
          // Where a dragged file will go: the whole form takes it, and the box lights up to say so.
          attach.dragging ? 'border-accent bg-accent-soft' : 'border-line-strong',
        )}
      />

      {/* The chips, then the files and Start at the row's end. Below the width they all need they wrap under the prompt, and
          Start keeps the row's end (the design's 680 px). */}
      <div className="flex flex-wrap items-center gap-2">
        <RepositoryPicker
          repositories={repositories}
          busy={busy}
          recent={recent}
          value={repository}
          onChoose={(next) => { setChosen(next); onChoose?.(next); }}
          foot={pickerFoot}
        />

        <Menu.Root>
          <Menu.Trigger asChild>
            <button type="button" aria-label={chip(t('work.start.tree'), treeName)} className={CHIP}>
              <Icon name="stepBranch" size={13} className="shrink-0 text-ink-faint" />
              <span className="min-w-0 truncate">{treeName}</span>
              <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
            </button>
          </Menu.Trigger>
          <Menu.Content align="start" className="w-72 max-w-[calc(100vw-2rem)]">
            <Menu.RadioGroup aria-label={t('work.start.tree')} value={tree} onValueChange={(next) => setTree(next as TreeChoice)}>
              {(['driver', 'checkout', 'own'] as const).map((choice) => (
                <Menu.RadioItem key={choice} value={choice}>
                  <span className="grid min-w-0">
                    <span>{t(`work.start.tree.${choice}`)}</span>
                    <span className="text-meta text-ink-faint">{t(`work.start.tree.${choice}Note`)}</span>
                  </span>
                </Menu.RadioItem>
              ))}
            </Menu.RadioGroup>
          </Menu.Content>
        </Menu.Root>

        <Menu.Root>
          <Menu.Trigger asChild>
            <button type="button" aria-label={chip(t('work.start.agentAccount'), `${agentName} · ${accountShown}`)} className={CHIP}>
              <Icon name="stepWork" size={13} className="shrink-0 text-ink-faint" />
              <span className="min-w-0 truncate">{`${agentName} · ${accountShown}`}</span>
              <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
            </button>
          </Menu.Trigger>
          <Menu.Content align="start" className="w-80 max-w-[calc(100vw-2rem)]">
            {harnesses.length > 1 && (
              <>
                <Menu.Label>{t('work.start.harness')}</Menu.Label>
                <Menu.RadioGroup aria-label={t('work.start.harness')} value={adapter || DEFAULT} onValueChange={chooseAgent}>
                  {/* Which it is, where the machine has one (UX5 U67): *this machine's default* said nothing of what a start
                      would run on. */}
                  <Menu.RadioItem value={DEFAULT}>
                    {defaultHarness
                      ? t('work.start.harnessDefaultNamed', { name: named(defaultHarness) })
                      : t('work.start.harnessDefault')}
                  </Menu.RadioItem>
                  {harnesses.map((harness) => <Menu.RadioItem key={harness} value={harness}>{named(harness)}</Menu.RadioItem>)}
                </Menu.RadioGroup>
              </>
            )}
            {harnesses.length > 1 && profiles.length > 0 && <Menu.Separator />}
            {profiles.length > 0 && (
              <>
                <Menu.Label>{t('work.start.profile')}</Menu.Label>
                <Menu.RadioGroup aria-label={t('work.start.profile')} value={profile || DEFAULT} onValueChange={(next) => setProfile(next === DEFAULT ? '' : next)}>
                  <Menu.RadioItem value={DEFAULT}>{t('work.start.profileDefault')}</Menu.RadioItem>
                  {ordered.map((choice, index) => {
                    // Named as its row on the agent's page leads with it (ACCTNAME1, D152 §4.2); a signed-out one is offered
                    // and labelled, since the spawn's refusal names the sign-in, and a refused key is repaired by a new key.
                    const who = accountWho(choice);
                    const name = who ? `${accountName(choice)} · ${who}` : accountName(choice);
                    const first = listed && !listed.includes(choice.name) && (index === 0 || listed.includes(ordered[index - 1]!.name));
                    return (
                      <div key={choice.name}>
                        {first && <Menu.Label className="pt-2">{outside}</Menu.Label>}
                        <Menu.RadioItem value={choice.name}>
                          {choice.login === 'out' ? t(choice.key ? 'harness.profileRefused' : 'harness.profileOut', { name }) : name}
                        </Menu.RadioItem>
                      </div>
                    );
                  })}
                </Menu.RadioGroup>
              </>
            )}
            {harnesses.length <= 1 && profiles.length === 0 && (
              <Menu.Label>{t('work.start.harnessDefaultNamed', { name: agentName })}</Menu.Label>
            )}
          </Menu.Content>
        </Menu.Root>

        <Tip content={t('work.composer.attachTip')}>
          <Button variant="ghost" aria-label={t('work.composer.attach')} onClick={chooser.open} className="h-7 w-7 justify-center px-0">
            <Icon name="attach" size={15} />
          </Button>
        </Tip>
        {chooser.input}

        <Button type="submit" variant="primary" disabled={!ready} aria-busy={pending || undefined} className="ml-auto">
          {pending ? t('work.start.going') : t('work.start.go')}
        </Button>
      </div>

      {here.length === 0 && <p className="m-0 text-small text-ink-soft">{t('work.start.none')}</p>}
      {/* What lets a busy checkout start after all, said where it is chosen (UX5 U68): its own tree, or the session holding it.
          Nothing is stopped for it. */}
      {held && repository && (
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <p className="m-0 text-small text-ink-soft">{t('work.start.busy', { repository })}</p>
          <Button variant="ghost" onClick={() => setTree('own')} className="min-h-0 px-1.5 py-0.5 text-small">
            {t('work.start.useOwnTree')}
          </Button>
          {onOpenSession && holder && (
            <Button variant="ghost" onClick={() => onOpenSession(holder)} className="min-h-0 px-1.5 py-0.5 text-small">
              {t('work.start.openHolder')}
            </Button>
          )}
        </div>
      )}
      {/* A fresh tree's cost is said where the tree is chosen (D51 §4), not found later by a session whose gates cannot run. */}
      {tree === 'own' && <p className="m-0 text-small text-ink-soft">{t('work.start.ownTreeCost')}</p>}

      {/* The driver's own sentence, whole, where Start was pressed (UX5 U68): it names what to do, and a paraphrase of an
          instruction is a different instruction (D46 §3). */}
      {refusal && (
        <p role="alert" className="m-0 whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-small text-ink-soft">
          <Inline text={refusal} />
        </p>
      )}
    </form>
  );
}
