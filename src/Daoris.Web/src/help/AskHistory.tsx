import { Fragment, type KeyboardEvent, type ReactNode, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { shellWord } from '../shellWord';
import { Button, Icon, type IconName, Inline, Menu, type MenuAct, SkeletonRows, Tip } from '../ui';
import { type Answered, InlineConfirm, Refused } from '../work/InlineConfirm';
import { useListKeys } from '../work/listKeys';
import { marked } from '../work/railSearch';
import { HELP_NAME_LIMIT, type HelpConversationRow, historyGroups, searchable } from './history';
import { previewText, rowLine } from './preview';

/** What a row of the history may do, each pressed through the organism that holds the bridge. */
export type AskHistoryActs = {
  /** Show it in the panel, telling where the list was scrolled so going back puts it there again (ASKHIST1c). */
  onOpen: (id: string, scroll: number) => void;
  /** Name it, or give it its first question back with null; told how it ended. */
  onRename: (id: string, name: string | null, answered: Answered) => void;
  onPin: (id: string, pinned: boolean) => void;
  /** A new conversation handed its words. */
  onStartFrom: (id: string) => void;
  /** Delete it, asked once; told how it ended. */
  onDelete: (id: string, answered: Answered) => void;
};

/** Escape is a field's, a menu's or an input method's before it is the list's. */
const KEEPS_ESCAPE = 'form, [role="menu"], [role="dialog"]';

/**
 * Ask Daoris's history (ASKHIST1, as ASKHIST1c made it): its conversations, kept on this machine only, grouped as a person
 * reads time; a search by words across them; and each one's acts, a rename and a delete asked where they were pressed.
 *
 * @remarks
 * A molecule: the rows arrive made, the search's words go out, and each act goes to the organism (`AskConversation`).
 *
 * - **Its head is fixed and left-led** (UX §2): what it is and that it stays on this machine, *New conversation* at its
 *   right, the search the whole width under them. Only the rows scroll.
 * - **A row reads its title whole**, wrapped where it must be, so two pasted URLs can be told apart without a tip (UX §4);
 *   then *current* on the one the panel shows, whose selection is the accent (UX §3); a conversation's line as plain words
 *   (`previewText`), or where a search found its words, those words marked; and a pin mark, when and what it was. Opening a
 *   row only reads it: what the next words do is said where they are written.
 * - **Its ⋯ is a 28 px control beside the row**, there at rest (UX §6), holding what has no other home (D76's RAIL1 rule). A
 *   live one is offered no delete, since only an ended conversation's record goes.
 * - **Its keys are a list's** (`useListKeys`): ↓ from the search into the rows, ↑ ↓ Home End along them across the groups.
 *   Escape goes back to the conversation once the search is empty; a menu, a form or an input method takes it first.
 * - **Coming back puts the person where they left** (`restore`): the row they opened, and the list's scroll; the search where
 *   that row went.
 * - **Every state says itself** (UX §4): a first load as skeleton rows with its words, a newer answer over the rows held
 *   dimmed, a list it could not read with why and a retry, an empty one with a way to start, a search that found nothing with
 *   a way to clear it, and a search too short to look with how long one must be.
 * - **It is read a page at a time** (ASKHIST1d2): the driver orders and searches every conversation and answers a page of
 *   them (D158's ASKHIST1d1 note). A search says how many it found and that it searched every one; a list with more says how
 *   many of how many it shows, and *Show more* lists the next page and takes the reader to the first row it brought.
 */
export function AskHistory({
  rows, total, loading = false, refreshing = false, error = null, search, shown = null, busy = false, refusal = null,
  restore = null, now, onMore, loadingMore = false, moreError = null, onSearch, onNew, onClose, onRetry, ...acts
}: {
  rows: HelpConversationRow[];
  /** Why the last act pressed from a row's menu (a pin, a new conversation from it) did not happen; null when it did. */
  refusal?: string | null;
  /** How many the whole list holds, or how many a search found; the rows' own count until the driver says. */
  total?: number;
  /** List the next page; absent on the last. */
  onMore?: () => void;
  /** The next page is on its way. */
  loadingMore?: boolean;
  /** Why the next page could not be read, in the sentence the person reads; null when it was. */
  moreError?: string | null;
  /** Its first answer is on its way. */
  loading?: boolean;
  /** A newer answer is on its way: the rows shown are the last one's. */
  refreshing?: boolean;
  /** Why the list could not be read, in the sentence the person reads; null when it was. */
  error?: string | null;
  /** The search's words, as typed. */
  search: string;
  /** The conversation the panel shows, which its row says. */
  shown?: string | null;
  /** An act on one is on its way: the others wait for it. */
  busy?: boolean;
  /** Where the person left the list from, put back as it opens again: the row they opened and its scroll. */
  restore?: { row: string | null; scroll: number } | null;
  /** The clock the groups read; the window's own, unless a story holds it still. */
  now?: Date;
  onSearch: (words: string) => void;
  /** A blank new conversation: the head's and an empty list's. Absent, none is offered. */
  onNew?: () => void;
  /** Back to the conversation the list was opened over. */
  onClose?: () => void;
  /** Ask for the list again, after it could not be read. */
  onRetry?: () => void;
} & AskHistoryActs) {
  const { t } = useTranslation();
  const headingId = useId();
  const keys = useListKeys();
  const scroller = useRef<HTMLDivElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const moreFoot = useRef<HTMLDivElement>(null);
  // The one row whose question is open: its rename, or its delete.
  const [asking, setAsking] = useState<{ id: string; what: 'rename' | 'delete' } | null>(null);
  const words = search.trim();
  const searching = searchable(words);
  const short = words.length > 0 && !searching;
  const groups = historyGroups(rows, now);
  const count = total ?? rows.length;
  // *Show more* pressed: how many rows there were and what was searched, so the first row the next page brings takes the focus.
  const pressed = useRef<{ words: string; at: number } | null>(null);

  // The next page arrived: the reader goes on from its first row, unless they went elsewhere meanwhile (the search, say),
  // where the focus stays. The press leaves with the last page, so the focus may be nowhere when the rows arrive.
  useLayoutEffect(() => {
    const was = pressed.current;
    if (!was) return;
    if (was.words !== words) { pressed.current = null; return; }
    if (rows.length <= was.at) return;
    pressed.current = null;
    const focused = document.activeElement;
    if (focused && focused !== document.body && !moreFoot.current?.contains(focused)) return;
    const first = rows[was.at]!.session;
    [...(scroller.current?.querySelectorAll<HTMLElement>('[data-session]') ?? [])]
      .find((door) => door.dataset.session === first)?.focus();
  }, [rows, words]);

  const more = () => {
    // Pressed again while it reads, it asks nothing more; it stays a press, so the focus is never taken from it.
    if (loadingMore || !onMore) return;
    pressed.current = { words, at: rows.length };
    onMore();
  };

  // Coming back to the list: the row it was left from, where the list was scrolled; the search where that row went.
  useLayoutEffect(() => {
    if (!restore) return;
    if (scroller.current) scroller.current.scrollTop = restore.scroll;
    const row = [...(scroller.current?.querySelectorAll<HTMLElement>('[data-session]') ?? [])]
      .find((door) => door.dataset.session === restore.row);
    (row ?? field.current)?.focus({ preventScroll: true });
    // Once, as the list opens: a later answer moves nothing the person is doing.
  }, []);

  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    // A key an input method is composing with is the method's, the search's arrow into the rows among them (IME1).
    if (isComposing(event)) return;
    keys(event);
    if (event.key !== 'Escape' || event.defaultPrevented || !onClose) return;
    const target = event.target as HTMLElement;
    if (!event.currentTarget.contains(target) || target.closest(KEEPS_ESCAPE)) return;
    event.preventDefault();
    onClose();
  };

  const clear = () => {
    onSearch('');
    field.current?.focus();
  };

  return (
    // `minmax(0,1fr)` on every grid between a row's words and the panel (ASKHIST1b): a grid's implicit column is `auto`, which
    // grows to its widest child's min-content, and a pasted URL ran the search and every row past the dock.
    <section aria-labelledby={headingId} className="flex min-h-0 flex-1 flex-col" onKeyDown={onKeyDown}>
      <header className="grid grid-cols-[minmax(0,1fr)] gap-2 border-b border-line px-4 pb-2.5 pt-2.5">
        <div className="flex flex-wrap items-start gap-x-2 gap-y-1.5">
          <div className="min-w-0 flex-1 basis-40">
            <h3 id={headingId} className="m-0 text-body font-semibold text-ink">{t('help.history.title')}</h3>
            <p className="m-0 text-small text-ink-soft">{t('help.history.kept')}</p>
          </div>
          {onNew && (
            <Button className="shrink-0 px-2.5 text-small" disabled={busy} onClick={onNew}>
              <Icon name="plus" size={13} />
              {t('help.new')}
            </Button>
          )}
        </div>
        <label className="flex min-h-7 items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            ref={field}
            type="search"
            value={search}
            aria-label={t('help.history.search.label')}
            placeholder={t('help.history.search.placeholder')}
            onChange={(event) => onSearch(event.target.value)}
            // Escape clears a search; one with nothing in it goes back to the conversation.
            onKeyDown={(event) => {
              if (isComposing(event)) return;
              if (event.key === 'Escape' && search) { event.preventDefault(); onSearch(''); }
            }}
            className="min-w-0 flex-1 bg-transparent py-1 text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </label>
        {short && <p role="status" className="m-0 text-small text-ink-faint">{t('help.history.short')}</p>}
        {refusal && <Refused sentence={refusal} />}
      </header>

      <div ref={scroller} className={cn('min-h-0 flex-1 overflow-y-auto px-2 pb-3 pt-1.5', refreshing && 'opacity-60')}>
        {error && rows.length > 0 && (
          <div className="mx-2 mb-2 grid grid-cols-[minmax(0,1fr)] justify-items-start gap-1">
            <Refused sentence={error} />
            {onRetry && <Button variant="ghost" className="px-0 text-small text-accent" onClick={onRetry}>{t('help.history.retry')}</Button>}
          </div>
        )}

        {rows.length === 0 && (loading ? (
          <div className="px-2.5 pt-1.5">
            <p role="status" className="m-0 text-small text-ink-faint">{t('help.history.loading')}</p>
            <SkeletonRows rows={4} />
          </div>
        ) : error ? (
          <Notice icon="failure" headline={t('help.history.failed')} body={error}>
            {onRetry && <Button onClick={onRetry}>{t('help.history.retry')}</Button>}
          </Notice>
        ) : searching ? (
          !refreshing && (
            // Every conversation was searched (ASKHIST1d1), and the hint says so: none is left out of a search.
            <Notice icon="search" headline={t('help.history.none', { words })} body={t('help.history.noneHint')}>
              <Button onClick={clear}>{t('help.history.clearSearch')}</Button>
            </Notice>
          )
        ) : (
          <Notice icon="history" headline={t('help.history.emptyHeadline')} body={t('help.history.empty')}>
            {onNew && <Button disabled={busy} onClick={onNew}>{t('help.history.start')}</Button>}
          </Notice>
        ))}

        {/* What a search covered, said as it answers: never over a newer search's wait, whose rows are the last one's. */}
        {searching && !refreshing && rows.length > 0 && (
          <p role="status" className="m-0 px-2.5 pb-1 pt-1.5 text-meta text-ink-faint wrap-anywhere">
            {t('help.history.searched', { count, words })}
          </p>
        )}

        {groups.map((group) => (
          <div key={group.id} role="group" aria-labelledby={`${headingId}-${group.id}`} className="mt-1.5 first:mt-0">
            <h4 id={`${headingId}-${group.id}`} className="m-0 truncate px-2.5 pb-1 pt-1.5 text-meta font-semibold text-ink-soft">
              {t(`help.history.group.${group.id}`)}
            </h4>
            <ul aria-labelledby={`${headingId}-${group.id}`} className="m-0 grid list-none grid-cols-[minmax(0,1fr)] gap-0.5 p-0">
              {group.rows.map((row) => (
                <HistoryRow
                  key={row.session}
                  row={row}
                  current={row.session === shown}
                  busy={busy}
                  words={searching ? words : ''}
                  asking={asking?.id === row.session ? asking.what : null}
                  onAsk={(what) => setAsking(what ? { id: row.session, what } : null)}
                  onOpen={(id) => acts.onOpen(id, scroller.current?.scrollTop ?? 0)}
                  onRename={acts.onRename}
                  onPin={acts.onPin}
                  onStartFrom={acts.onStartFrom}
                  onDelete={acts.onDelete}
                />
              ))}
            </ul>
          </div>
        ))}

        {onMore && rows.length > 0 && (
          <div ref={moreFoot} className="grid grid-cols-[minmax(0,1fr)] justify-items-start gap-1.5 px-2.5 pt-2">
            <p className="m-0 text-meta text-ink-faint">{t('help.history.listed', { shown: rows.length, total: count })}</p>
            {moreError && <Refused sentence={moreError} />}
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <Button className="px-2.5 text-small" onClick={more}>{t('help.history.more')}</Button>
              {/* There from the first, so a reader hears it speak when the wait begins. */}
              <span role="status" className="text-small text-ink-soft">{loadingMore ? t('help.history.loadingMore') : ''}</span>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

/** A state the list is in, said as an empty state is (UX §4): its glyph, one line, one more, and what changes it. */
function Notice({ icon, headline, body, children }: { icon: IconName; headline: string; body: string; children?: ReactNode }) {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)] justify-items-center gap-1.5 px-3 py-6 text-center text-ink-faint">
      <Icon name={icon} size={24} />
      <p className="m-0 mt-1 text-body font-semibold text-ink wrap-anywhere">{headline}</p>
      <p className="m-0 text-small text-ink-soft wrap-anywhere"><Inline text={body} /></p>
      {children && <div className="mt-2">{children}</div>}
    </div>
  );
}

/** Words with what a search found in them marked, as the rail marks its found words. */
function Marked({ text, words }: { text: string; words: string }) {
  if (!words) return <>{text}</>;
  return (
    <>
      {marked(text, words).map((part, index) => (part.match
        ? <mark key={index} className="-mx-0.5 rounded-sm bg-accent-soft px-0.5 text-ink">{part.text}</mark>
        : <Fragment key={index}>{part.text}</Fragment>))}
    </>
  );
}

function HistoryRow({ row, current, busy, words, asking, onAsk, onOpen, onRename, onPin, onStartFrom, onDelete }: {
  row: HelpConversationRow;
  current: boolean;
  busy: boolean;
  /** What a search found it by, to mark; empty when nothing is searched. */
  words: string;
  asking: 'rename' | 'delete' | null;
  onAsk: (what: 'rename' | 'delete' | null) => void;
  onOpen: (id: string) => void;
} & Omit<AskHistoryActs, 'onOpen'>) {
  const { t } = useTranslation();
  const trigger = useRef<HTMLButtonElement>(null);
  // Where a search found its words: in the title, which is then marked, or in what was said, which takes the line's place.
  // Its line is parsed whole and then cut from a little before the words (ASKHIST1d2); a driver before pages sent only the
  // 60 characters around them, already cut.
  const inTitle = words !== '' && row.found === row.title;
  const find = words === '' || inTitle ? null
    : row.foundLine ? rowLine(previewText(row.foundLine), words)
      : row.found ? previewText(row.found) : null;
  const about = row.about ? rowLine(previewText(row.about)) : null;
  const marks = [
    ago(row.last),
    row.live ? t('help.history.running') : null,
    row.from ? t('help.history.fromEarlier') : null,
  ].filter((mark): mark is string => mark !== null);

  const acts: MenuAct[] = [
    { id: 'rename', label: t('help.history.rename'), icon: 'edit', disabled: busy, onSelect: () => onAsk('rename') },
    {
      id: 'pin', label: t(row.pinned ? 'help.history.unpin' : 'help.history.pin'), icon: 'pin', disabled: busy,
      onSelect: () => onPin(row.session, !row.pinned),
    },
    { id: 'startFrom', label: t('help.history.startFrom'), icon: 'plus', disabled: busy, onSelect: () => onStartFrom(row.session) },
    // Only an ended conversation's record goes: a live one is finished or stopped first.
    ...(row.live ? [] : [{
      id: 'delete', label: t('help.history.delete'), icon: 'remove' as const, disabled: busy, onSelect: () => onAsk('delete'),
    }]),
  ];

  // A rename put down gives the focus back to the ⋯ it was opened from.
  const putDown = () => {
    trigger.current?.focus();
    onAsk(null);
  };

  return (
    <li
      data-list-row=""
      className={cn(
        'grid grid-cols-[minmax(0,1fr)_auto] items-start rounded-control border-l-[3px] transition-colors duration-(--speed)',
        current ? 'border-l-accent bg-accent-soft' : 'border-l-transparent hover:bg-accent-soft/50',
      )}
    >
      <button
        type="button"
        data-session={row.session}
        aria-current={current || undefined}
        onClick={() => onOpen(row.session)}
        className="block min-w-0 px-2.5 py-1.5 text-left"
      >
        {/* The title whole, broken inside a word only where one will not fit, so two pasted URLs read apart (UX §4). */}
        <span className="block text-body text-ink wrap-anywhere">
          {inTitle ? <Marked text={row.title} words={words} /> : row.title}
          {current && (
            <span className="ml-1.5 inline-block rounded-[3px] border border-line-strong px-1 align-[1px] text-meta leading-normal text-ink-soft">
              {t('help.history.current')}
            </span>
          )}
        </span>
        {(find ?? about) && (
          <span className="mt-0.5 line-clamp-2 wrap-anywhere text-small text-ink-soft">
            {find ? <Marked text={find} words={words} /> : about}
          </span>
        )}
        <span className="mt-0.5 flex flex-wrap items-center gap-x-1.5 text-meta text-ink-faint">
          {row.pinned && (
            <span className="inline-flex items-center text-ink-soft">
              <Icon name="pin" size={11} />
              <span className="sr-only">{t('help.history.pinned')}</span>
            </span>
          )}
          <span className="min-w-0 wrap-anywhere">{marks.join(' · ')}</span>
        </span>
      </button>

      {/* Beside the row, never inside it: a button inside a button is not a thing a page may hold. */}
      <Menu.Root>
        <Menu.Trigger asChild>
          <button
            ref={trigger}
            type="button"
            aria-label={t('help.history.menu', { title: row.title })}
            className={cn(
              'mr-1 mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-control text-ink-faint',
              'hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
            )}
          >
            <Icon name="more" size={14} />
          </button>
        </Menu.Trigger>
        <Menu.Content side="bottom" align="end" highlight="accent" className="min-w-44">
          <Menu.Acts acts={acts} />
        </Menu.Content>
      </Menu.Root>

      {asking === 'rename' && (
        <div className="col-span-2">
          <RenameHelp row={row} onSave={(name, answered) => onRename(row.session, name, answered)} onClose={putDown} />
        </div>
      )}

      {asking === 'delete' && (
        <InlineConfirm
          className="col-span-2 mx-2.5 my-1"
          label={t('help.history.deleteLabel', { title: row.title })}
          // The title is said whole, so a word too long for the row breaks inside it rather than running past the dock.
          says={<span className="wrap-anywhere">{t('help.history.deleteSays', { title: row.title })}</span>}
          meanIt={t('help.history.deleteMeanIt')}
          busy={busy}
          onConfirm={(answered) => onDelete(row.session, answered)}
          onClose={() => onAsk(null)}
        />
      )}
    </li>
  );
}

/** The keys a field moves its caret by, which the list would otherwise take for its rows. */
const FIELD_KEYS = new Set(['ArrowDown', 'ArrowUp', 'Home', 'End']);

/**
 * A conversation's name, set where its row is (ASKHIST1, as ASKHIST1c made it): it starts from the title shown, selected, so
 * typing replaces it; the field the row's whole width above presses that wrap; *Use the original question* for a named one,
 * since a name cleared to nothing was a press nobody could read; its wait said aloud, and no Escape while it waits; and the
 * terminal's twin under it (D50).
 */
function RenameHelp({ row, onSave, onClose }: {
  row: HelpConversationRow;
  onSave: (name: string | null, answered: Answered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  // A title longer than a name may be (a first question) starts cut to the longest name.
  const start = row.title.slice(0, HELP_NAME_LIMIT);
  const [name, setName] = useState(start);
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const field = useRef<HTMLInputElement>(null);
  const kept = name.trim();
  const unchanged = kept === start.trim();
  // Named, it can be called by its first question again; unnamed, it already is.
  const original = row.name !== null && row.opening !== null;

  useEffect(() => {
    field.current?.focus();
    field.current?.select();
  }, []);

  const save = (value: string | null) => {
    setRefusal(null);
    setPending(true);
    onSave(value, {
      done: () => { setPending(false); onClose(); },
      refused: (sentence) => { setPending(false); setRefusal(sentence); },
    });
  };

  return (
    <form
      aria-label={t('help.history.renameTitle', { title: row.title })}
      className="mx-2.5 mb-1.5 mt-0.5 grid grid-cols-[minmax(0,1fr)] gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (!pending && kept && !unchanged) save(kept);
      }}
      onKeyDown={(event) => {
        // The field's own keys: the caret's, never the list's rows.
        if (FIELD_KEYS.has(event.key)) { event.stopPropagation(); return; }
        if (isComposing(event) || event.key !== 'Escape') return;
        event.preventDefault();
        event.stopPropagation();
        // While it saves, Escape changes nothing: the answer is on its way.
        if (!pending) onClose();
      }}
    >
      <input
        ref={field}
        value={name}
        readOnly={pending}
        maxLength={HELP_NAME_LIMIT}
        aria-label={t('help.history.name')}
        onChange={(event) => setName(event.target.value.replace(/[\r\n]+/g, ' '))}
        spellCheck={false}
        autoComplete="off"
        className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2 py-1 text-small text-ink outline-none focus:border-accent read-only:text-ink-soft"
      />
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" variant="primary" disabled={pending || unchanged || !kept}>{t('help.history.save')}</Button>
        {original && (
          <Tip content={t('help.command', { command: `daoris-driver help rename ${row.session} --clear` })}>
            <Button disabled={pending} onClick={() => save(null)}>{t('help.history.useOriginal')}</Button>
          </Tip>
        )}
        <Button variant="ghost" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
        {/* There from the first, so a reader hears it speak when the wait begins. */}
        <span role="status" className="text-small text-ink-soft">{pending ? t('help.history.saving') : ''}</span>
      </div>
      {refusal && <Refused sentence={refusal} />}
      {kept && (
        // `min-w-0`: a grid item is otherwise as wide as its longest word, and a code word breaks only inside its line.
        <span className="min-w-0 text-meta text-ink-faint wrap-anywhere">
          <Inline text={t('help.command', { command: `daoris-driver help rename ${row.session} ${shellWord(kept, '<name>')}` })} />
        </span>
      )}
    </form>
  );
}
