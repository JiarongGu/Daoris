import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { askItem } from '../app/opener';
import { ASK_TONE } from '../asks/AskRow';
import { Icon, Pill, QUEST_TONE, SESSION_TONE } from '../design/ui';
import { heldWord } from '../quests/questLife';
import { askName, questName } from '../work/identity';
import type { PublishedCounts, PublishedItem, PublishedQuest } from './published';

/** Where a row's press takes the person: its record on Quests, a quest by its id and an ask as Quests' list names one. */
export type PublishedDoor = { view: 'quests'; item: string };

/**
 * What an Ask Daoris conversation published (ASKQUESTS1, the Ask Daoris design §9 item 12), in a strip of its own under the
 * conversation's head, outside its scroll. Folded, one line: what it is, and what stands, each count with its noun, in the soft ink
 * but for what waits on the person (D185 §3.10.2). Opened in place: each ask with the quests it became under it, and each quest
 * published on its own, the newest first, each as its own list says it, and each a press that opens its record on Quests.
 *
 * @remarks
 * **A molecule**: the rows arrive judged (`publishedItems`), and a press goes out as the door a go uses. It adds no act: every act
 * on a quest or an ask is its page's (D50). A record no longer on this machine is said so and opens nothing; one still being read
 * says so, never empty (D185 §3.8).
 */
export function PublishedList({ items, counts, failed = false, open, onToggle, onOpen }: {
  items: readonly PublishedItem[];
  counts: PublishedCounts;
  /** A list its state is read from could not be read: said, never drawn as nothing. */
  failed?: boolean;
  open: boolean;
  onToggle: () => void;
  onOpen: (door: PublishedDoor) => void;
}) {
  const { t } = useTranslation();
  const parts = summary(counts, failed, t);

  return (
    <section aria-label={t('help.published.title')} className="shrink-0 border-b border-line">
      <button
        type="button"
        aria-expanded={open}
        onClick={onToggle}
        className="flex w-full min-w-0 items-start gap-1.5 border-0 bg-transparent px-3 py-1.5 text-left hover:bg-raised"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="mt-0.5 text-ink-soft" />
        <span className="min-w-0 flex-1 wrap-anywhere">
          <span className="text-small font-semibold text-ink">{t('help.published.title')}</span>
          {parts.map((part) => (
            <span key={part.key} className={part.waits ? 'text-meta font-semibold text-ink-open' : 'text-meta text-ink-soft'}>
              {' · '}{part.words}
            </span>
          ))}
        </span>
      </button>
      {open && (
        <ul aria-label={t('help.published.list')} className="m-0 grid max-h-48 list-none gap-1 overflow-y-auto px-2 pb-2">
          {items.map((item) => (item.kind === 'ask' ? (
            <li key={item.key} className="grid gap-0.5">
              <Row
                pill={item.ask.record === 'reading' || item.ask.record === 'gone' ? null
                  : <Pill tone={ASK_TONE[item.ask.record.state]}>{t(`asks.state.${item.ask.record.state}`)}</Pill>}
                name={typeof item.ask.record === 'string' ? `#${item.ask.id}` : askName(item.ask.record)}
                kind="ask"
                meta={item.ask.record === 'reading' ? t('help.published.reading')
                  : item.ask.record === 'gone' ? t('help.published.gone')
                    : t('help.published.at', { workspace: item.ask.record.workspace })}
                waits={item.ask.waits && typeof item.ask.record !== 'string'
                  && (item.ask.record.goAheads ?? []).some((goAhead) => goAhead.state === 'asked') ? t('help.published.goAhead') : null}
                onOpen={item.ask.record === 'gone' ? undefined : () => onOpen({ view: 'quests', item: askItem(item.ask.id) })}
              />
              {item.ask.quests.length > 0 && (
                <ul className="m-0 grid list-none gap-0.5 border-l border-line pl-2 ml-3">
                  {item.ask.quests.map((quest) => (
                    <li key={quest.id}><QuestRow quest={quest} onOpen={onOpen} /></li>
                  ))}
                </ul>
              )}
            </li>
          ) : (
            <li key={item.key}><QuestRow quest={item.quest} onOpen={onOpen} /></li>
          )))}
        </ul>
      )}
    </section>
  );
}

/** One quest as its list says it: its status (or the hold that waits on the person), its name, where it goes and who works it. */
function QuestRow({ quest, onOpen }: { quest: PublishedQuest; onOpen: (door: PublishedDoor) => void }) {
  const { t } = useTranslation();
  const { record } = quest;
  if (record === 'reading' || record === 'gone') {
    return (
      <Row
        pill={null} name={`#${quest.id}`} kind="quest"
        meta={t(record === 'reading' ? 'help.published.reading' : 'help.published.gone')}
        onOpen={record === 'gone' ? undefined : () => onOpen({ view: 'quests', item: quest.id })}
      />
    );
  }

  const pill = quest.shown === 'Held'
    ? <Pill tone="open">{record.hold ? heldWord(t, record.hold) : t('quests.card.held')}</Pill>
    : <Pill tone={QUEST_TONE[record.status]}>{t(`status.${quest.shown ?? record.status}`)}</Pill>;
  const who = quest.session
    ? <Pill tone={SESSION_TONE[quest.session.state]}>{t('help.published.session', { state: t(`sessionState.${quest.session.state}`) })}</Pill>
    : quest.elsewhere ? <span className="text-meta text-ink-soft">{t('help.published.elsewhere')}</span> : null;
  return (
    <Row
      pill={pill} name={questName(record)} kind="quest"
      meta={t('help.published.to', { repository: record.to })} after={who}
      onOpen={() => onOpen({ view: 'quests', item: quest.id })}
    />
  );
}

/** A row: a press that opens its record, or, for one no longer here, the same words with nothing to open. */
function Row({ pill, name, kind, meta, waits = null, after = null, onOpen }: {
  pill: ReactNode;
  name: string;
  kind: 'ask' | 'quest';
  meta: string;
  waits?: string | null;
  after?: ReactNode;
  onOpen?: () => void;
}) {
  const { t } = useTranslation();
  const body = (
    <>
      <span className="flex min-w-0 flex-wrap items-center gap-1.5">
        {pill}
        <span className="min-w-0 text-small text-ink wrap-anywhere">{name}</span>
      </span>
      <span className="flex min-w-0 flex-wrap items-center gap-1.5 text-meta text-ink-soft">
        <span className="wrap-anywhere">{meta}</span>
        {waits && <span className="font-semibold text-ink-open">{waits}</span>}
        {after}
      </span>
    </>
  );
  if (!onOpen) return <div className="grid min-w-0 gap-0.5 rounded-control px-2 py-1 opacity-70">{body}</div>;
  return (
    <button
      type="button"
      aria-label={t(kind === 'ask' ? 'help.published.openAsk' : 'help.published.openQuest', { name })}
      onClick={onOpen}
      className="grid w-full min-w-0 gap-0.5 rounded-control border-0 bg-transparent px-2 py-1 text-left hover:bg-raised"
    >
      {body}
    </button>
  );
}

type Part = { key: string; words: string; waits?: boolean };

/** The folded line's counts, each with its noun: reading while any is, what waits on the person first, then the quests by status. */
function summary(counts: PublishedCounts, failed: boolean, t: (key: string, values?: Record<string, unknown>) => string): Part[] {
  if (failed) return [{ key: 'failed', words: t('help.published.failed') }];
  if (counts.reading) return [{ key: 'reading', words: t('help.published.reading') }];
  const parts: Part[] = [];
  if (counts.waits > 0) parts.push({ key: 'waits', words: t('help.published.waits', { count: counts.waits }), waits: true });
  if (counts.quests > 0) parts.push({ key: 'quests', words: t('help.published.quests', { count: counts.quests }) });
  for (const status of ['open', 'taken', 'done', 'declined'] as const) {
    if (counts[status] > 0) parts.push({ key: status, words: t(`help.published.${status}`, { count: counts[status] }) });
  }
  if (counts.asked > 0) parts.push({ key: 'asked', words: t('help.published.asked', { count: counts.asked }) });
  if (counts.gone > 0) parts.push({ key: 'gone', words: t('help.published.goneCount', { count: counts.gone }) });
  return parts;
}
