import { useTranslation } from 'react-i18next';
import { useAsks, useQuests, useRegistry, useSessions } from '../queries';
import { Card, CardHeader, Tip } from '../ui';
import { type Attention, AttentionRow } from './AttentionRow';
import { needsAPerson } from './attention';

/**
 * Where each kind of row leads, when it leads anywhere. A kind with no door here is shown, and is
 * not a button: a parked session in a browser has no Sessions to open in.
 */
export type AttentionDoors = Partial<Record<Attention['kind'], (item: Attention) => void>>;

/**
 * Overview's **what needs you** band (design §4).
 *
 * @remarks
 * **Overview keeps the landing** (D40 — "is anything sitting" is still the first question) and
 * gains this one band. Continuous monitoring is the reported source of fatigue and no-visibility
 * sessions showed 3× the abandonment at identical output quality (research §2), so visibility is
 * load-bearing and *watching* is not the mechanism: the band is what a person checks instead of
 * watching, and every row is a door wherever its destination exists.
 *
 * **It renders nothing when nothing is waiting** — not an empty card. A band that was always there
 * saying "all clear" is a band people stop reading, and the answer to "is anything sitting" is
 * already the tiles above it.
 */
export function AttentionBand({ doors = {} }: { doors?: AttentionDoors }) {
  const { t } = useTranslation();
  const sessions = useSessions(null, false);
  const quests = useQuests(null, false);
  const registry = useRegistry();
  // The asks still waiting (INT4d) — the Quests view's own list, so one fetch serves both.
  const asks = useAsks(false);

  const waiting = needsAPerson(
    sessions.data ?? [], quests.data ?? [], registry.data ?? [], asks.data ?? []);
  if (waiting.length === 0) return null;

  return (
    <section aria-label={t('work.attention.title')}>
      <Card warn className="mb-5 px-0 py-0">
        <div className="px-[1.15rem] pb-1 pt-3.5">
          <CardHeader
            title={
              <Tip content={t('work.attention.hint')}>
                <span>{t('work.attention.title')}</span>
              </Tip>
            }
            aside={
              <span className="font-mono text-meta tabular-nums text-st-open">{waiting.length}</span>
            }
          />
        </div>
        <ul className="m-0 list-none p-0 pb-2">
          {waiting.map((item) => (
            <AttentionRow key={`${item.kind}:${item.id}`} item={item} onOpen={doors[item.kind]} />
          ))}
        </ul>
        <p className="m-0 px-[1.15rem] pb-3 text-meta text-ink-faint">
          {t('work.attention.reviewNote')}
        </p>
      </Card>
    </section>
  );
}
