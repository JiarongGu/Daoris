import { useTranslation } from 'react-i18next';
import type { Ask, AskState, Session } from '../api';
import { ago } from '../format';
import { Dot, Pill, RecordCard } from '../ui';
import { CarriedCount } from '../compose/carry';

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
 * sentence, or the card's short form. A tier this page has no word for is shown as the service wrote
 * it rather than as a blank or a guess. Every tier the service writes has words in both catalogues,
 * held from the service's side (`AskTierCatalogueTests`), since that side decides which exist.
 */
export function tierWords(
  t: (key: string, options?: Record<string, unknown>) => string, tier: string, form: 'sentence' | 'short' = 'sentence',
) {
  return t(form === 'short' ? `asks.tierShort.${tier}` : `asks.tier.${tier}`, { defaultValue: tier });
}

/** The intake states in which an ask is its intake's to answer, not yet the person's (D65 §1b). */
const INTAKE_READING: ReadonlySet<Session['state']> = new Set(['queued', 'starting', 'working']);

/**
 * One ask, as a summary and a door (INT4c) — the Quests view's card shape, because an ask is where
 * quests come from: state beside the first line, then its workspace, what answered it, and what
 * became of it. Props only (components §2).
 *
 * @remarks
 * **It says what it waits for, as the band does** (INT4d, POLISH4): an intake reading it, or an
 * intake that parked asking the person. Without that, five cards read "proposed" alike while one was
 * somebody else's to answer and one was waiting on the reader. Its place is named as a workspace,
 * because a bare name beside `game → engine` reads as one more repository.
 */
export function AskCard({ ask, intake = null, onOpen }: {
  ask: Ask;
  /** Its intake session's state, when one served it and the page holds that record. */
  intake?: Session['state'] | null;
  onOpen: (ask: Ask) => void;
}) {
  const { t } = useTranslation();
  const became = ask.quests.map((id) => `#${id.slice(0, 6)}`).join(', ');
  const proposed = ask.proposal.map((match) => match.repository).join(', ');
  const live = ask.state === 'Open' || ask.state === 'Proposed';
  const reading = live && intake !== null && INTAKE_READING.has(intake);
  const askedYou = live && intake === 'awaiting-person';

  return (
    <RecordCard closed={ask.state === 'Closed'} onOpen={() => onOpen(ask)}>
      <header className="flex items-baseline gap-2">
        <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
        <span className="min-w-0 flex-1 truncate text-body font-semibold">{firstLine(ask.sentence)}</span>
        <CarriedCount links={ask.links.length} files={ask.attachments.length} />
      </header>
      <p className="mt-1 text-body text-accent">
        {t('asks.card.workspace', { workspace: ask.workspace })}
        <span className="font-mono text-meta text-ink-faint">
          {' '}· {tierWords(t, ask.tier, 'short')} · {t('asks.card.asked', { ago: ago(ask.asked) })}
          {became && <> · {t('asks.card.became', { quests: became })}</>}
          {!became && proposed && ask.state !== 'Closed' && <> · {t('asks.card.proposed', { repositories: proposed })}</>}
          {reading && <> · {t('asks.card.intakeReading')}</>}
        </span>
      </p>
      {askedYou && (
        <p className="mt-1 mb-0">
          <Dot tone="parked" label={t('work.attention.intake')} />
        </p>
      )}
    </RecordCard>
  );
}
