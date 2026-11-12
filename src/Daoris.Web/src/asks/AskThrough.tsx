import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Ask } from '../data/api';
import { ago } from '../locales/format';
import { Button } from '../design/ui';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';

/**
 * **An ask's through words**: the person's own words asking for its work to be delivered with no press of theirs, the latest
 * standing, which each run of its quests reads live and Daoris applies as their Accept where nothing else waits on them; and
 * *Take back*, the person's press that stops them reaching any run from then on.
 *
 * @remarks
 * Nothing is drawn where none stand: none given, or the latest a take-back. A molecule: the ask and the press arrive as props.
 */
export function AskThrough({ ask, busy = false, onTakeBack }: {
  ask: Ask;
  busy?: boolean;
  /** Take the standing words back; absent where nothing can (a browser). */
  onTakeBack?: (answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const latest = ask.throughWords?.at(-1);
  if (!latest?.words) return null;

  return (
    <div className="grid gap-1.5">
      <p className="m-0 text-body">{t('asks.record.through', { words: latest.words })}</p>
      <p className="m-0 text-meta text-ink-faint">{t('asks.record.throughSince', { ago: ago(latest.at) })}</p>
      {onTakeBack && ask.state !== 'Closed' && !asking && (
        <div>
          <Button variant="ghost" disabled={busy} className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking(true)}>
            {t('asks.record.throughTakeBack')}
          </Button>
        </div>
      )}
      {asking && onTakeBack && (
        <InlineConfirm
          tone="primary"
          block
          label={t('asks.record.throughTakeBackTitle')}
          says={t('asks.record.throughTakeBackSays')}
          meanIt={t('asks.record.throughTakeBackMeanIt')}
          onConfirm={onTakeBack}
          onClose={() => setAsking(false)}
        />
      )}
    </div>
  );
}
