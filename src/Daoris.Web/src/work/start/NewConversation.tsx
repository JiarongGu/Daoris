import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MAX_FILE_BYTES, MAX_FILES } from '../../compose/attachments';
import { type Carry, NO_CARRY, useCarry, useFileChooser } from '../../compose/carry';
import { size } from '../../locales/format';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import type { Account } from '../../agents/tools';
import { Button, Icon, Inline, Menu, Tip } from '../../design/ui';
import { type AgentChoice, AgentChip } from './AgentChip';
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

/**
 * What the checkout's branch read answered: `read` is the answer once there is one (null `branch` is a detached HEAD), and
 * absent while pending, refused or unavailable; `refresh` asks again. A hook handed in, as the molecule reaches no bridge
 * (the presentational boundary): the frame owns the read, a story or a test hands a fixed one.
 */
export type CheckoutReader = (repository: string | null, workspace?: string) => {
  read: { branch: string | null } | undefined;
  refresh: () => void;
};

/** No reader handed in: the checkout is said plain. */
const UNREAD: CheckoutReader = () => ({ read: undefined, refresh: () => {} });

/**
 * The tree chip and its menu (NEW1e-b, design §1). The person's checkout is never switched, so where *The checkout* is chosen
 * the chip names the branch it is on: read for the chosen repository, again when the menu opens (a branch switched outside
 * Daoris shows), never polled. While the read is pending, refused or unavailable the checkout is said plain; the driver's
 * choice and its own tree name no branch.
 */
function TreeChip({ tree, onTree, repository, workspace, readCheckout }: {
  tree: TreeChoice;
  onTree: (next: TreeChoice) => void;
  /** The chosen repository when it has a checkout here, else null: nothing is asked of one that has none. */
  repository: string | null;
  workspace?: string;
  readCheckout: CheckoutReader;
}) {
  const { t } = useTranslation();
  const { read, refresh } = readCheckout(repository, workspace);
  const on = (key: 'tree.checkout' | 'tree.checkoutNote') => {
    const plain = t(`work.start.${key}`);
    if (!read) return plain;
    const where = read.branch === null ? t('work.start.tree.detached') : read.branch;
    return key === 'tree.checkout' ? `${plain} · ${where}` : t('work.start.tree.checkoutNoteOn', { where });
  };
  const named = (choice: TreeChoice) => (choice === 'checkout' ? on('tree.checkout') : t(`work.start.tree.${choice}`));
  const treeName = named(tree);
  return (
    <Menu.Root onOpenChange={(open) => { if (open && repository) refresh(); }}>
      <Menu.Trigger asChild>
        <button type="button" aria-label={t('work.start.chip', { field: t('work.start.tree'), value: treeName })} className={CHIP}>
          <Icon name="stepBranch" size={13} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{treeName}</span>
          <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
        </button>
      </Menu.Trigger>
      <Menu.Content align="start" className="w-72 max-w-[calc(100vw-2rem)]">
        <Menu.RadioGroup aria-label={t('work.start.tree')} value={tree} onValueChange={(next) => onTree(next as TreeChoice)}>
          {(['driver', 'checkout', 'own'] as const).map((choice) => (
            <Menu.RadioItem key={choice} value={choice}>
              <span className="grid min-w-0">
                <span>{t(`work.start.tree.${choice}`)}</span>
                <span className="text-meta text-ink-faint">
                  {choice === 'checkout' ? on('tree.checkoutNote') : t(`work.start.tree.${choice}Note`)}
                </span>
              </span>
            </Menu.RadioItem>
          ))}
        </Menu.RadioGroup>
      </Menu.Content>
    </Menu.Root>
  );
}

/**
 * The repository a start opens on: where the person was when they pressed *New conversation* (NEW1f), busy or not, as the tree
 * chip says it is busy; else the one last chosen; each only where it can still be chosen. Else the newest recent one nothing
 * works in, else the first nothing works in, else the first with a checkout here (UX5 U68: it opened on a busy one and was refused).
 */
export function openingRepository(
  repositories: readonly StartRepository[], busy: Readonly<Record<string, string>>, recent: readonly string[], remembered?: string | null,
  attended?: string | null,
): string | null {
  const here = repositories.filter((row) => row.checkout).map((row) => row.name);
  if (attended && here.includes(attended)) return attended;
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
 * names Settings gives them (UX5 U67), and the accounts in the set's order (D197 point 2).
 *
 * **Every chip may be left alone.** Silence resolves the way a driven session resolves (D49 §4, D51): the agent from
 * `driver.json`, the account the walk of the agent's set takes (D197), the tree from the repository's standing opt-in.
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
  attached = true, repositories, busy = {}, recent = [], remembered, attended, onChoose, harnesses, labels = {}, names = {},
  defaultHarness = '', accounts, orderOf, draft, onDraft, carried, onCarry, pending = false, refusal, focus = 0, onFocused, onStart,
  onOpenSession, onAdd, onImport, pick, reveal, pickerOpen = false, openingTree = 'driver', readCheckout = UNREAD, onMakeTask,
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
  /** The repository of the session attended when *New conversation* was pressed (NEW1f); it opens ahead of `remembered`. */
  attended?: string | null;
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
  /** Each agent's accounts of Daoris's in the set's order, every workspace's (POOL1c, D197); null where it was not answered. */
  orderOf?: (harness: string) => readonly string[] | null;
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
  /**
   * Bumped to put the focus in the prompt: each *New conversation* pressed, including the press that mounts it. Zero is no
   * ask, and `onFocused` says the ask was met, so its holder can put it down: a composer mounted later by another path (the
   * attended session deleted) takes no focus it was not asked for.
   */
  focus?: number;
  onFocused?: () => void;
  onStart: (start: NewStart) => void;
  /** Attend a session: the one holding a busy checkout. Absent, none is offered. */
  onOpenSession?: (session: string) => void;
  /** *Add repository…* pressed, at the picker's foot or in the first run: the holder opens its drawer over this (NEW1c). */
  onAdd?: () => void;
  /** *Import a folder…*, the same. */
  onImport?: () => void;
  /**
   * A repository the holder just registered, to choose (NEW1c): each new `n` is one ask. It is kept until the registry lists it
   * with a checkout, which a refetch may do a render later, so the choice is not lost to the order of the two.
   */
  pick?: { repository: string; n: number } | null;
  /** Opens the picker on a workspace's group, after an import that registered several (NEW1c). */
  reveal?: { workspace: string; n: number } | null;
  /** The repository picker drawn open, for a story. */
  pickerOpen?: boolean;
  /** The tree the chip opens on, for a story: a person's own press is what chooses it. */
  openingTree?: TreeChoice;
  /** The checkout's branch for the chosen repository, for the tree chip (NEW1e-b). Absent, the checkout is said plain. A hook: keep it stable. */
  readCheckout?: CheckoutReader;
  /**
   * *Make it a task with Daoris…* (NEW1d-b): hands the draft, with a line naming the repository chosen above it, to Ask Daoris as
   * a draft the person sends. Absent, the press is not offered. The composer keeps its own draft.
   */
  onMakeTask?: (text: string) => void;
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
  const [tree, setTree] = useState<TreeChoice>(openingTree);
  const [choice, setChoice] = useState<AgentChoice>({ adapter: '', profile: '' });

  useEffect(() => {
    if (focus <= 0) return;
    field.current?.focus();
    onFocused?.();
    // Only a new ask: the callback is read, not watched.
  }, [focus]);

  // A repository the holder registered is chosen, recorded as a press of the picker is, and waits in `chosen` until the
  // registry lists it: `repository` below falls back to the opening choice for as long as it is not there.
  useEffect(() => {
    if (!pick) return;
    setChosen(pick.repository);
    onChoose?.(pick.repository);
    // Only a new ask.
  }, [pick?.n]);

  if (!attached) {
    return <p className="m-0 px-4 py-7 text-center text-body text-ink-soft">{t('work.start.browser')}</p>;
  }

  const here = repositories.filter((row) => row.checkout);
  const repository = chosen && here.some((row) => row.name === chosen)
    ? chosen
    : openingRepository(repositories, busy, recent, remembered, attended);
  const holder = repository ? busy[repository] : undefined;
  const held = Boolean(holder) && tree !== 'own';

  const { adapter, profile } = choice;

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

  return (
    <form
      // A form at what it holds (platform language §4, D141 §4): the prompt reads as one, centred in the main area. Its
      // room above follows the main area's own width, never the window's (R7, UIFIT1d).
      className="mx-auto grid w-full max-w-[48rem] grid-cols-[minmax(0,1fr)] gap-3 pt-[8vh] @max-3xl/main:pt-6"
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
          foot={onAdd || onImport ? (close) => (
            // Registering is not choosing, and it starts nothing (the design §1): the list goes down, then the drawer comes up.
            <>
              {onAdd && (
                <Button variant="ghost" onClick={() => { close(); onAdd(); }} className="w-full justify-start">
                  {t('work.start.add')}
                </Button>
              )}
              {onImport && (
                <Button variant="ghost" onClick={() => { close(); onImport(); }} className="w-full justify-start">
                  {t('work.start.import')}
                </Button>
              )}
            </>
          ) : undefined}
          reveal={reveal}
          defaultOpen={pickerOpen}
        />

        <TreeChip
          tree={tree}
          onTree={setTree}
          repository={repository && here.some((row) => row.name === repository) ? repository : null}
          workspace={here.find((row) => row.name === repository)?.workspace}
          readCheckout={readCheckout}
        />

        <AgentChip
          value={choice}
          onChange={setChoice}
          harnesses={harnesses}
          labels={labels}
          names={names}
          defaultHarness={defaultHarness}
          accounts={accounts}
          orderOf={orderOf}
        />

        <Tip content={t('work.composer.attachTip')}>
          <Button variant="ghost" aria-label={t('work.composer.attach')} onClick={chooser.open} className="h-7 w-7 justify-center px-0">
            <Icon name="attach" size={15} />
          </Button>
        </Tip>
        {chooser.input}

        {onMakeTask && (
          <Button
            variant="ghost" disabled={!words} className="ml-auto"
            onClick={() => onMakeTask(repository ? `${t('work.start.taskIn', { repository })}\n\n${words}` : words)}
          >
            {t('work.start.task')}
          </Button>
        )}
        <Button type="submit" variant="primary" disabled={!ready} aria-busy={pending || undefined} className={onMakeTask ? undefined : 'ml-auto'}>
          {pending ? t('work.start.going') : t('work.start.go')}
        </Button>
      </div>

      {/* A first run (the design §1): Start waits, and the way out is here, not on another page, so the words are kept. */}
      {here.length === 0 && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <p className="m-0 text-small text-ink-soft">{t('work.start.none')}</p>
          {onAdd && <Button variant="primary" onClick={onAdd}>{t('work.start.add')}</Button>}
          {onImport && <Button onClick={onImport}>{t('work.start.import')}</Button>}
        </div>
      )}
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
