import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { Button, Icon, Inline, Menu, type MenuAct } from '../ui';
import { type Answered, InlineConfirm, Refused } from '../work/InlineConfirm';
import { ListRowDoor } from '../work/ListPane';
import { HELP_NAME_LIMIT, type HelpConversationRow } from './history';

/** What a row of the history may do, each pressed through the organism that holds the bridge. */
export type AskHistoryActs = {
  /** Show it in the panel: one that ended goes on in itself with the next words, where it can. */
  onOpen: (id: string) => void;
  /** Name it, or give it its first question back with null; told how it ended. */
  onRename: (id: string, name: string | null, answered: Answered) => void;
  onPin: (id: string, pinned: boolean) => void;
  /** A new conversation handed its words. */
  onStartFrom: (id: string) => void;
  /** Delete it, asked once; told how it ended. */
  onDelete: (id: string, answered: Answered) => void;
};

/**
 * Ask Daoris's history (ASKHIST1): its conversations, kept on this machine only, pinned first and then the newest, each with
 * its title (its name, or its first question), when it was last spoken in and a line of what it was about; a search by words
 * across them; and each one's acts, a rename and a delete asked where they were pressed.
 *
 * @remarks
 * A molecule: the rows arrive made, the search's words go out, and each act goes to the organism (`AskConversation`). A row is
 * a door to its conversation, which the panel then shows; its ⋯ holds what has no other home (D76's RAIL1 rule). A live one is
 * offered no delete, since only an ended conversation's record goes.
 */
export function AskHistory({
  rows, cut = false, loading = false, search, shown = null, busy = false, refusal = null, onSearch, ...acts
}: {
  rows: HelpConversationRow[];
  /** Why the last act pressed from a row's menu (a pin, a new conversation from it) did not happen; null when it did. */
  refusal?: string | null;
  /** Whether older conversations were left out of the list. */
  cut?: boolean;
  /** Its first answer is on its way. */
  loading?: boolean;
  /** The search's words, as typed. */
  search: string;
  /** The conversation the panel shows, which its row says. */
  shown?: string | null;
  /** An act on one is on its way: the others wait for it. */
  busy?: boolean;
  onSearch: (words: string) => void;
} & AskHistoryActs) {
  const { t } = useTranslation();
  // The one row whose question is open: its rename, or its delete.
  const [asking, setAsking] = useState<{ id: string; what: 'rename' | 'delete' } | null>(null);
  const searching = search.trim().length > 0;

  return (
    <section aria-label={t('help.history.title')} className="grid gap-2">
      <p className="m-0 text-meta text-ink-faint">{t('help.history.kept')}</p>
      {refusal && <Refused sentence={refusal} />}
      <label className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2 py-1 text-ink-faint focus-within:border-accent">
        <Icon name="search" size={12} className="shrink-0" />
        <input
          type="search"
          value={search}
          aria-label={t('help.history.search.label')}
          placeholder={t('help.history.search.placeholder')}
          onChange={(event) => onSearch(event.target.value)}
          // Escape clears a search, and one with nothing in it is left to what holds the panel.
          onKeyDown={(event) => { if (event.key === 'Escape' && search) { event.preventDefault(); onSearch(''); } }}
          className="min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
        />
      </label>

      {rows.length === 0 && !loading && (
        <p className="m-0 text-small text-ink-faint">
          {searching ? t('help.history.none', { words: search.trim() }) : t('help.history.empty')}
        </p>
      )}

      {rows.length > 0 && (
        <ul aria-label={t('help.history.title')} className="m-0 grid list-none gap-0.5 p-0">
          {rows.map((row) => (
            <HistoryRow
              key={row.session}
              row={row}
              shown={row.session === shown}
              busy={busy}
              asking={asking?.id === row.session ? asking.what : null}
              onAsk={(what) => setAsking(what ? { id: row.session, what } : null)}
              {...acts}
            />
          ))}
        </ul>
      )}

      {cut && <p className="m-0 text-meta text-ink-faint">{t('help.history.cut', { count: rows.length })}</p>}
    </section>
  );
}

function HistoryRow({ row, shown, busy, asking, onAsk, onOpen, onRename, onPin, onStartFrom, onDelete }: {
  row: HelpConversationRow;
  shown: boolean;
  busy: boolean;
  asking: 'rename' | 'delete' | null;
  onAsk: (what: 'rename' | 'delete' | null) => void;
} & AskHistoryActs) {
  const { t } = useTranslation();
  const marks = [
    ago(row.last),
    row.pinned ? t('help.history.pinned') : null,
    row.live ? t('help.history.running') : row.resumable ? null : t('help.history.startsAnew'),
    row.from ? t('help.history.fromEarlier') : null,
  ].filter((mark): mark is string => mark !== null);

  const acts: MenuAct[] = [
    { id: 'rename', label: t('help.history.rename'), icon: 'edit', disabled: busy, onSelect: () => onAsk('rename') },
    {
      id: 'pin', label: t(row.pinned ? 'help.history.unpin' : 'help.history.pin'), disabled: busy,
      onSelect: () => onPin(row.session, !row.pinned),
    },
    { id: 'startFrom', label: t('help.history.startFrom'), icon: 'plus', disabled: busy, onSelect: () => onStartFrom(row.session) },
    // Only an ended conversation's record goes: a live one is finished or stopped first.
    ...(row.live ? [] : [{
      id: 'delete', label: t('help.history.delete'), icon: 'remove' as const, disabled: busy, onSelect: () => onAsk('delete'),
    }]),
  ];

  return (
    <li className="group relative">
      <ListRowDoor chosen={shown} onPress={() => onOpen(row.session)}>
        <span title={row.title} className="block truncate pr-6 text-body text-ink">{row.title}</span>
        <span className="block truncate text-meta text-ink-faint">{marks.join(' · ')}</span>
        {row.about && <span className="block truncate text-small text-ink-soft">{row.about}</span>}
        {row.found && row.found !== row.title && (
          <span className="block truncate text-small text-ink-faint">
            <Inline text={row.found} />
          </span>
        )}
      </ListRowDoor>

      {/* Beside the row, never inside it: a button inside a button is not a thing a page may hold. */}
      <Menu.Root>
        <Menu.Trigger asChild>
          <button
            type="button"
            aria-label={t('help.history.menu', { title: row.title })}
            className={cn(
              'absolute right-1.5 top-1.5 flex h-5 w-5 items-center justify-center rounded-control text-ink-faint',
              'bg-raised opacity-0 transition-opacity duration-(--speed) hover:text-ink',
              'focus-visible:opacity-100 group-hover:opacity-100 data-[state=open]:opacity-100',
            )}
          >
            <Icon name="more" size={13} />
          </button>
        </Menu.Trigger>
        <Menu.Content side="bottom" align="end" highlight="accent" className="min-w-44">
          <Menu.Acts acts={acts} />
        </Menu.Content>
      </Menu.Root>

      {asking === 'rename' && (
        <RenameHelp
          row={row}
          onSave={(name, answered) => onRename(row.session, name, answered)}
          onClose={() => onAsk(null)}
        />
      )}

      {asking === 'delete' && (
        <InlineConfirm
          className="mx-2.5 my-1"
          label={t('help.history.deleteLabel', { title: row.title })}
          says={t('help.history.deleteSays', { title: row.title })}
          meanIt={t('help.history.deleteMeanIt')}
          busy={busy}
          onConfirm={(answered) => onDelete(row.session, answered)}
          onClose={() => onAsk(null)}
        />
      )}
    </li>
  );
}

/**
 * A conversation's name, set where its row is (ASKHIST1): one line, a title's length, with the terminal's twin beside it
 * (D50). Saved empty, the conversation is called by its first question again.
 */
function RenameHelp({ row, onSave, onClose }: {
  row: HelpConversationRow;
  onSave: (name: string | null, answered: Answered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(row.name ?? '');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const kept = name.trim();
  const unchanged = kept === (row.name ?? '');

  return (
    <form
      aria-label={t('help.history.renameTitle', { title: row.title })}
      className="mx-2.5 my-1 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
      onSubmit={(event) => {
        event.preventDefault();
        setRefusal(null);
        setPending(true);
        onSave(kept || null, {
          done: () => { setPending(false); onClose(); },
          refused: (sentence) => { setPending(false); setRefusal(sentence); },
        });
      }}
      onKeyDown={(event) => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); onClose(); } }}
    >
      <input
        autoFocus
        value={name}
        maxLength={HELP_NAME_LIMIT}
        aria-label={t('help.history.name')}
        placeholder={row.opening ?? row.title}
        onChange={(event) => setName(event.target.value.replace(/[\r\n]+/g, ' '))}
        spellCheck={false}
        autoComplete="off"
        className="min-w-0 flex-1 basis-40 rounded-control border border-line-strong bg-raised px-2 py-1 text-small text-ink outline-none focus:border-accent"
      />
      <Button type="submit" variant="primary" disabled={pending || unchanged}>{t('help.history.save')}</Button>
      <Button variant="ghost" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
      {refusal && <Refused sentence={refusal} />}
      <span className="basis-full text-meta text-ink-faint [overflow-wrap:anywhere]">
        <Inline text={t('help.command', { command: kept ? `daoris-driver help rename ${row.session} "${kept}"` : `daoris-driver help rename ${row.session} --clear` })} />
      </span>
    </form>
  );
}
