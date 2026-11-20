import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ExternalLink } from '../../design/links';
import { Button, cardFrame } from '../../design/ui';
import { clockOf, phaseTime } from '../../locales/format';
import { cn } from '../../lib/cn';

/** A press and what happens after it, in the asker's words: never what the button is called alone. */
export type WaitEffect = { label: string; effect: string };

/**
 * One wait on the person, as its asker filed it: a title the person can answer, what it is, what yes and no (or each option) do,
 * where to look, how it is known, who asked on which model and when, and what goes on meanwhile. Every word but the frame's is
 * the asker's, drawn as written.
 */
export type WaitCardFacts = {
  id: string;
  title: string;
  kind: string;
  /** When its asker filed it (ISO): the card's age. */
  since: string;
  /** How many sessions or quests wait on it. */
  holds?: number;
  what: string;
  yes?: WaitEffect;
  no?: WaitEffect;
  /** A choice of more than two, each saying what it does; drawn in place of yes and no. */
  options?: WaitEffect[];
  look: { label: string; href?: string; onOpen?: () => void }[];
  known: string[];
  from: { role: string; agent: string; model: string | null; at: string };
  meanwhile?: string | null;
  /**
   * Where its answer takes more than a press (a landing's gates, a step's *Try again*, a sign-in, a park's *Finish…*): its own
   * presses, drawn in place of the yes and no, and beside a question's options.
   */
  presses?: ReactNode;
  /** The subagent that raised it: its name where the record holds it, and the way into its focus. */
  by?: { name: string | null; open?: () => void };
};

/** What a press answered: yes, no, or an option by its place. */
export type WaitAnswer = 'yes' | 'no' | { option: number };

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[5.5rem_minmax(0,1fr)] gap-x-2">
      <dt className="text-small text-ink-faint">{label}</dt>
      <dd className="m-0 min-w-0 text-body text-ink">{children}</dd>
    </div>
  );
}

function Unsaid() {
  const { t } = useTranslation();
  return <span className="text-small italic text-ink-faint">{t('work.session.wait.unsaid')}</span>;
}

/**
 * **The wait card**: every wait the session holds on the person, drawn one way (design §5.3). Whole, it says what it is, what each
 * press does, where to look, how it is known, who asked and on which model, and answers with one press; folded to a line (`line`), it
 * names itself, how much it holds and its age, and opens on a press. A field its asker left out reads "not said by its asker".
 * While a press's answer is on its way, its presses wait, so a second press never sends a second answer.
 */
export function WaitCard({ wait, now, onAnswer, layout = 'whole', onOpen }: {
  wait: WaitCardFacts;
  now: number;
  onAnswer: (answer: WaitAnswer) => void | Promise<unknown>;
  layout?: 'whole' | 'line';
  onOpen?: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [pending, setPending] = useState<string | null>(null);
  const busy = pending === wait.id;
  const answer = (given: WaitAnswer) => {
    setPending(wait.id);
    void Promise.resolve(onAnswer(given)).finally(() => setPending((was) => (was === wait.id ? null : was)));
  };
  const age = phaseTime(now - Date.parse(wait.since));
  const facts = [wait.holds ? t('work.session.wait.holds', { count: wait.holds }) : null, age].filter(Boolean).join(' · ');
  if (layout === 'line') {
    return (
      <button
        type="button"
        data-wait={wait.id}
        onClick={onOpen}
        className="flex w-full min-w-0 cursor-pointer items-center gap-2 rounded-control border border-line bg-raised px-3 py-1.5 text-left hover:border-accent"
      >
        <span aria-hidden="true" className="text-you-ink">◆</span>
        <span className="min-w-0 flex-1 truncate text-body text-ink">{wait.title}</span>
        <span className="shrink-0 text-caption text-ink-faint">{facts}</span>
      </button>
    );
  }
  const options = wait.options ?? [];
  return (
    <article
      aria-label={wait.title}
      data-wait={wait.id}
      tabIndex={-1}
      className={cn('grid gap-2', cardFrame('wait'), 'outline-none focus:outline-2 focus:outline-offset-2 focus:outline-accent/60')}
    >
      <header className="flex min-w-0 items-baseline gap-2">
        <span aria-hidden="true" className="text-you-ink">◆</span>
        <h3 className="m-0 min-w-0 flex-1 text-heading font-semibold text-ink">{wait.title}</h3>
        <span className="shrink-0 text-caption text-ink-faint">{facts}</span>
      </header>
      {wait.by && (
        <p data-wait-child className="m-0 flex min-w-0 items-center gap-x-2 text-small text-ink-faint">
          <span className="min-w-0 truncate">{wait.by.name ? t('work.wait.child', { name: wait.by.name }) : t('work.wait.childUnheld')}</span>
          {wait.by.open && (
            <button type="button" aria-label={t('work.wait.childOpen', { name: wait.by.name })} onClick={wait.by.open} className="shrink-0 cursor-pointer border-0 bg-transparent p-0 text-small text-accent-ink underline">
              {t('work.band.open')}
            </button>
          )}
        </p>
      )}
      <p className="m-0 whitespace-pre-line text-body text-ink">{wait.what}</p>
      <dl className="m-0 grid gap-1">
        {options.length > 0
          ? options.map((option, index) => (
            <Row key={option.label} label={`${index + 1}`}>{option.effect}</Row>
          ))
          : wait.presses ? null : (
            <>
              <Row label={t('work.session.wait.yes')}>{wait.yes?.effect ?? <Unsaid />}</Row>
              <Row label={t('work.session.wait.no')}>{wait.no?.effect ?? <Unsaid />}</Row>
            </>
          )}
        <Row label={t('work.session.wait.look')}>
          {wait.look.length === 0 ? <Unsaid /> : (
            <span className="flex flex-wrap gap-x-3">
              {wait.look.map((door) => (door.href
                ? <ExternalLink key={door.label} href={door.href} className="text-accent-ink underline">{door.label} ↗</ExternalLink>
                : (
                  <button key={door.label} type="button" onClick={door.onOpen} className="cursor-pointer border-0 bg-transparent p-0 text-accent-ink underline">
                    {door.label}
                  </button>
                )))}
            </span>
          )}
        </Row>
        <Row label={t('work.session.wait.known')}>
          {wait.known.length === 0 ? <Unsaid /> : (
            <span className="flex flex-wrap gap-x-3">
              {wait.known.map((fact) => <span key={fact} className="text-ok-ink">✓ {fact}</span>)}
            </span>
          )}
        </Row>
        <Row label={t('work.session.wait.from')}>
          {[wait.from.role, wait.from.agent, wait.from.model ?? t('work.session.head.notReported'),
            t('work.session.wait.asked', { time: clockOf(wait.from.at, new Date(now), i18n.language) })].join(' · ')}
        </Row>
        {wait.meanwhile && <Row label={t('work.session.wait.meanwhile')}>{wait.meanwhile}</Row>}
      </dl>
      {wait.presses && <div data-presses className="grid min-w-0 gap-2">{wait.presses}</div>}
      <div data-answer className="flex flex-wrap items-center gap-2">
        {options.length > 0
          ? options.map((option, index) => (
            <Button key={option.label} variant={index === 0 ? 'primary' : 'default'} disabled={busy} onClick={() => answer({ option: index })}>
              {option.label}
            </Button>
          ))
          : wait.presses ? null : (
            <>
              {wait.yes && <Button variant="primary" disabled={busy} onClick={() => answer('yes')}>{wait.yes.label}</Button>}
              {wait.no && <Button disabled={busy} onClick={() => answer('no')}>{wait.no.label}</Button>}
            </>
          )}
        {onOpen && <Button variant="ghost" className={cn('ml-auto')} onClick={onOpen}>{t('work.session.wait.open')}</Button>}
      </div>
    </article>
  );
}
