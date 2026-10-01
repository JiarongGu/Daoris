import { useTranslation } from 'react-i18next';
import type { Ask, AskState, Session } from '../api';
import { ago } from '../format';
import { Dot, Pill } from '../ui';
import { CarriedCount } from '../compose/carry';
import { ListRowDoor } from '../work/ListPane';

/**
 * An ask's state as a pill tone: waiting on a person while it is asked or proposed — a proposal is a
 * person's to accept (INT4a) — done once it became quests and once their work finished (USE1c), quiet
 * once closed. Exhaustive at compile time, as `QUEST_TONE` is.
 */
export const ASK_TONE: Record<AskState, 'open' | 'done' | 'neutral'> = {
  Open: 'open',
  Proposed: 'open',
  Published: 'done',
  Done: 'done',
  Closed: 'neutral',
};

/** Whether an ask is finished with — closed by the person, or done with every quest it became (USE1c). */
export const askEnded = (ask: Ask) => ask.state === 'Closed' || ask.state === 'Done';

/** The ask's first line — what a row and a page's title show; the whole sentence is the record's. */
export const firstLine = (sentence: string) => sentence.split('\n', 1)[0].trim();

/**
 * The asks as the list reads them (UX5 U32): what has waited longest first, and a closed or done ask
 * (USE1c) after every live one, as a closed quest comes after the open ones. The service lists newest
 * first, which is its terminal door's order; the group ran in it, under quests that run oldest first.
 */
export function asksInOrder(asks: readonly Ask[]): Ask[] {
  const ended = (ask: Ask) => (askEnded(ask) ? 1 : 0);
  return [...asks].sort((a, b) => ended(a) - ended(b) || a.asked.localeCompare(b.asked));
}

/**
 * Which tier answered, in the words for it (`model-decoupling`: said, never implied) — the record's
 * sentence, or the row's short form. A tier this page has no word for is shown as the service wrote
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
 * One ask as a row of Quests' list (INT4c; FRAME1d, D118 §2): state and what it carries, its first line,
 * then its workspace, what answered it, and what became of it. Its record opens in the main area. Props
 * only (components §2).
 *
 * @remarks
 * **It says what it waits for, as the band does** (INT4d, POLISH4): an intake reading it, or an
 * intake that parked asking the person. Without that, five rows read "proposed" alike while one was
 * somebody else's to answer and one was waiting on the reader. Its place is named as a workspace,
 * because a bare name beside `game → engine` reads as one more repository.
 */
export function AskRow({ ask, intake = null, chosen = false, onOpen }: {
  ask: Ask;
  /** Its intake session's state, when one served it and the page holds that record. */
  intake?: Session['state'] | null;
  /** The list has it chosen: its record is the main area. */
  chosen?: boolean;
  onOpen: (ask: Ask) => void;
}) {
  const { t } = useTranslation();
  const title = firstLine(ask.sentence);
  const became = ask.quests.map((id) => `#${id.slice(0, 6)}`).join(', ');
  const proposed = ask.proposal.map((match) => match.repository).join(', ');
  const live = ask.state === 'Open' || ask.state === 'Proposed';
  const reading = live && intake !== null && INTAKE_READING.has(intake);
  const askedYou = live && intake === 'awaiting-person';

  return (
    <li data-list-row="">
      <ListRowDoor chosen={chosen} dimmed={askEnded(ask)} onPress={() => onOpen(ask)}>
        <span className="flex items-baseline justify-between gap-2">
          <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
          <CarriedCount links={ask.links.length} files={ask.attachments.length} />
        </span>
        <span title={title} className="mt-0.5 block truncate text-body text-ink">{title}</span>
        <span className="block truncate font-mono text-meta text-ink-faint">
          <span className="text-accent">{t('asks.card.workspace', { workspace: ask.workspace })}</span>
          {' '}· {tierWords(t, ask.tier, 'short')} · {t('asks.card.asked', { ago: ago(ask.asked) })}
          {became && <> · {t('asks.card.became', { quests: became })}</>}
          {!became && proposed && !askEnded(ask) && <> · {t('asks.card.proposed', { repositories: proposed })}</>}
          {reading && <> · {t('asks.card.intakeReading')}</>}
        </span>
        {askedYou && (
          <span className="mt-0.5 block">
            <Dot tone="parked" label={t('work.attention.intake')} />
          </span>
        )}
      </ListRowDoor>
    </li>
  );
}
