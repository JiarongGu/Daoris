import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sittingSentence } from '../data/signals';
import { Button, Card, CardHeader, EmptyState, Inline, SectionTitle, Segmented, SkeletonRows } from '../design/ui';
import { type Answered, Refused } from '../work/InlineConfirm';
import { CLASSES, type QueueClass, type QueueRow, type QueueStage, placed } from './queuePlaces';

/** One class set by the card's press: the item's quest, the class, why, and an urgent one's end where the person names one. */
export type PriorityChange = { quest: string; class: QueueClass; why: string; until?: string };

/** Who set a class, in the reader's words: you, a session by its id, or the driver's word for anyone else. */
function bySaid(t: TFunction, by: string): string {
  if (by === 'the person') return t('overview.queues.byPerson');
  const session = /^session (\S+)$/.exec(by);
  return session ? t('overview.queues.bySession', { session: session[1] }) : by;
}

/** The item a row is: a quest's start, or a run's checks. */
export const itemSaid = (t: TFunction, row: QueueRow): string =>
  t(row.queue === 'checks' ? 'overview.queues.item.checks' : 'overview.queues.item.starts', { item: row.item });

/** A row's class with where it comes from: `high (aged from normal; from its ask #a, set by you: the release)`. */
export function classSaid(t: TFunction, row: QueueRow): string {
  const why = [
    ...(row.agedFrom ? [t('overview.queues.aged', { from: t(`overview.queues.class.${row.agedFrom}`, { defaultValue: row.agedFrom }) })] : []),
    ...(row.by
      ? [row.source === 'ask' && row.ask
        ? t('overview.queues.fromAsk', { ask: row.ask, by: bySaid(t, row.by), why: row.why ?? '' })
        : t('overview.queues.setBy', { by: bySaid(t, row.by), why: row.why ?? '' })]
      : []),
  ];
  const word = t(`overview.queues.class.${row.class}`);
  return why.length === 0 ? word : t('overview.queues.classWhy', { class: word, why: why.join(t('overview.queues.aheadJoin')) });
}

export const stageSaid = (t: TFunction, stage: QueueStage): string => t(`overview.queues.stage.${stage}`);

/**
 * Why a row cannot go now, without its place, which the card says beside it: a start's sitting sentence, as the quests' rows say it,
 * and a check's slot wait by its code; a code the page has no words for is said in the driver's words.
 */
export function whySaid(t: TFunction, row: QueueRow): string {
  if (row.queue === 'checks') return t(`overview.queues.why.${row.code}`, { ...row.values, defaultValue: row.reason });
  const { values } = row;
  return sittingSentence({
    quest: row.item, repository: row.repository, verdict: row.verdict ?? '', reason: row.reason,
    said: {
      code: row.code, cap: values.cap != null ? Number(values.cap) : null, session: values.session ?? null, quest: values.quest ?? null,
      repository: values.repository ?? null, ...(values.paths ? { paths: values.paths.split('\n') } : {}),
    },
  });
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** The press on one item: its class chosen, why said, and an urgent one's end, the driver's refusal said under it in its words. */
function SetPriority({ row, busy, onSet }: { row: QueueRow; busy: boolean; onSet: (change: PriorityChange, answered: Answered) => void }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [chosen, setChosen] = useState<QueueClass>(row.class);
  const [why, setWhy] = useState('');
  const [until, setUntil] = useState('');
  const [refused, setRefused] = useState<string | null>(null);
  const close = () => { setOpen(false); setWhy(''); setUntil(''); setRefused(null); };
  const item = itemSaid(t, row).replaceAll('`', '');

  if (!open) {
    return (
      <Button variant="ghost" disabled={busy} aria-label={t('overview.queues.setOf', { item })} onClick={() => { setChosen(row.class); setOpen(true); }}>
        {t('overview.queues.set')}
      </Button>
    );
  }
  return (
    <form
      className="flex basis-full flex-wrap items-center gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (busy || !why.trim()) return;
        setRefused(null);
        onSet(
          { quest: row.item, class: chosen, why: why.trim(), ...(chosen === 'urgent' && until.trim() ? { until: until.trim() } : {}) },
          { done: close, refused: setRefused },
        );
      }}
    >
      <Segmented
        label={t('overview.queues.priority')}
        value={chosen}
        options={CLASSES.map((value) => ({ value, label: t(`overview.queues.class.${value}`) }))}
        onChange={setChosen}
      />
      <input aria-label={t('overview.queues.why')} placeholder={t('overview.queues.whyHint')} value={why} className={`${FIELD} max-w-80`}
        onChange={(event) => setWhy(event.target.value)} />
      {chosen === 'urgent' && (
        <input aria-label={t('overview.queues.until')} placeholder={t('overview.queues.untilHint')} value={until} className={`${FIELD} max-w-56`}
          onChange={(event) => setUntil(event.target.value)} />
      )}
      <Button type="submit" variant="ghost" disabled={busy || !why.trim()}>{t('overview.queues.save')}</Button>
      <Button variant="ghost" disabled={busy} onClick={close}>{t('overview.queues.cancel')}</Button>
      {refused && <Refused sentence={refused} />}
    </form>
  );
}

/**
 * **Waiting in the machine's queues**: every item the one reader of the waiting items gives (`QUEUES`, which `daoris queue` prints),
 * each with its place composed by the page's twin of the comparator, its class and where it comes from, its stage, why it cannot go
 * now and the items ahead of it; one press on an item sets its priority. The page judges nothing: the press goes out, and the driver's
 * refusal is said under it in its words.
 *
 * A molecule: every state is reached by its props (`rows` undefined while not read yet), and every press goes out.
 */
export function QueuesCard({ rows, busy = false, onSet }: {
  rows: readonly QueueRow[] | undefined;
  busy?: boolean;
  onSet: (change: PriorityChange, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const spots = placed(rows ?? []);
  const queues = [...new Set(spots.map((spot) => spot.row.queue))];
  return (
    <Card>
      <CardHeader title={t('overview.queues.title')} />
      <p className="m-0 mb-2 text-small text-ink-faint"><Inline text={t('overview.queues.hint')} /></p>
      {rows === undefined && <SkeletonRows />}
      {rows !== undefined && spots.length === 0 && <EmptyState icon="check" headline={t('overview.queues.empty')} body="" />}
      {queues.map((queue) => (
        <section key={`queue:${queue}`} aria-label={t(`overview.queues.queue.${queue}`, { defaultValue: queue })}>
          <SectionTitle level={3}>{t(`overview.queues.queue.${queue}`, { defaultValue: queue })}</SectionTitle>
          <ul className="m-0 grid list-none gap-1 p-0">
            {spots.filter((spot) => spot.row.queue === queue).map(({ row, position, of, ahead }) => (
              <li key={`${queue}:${row.item}`} className="flex flex-wrap items-baseline gap-x-2 border-t border-line py-1.5 first:border-t-0">
                <span className="font-mono text-caption text-ink-faint">{t('overview.queues.place', { position, of })}</span>
                <span className="font-medium text-body"><Inline text={itemSaid(t, row)} /></span>
                <span className="text-small text-ink-soft"><Inline text={classSaid(t, row)} /></span>
                <span className="text-small text-ink-faint">{stageSaid(t, row.stage)}</span>
                <SetPriority row={row} busy={busy} onSet={onSet} />
                <span className="basis-full text-small text-ink-soft"><Inline text={whySaid(t, row)} /></span>
                {ahead.length > 0 && (
                  <span className="basis-full text-caption text-ink-faint">
                    <Inline text={t('overview.queues.ahead', {
                      items: ahead
                        .map((each) => t('overview.queues.aheadOne', { item: itemSaid(t, each), class: classSaid(t, each), stage: stageSaid(t, each.stage) }))
                        .join(t('overview.queues.aheadJoin')),
                    })} />
                  </span>
                )}
              </li>
            ))}
          </ul>
        </section>
      ))}
    </Card>
  );
}
