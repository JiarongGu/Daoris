import { useTranslation } from 'react-i18next';
import type { Ask, AskState } from '../api';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { Card, Icon, Pill } from '../ui';

/**
 * An ask's state as a pill tone: waiting on a person while it is asked or proposed — a proposal is a
 * person's to accept (INT4a) — done once it became quests, quiet once closed. Exhaustive at compile
 * time, as `QUEST_TONE` is.
 */
export const ASK_TONE: Record<AskState, 'open' | 'done' | 'neutral'> = {
  Open: 'open',
  Proposed: 'open',
  Published: 'done',
  Closed: 'neutral',
};

/** The ask's first line — what a card and a drawer's title show; the whole sentence is the record's. */
export const firstLine = (sentence: string) => sentence.split('\n', 1)[0].trim();

/**
 * Which tier answered, in the words for it (`model-decoupling`: said, never implied) — the record's
 * sentence, or the card's short form. A tier this page has no word for — INT4b's session, or anything
 * after it — is shown as the service wrote it rather than as a blank or a guess.
 */
export function tierWords(
  t: (key: string, options?: Record<string, unknown>) => string, tier: string, form: 'sentence' | 'short' = 'sentence',
) {
  return t(form === 'short' ? `asks.tierShort.${tier}` : `asks.tier.${tier}`, { defaultValue: tier });
}

/**
 * One ask, as a summary and a door (INT4c) — the Quests view's card shape, because an ask is where
 * quests come from: state beside the first line, then its circle, what answered it, and what became
 * of it. Props only (components §2).
 */
export function AskCard({ ask, onOpen }: { ask: Ask; onOpen: (ask: Ask) => void }) {
  const { t } = useTranslation();
  const links = ask.links.length;
  const files = ask.attachments.length;
  const carried = [
    links > 0 && t('quests.card.links', { count: links }),
    files > 0 && t('quests.card.files', { count: files }),
  ].filter(Boolean).join(' · ');
  const became = ask.quests.map((id) => `#${id.slice(0, 6)}`).join(', ');
  const proposed = ask.proposal.map((match) => match.repository).join(', ');

  return (
    <Card className={cn(
      'mb-3.5 cursor-pointer transition-colors duration-(--speed) hover:border-line-strong hover:border-l-accent',
      ask.state === 'Closed' && 'opacity-75',
    )}>
      <div
        role="button" tabIndex={0}
        onClick={() => onOpen(ask)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onOpen(ask); }
        }}
      >
        <header className="flex items-baseline gap-2">
          <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
          <span className="min-w-0 flex-1 truncate text-body font-semibold">{firstLine(ask.sentence)}</span>
          {carried && (
            <span aria-label={carried} title={carried} className="flex shrink-0 items-center gap-1 font-mono text-meta text-ink-faint">
              {links > 0 && <><Icon name="link" size={12} />{links}</>}
              {files > 0 && <><Icon name="attach" size={12} />{files}</>}
            </span>
          )}
        </header>
        <p className="mt-1 text-body text-accent">
          {ask.workspace}
          <span className="font-mono text-meta text-ink-faint">
            {' '}· {tierWords(t, ask.tier, 'short')} · {t('asks.card.asked', { ago: ago(ask.asked) })}
            {became && <> · {t('asks.card.became', { quests: became })}</>}
            {!became && proposed && ask.state !== 'Closed' && <> · {t('asks.card.proposed', { repositories: proposed })}</>}
          </span>
        </p>
      </div>
    </Card>
  );
}
