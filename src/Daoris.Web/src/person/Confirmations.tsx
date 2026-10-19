import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf, sentence } from '../locales/format';

/**
 * One value a press carries, as the card shows it (RPC1c, PERSONDOOR1b): its JSON pointer, its JSON type, and a string as it is
 * and anything else as its JSON, so a string "3" and a number 3 read apart.
 */
export type ConfirmationField = { name: string; type: string; value: string };

/**
 * A press that waits for the person's confirmation in this window (PERSONDOOR1f, RPC1c; D156 §4.2): one a terminal sent over the
 * pipe (`pipe`), named by its route, or one asked at the service's own confirmations (`service`), named by its door and the act the
 * host's table gives it. Never a sentence the asker wrote.
 */
export type Confirmation = {
  source: 'pipe' | 'service';
  id: string;
  /** The route's name in the census (`SESSION_ARCHIVE`), or the service door's method and pattern. */
  route: string;
  /** The act the service's table names (its own words, shown as they are); none for a terminal's press, which the page words. */
  act?: string | null;
  fields: ConfirmationField[];
  /** What sent it, as the system names the pipe's client; null where nothing can say (the service's). */
  asker?: { process?: string | null; pid?: number | null } | null;
  /** When its two minutes end, and it is refused. */
  expires: string;
};

/** How the person's answer went: answered, or no longer waiting, by the bridge's code (`gone`) or the host's own sentence. */
export type ConfirmationAnswer = { answered: true } | { answered: false; why: string | null; message: string };

/**
 * The terminal's presses the page words by their act (the census's names for them); any other is named by its route alone, which
 * the person can look up, never by a sentence the asker wrote.
 */
const WORDED = new Set([
  'SESSION_ARCHIVE', 'SESSION_DELETE', 'STOP_SESSION', 'RESOLVE_SESSION', 'SESSION_INPUT', 'LAND_SESSION_TREE', 'DISCARD_SESSION_TREE',
  'SWEEP', 'TREES_SYNC',
]);

/**
 * What waits for the person's Confirm in this window (PERSONDOOR1f, RPC1c; D156 point 4, the person-door design §4.2), under the
 * app strip and above every view of every main window: each press by its own name, every value it carries, what sent it, that
 * Daoris cannot tell whether the person ran it or an agent did, and *Refuse* and *Confirm*. And the adoption notice (design
 * §2.3), said once until dismissed.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. **Its default is no** (design §4.2): nothing is
 * focused or pressed for the person, *Refuse* comes first, and a card nobody answers is refused when its two minutes end. A
 * refusal of the answer itself is said under its card (the UI/UX design's R3), never as a toast.
 */
export function Confirmations({ waiting, notice = false, onAnswer, onDismissNotice }: {
  waiting: Confirmation[];
  /** Whether to say that the host in use holds a person key this window does not, so the person's acts are refused there. */
  notice?: boolean;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  onDismissNotice?: () => void;
}) {
  const { t } = useTranslation();
  if (waiting.length === 0 && !notice) return null;

  return (
    <div className="grid border-b border-line bg-raised text-small text-ink-soft">
      {notice && (
        <div role="status" className="flex flex-wrap items-center gap-2 border-l-[3px] border-warn px-3 py-1.5">
          <span className="min-w-0 flex-1">{t('person.adopted')}</span>
          <Button variant="ghost" onClick={onDismissNotice}>{t('common.dismiss')}</Button>
        </div>
      )}
      {waiting.length > 0 && (
        <section aria-label={t('person.label')} className="grid gap-2 px-3 py-2">
          {waiting.map((each) => <Card key={`${each.source}:${each.id}`} waiting={each} onAnswer={onAnswer} />)}
        </section>
      )}
    </div>
  );
}

function Card({ waiting, onAnswer }: {
  waiting: Confirmation;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
}) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const worded = waiting.source === 'pipe' && WORDED.has(waiting.route);
  const head = waiting.act
    ? t('person.ask.for', { act: waiting.act })
    : worded
      ? t('person.ask.to', { act: t(`person.route.${waiting.route}`) })
      : t('person.ask.for', { act: waiting.route });
  const asker = waiting.asker;

  const press = async (confirm: boolean) => {
    setBusy(true);
    setSaid(null);
    try {
      const answer = await onAnswer(waiting, confirm);
      if (!answer.answered) setSaid(answer.why === 'gone' || !answer.message ? t('person.gone') : answer.message);
    } catch (error) {
      setSaid(sentence(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div role="group" aria-label={head} className="grid gap-1.5 border-l-[3px] border-warn bg-page/60 px-3 py-2 text-body text-ink">
      <p className="m-0">
        {head}
        {(worded || waiting.act) && <> <code className="text-small text-ink-soft">{waiting.route}</code></>}
      </p>
      <p className="m-0 text-small text-ink-soft">
        {asker?.process && asker.pid != null
          ? t('person.asker.process', { process: asker.process, pid: asker.pid })
          : asker?.pid != null
            ? t('person.asker.pid', { pid: asker.pid })
            : t('person.asker.unknown')}
        {' '}
        {t('person.until', { time: clockOf(waiting.expires) })}
      </p>
      {waiting.fields.length > 0 && (
        <dl aria-label={t('person.values')} className="m-0 grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5 text-small">
          {waiting.fields.map((field) => (
            <div key={field.name} className="contents">
              <dt className="font-mono text-ink-soft">{field.name}</dt>
              <dd className="m-0 break-all font-mono">{field.type === 'string' ? JSON.stringify(field.value) : field.value}</dd>
            </div>
          ))}
        </dl>
      )}
      <p className="m-0 text-small">{t('person.cannotTell')}</p>
      <div className="flex flex-wrap items-center gap-2">
        <Button disabled={busy} onClick={() => void press(false)}>{t('person.refuse')}</Button>
        <Button variant="primary" disabled={busy} onClick={() => void press(true)}>{t('person.confirm')}</Button>
      </div>
      {said && <p role="alert" className="m-0 text-small text-warn">{said}</p>}
    </div>
  );
}
