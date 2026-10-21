import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, type Notify } from '../design/ui';
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
  /**
   * When it is refused: for a terminal's press, the end of its wait to be found until a window shows it, and of its two minutes from
   * then (CONFIRMSEEN1).
   */
  expires: string;
  /** Whether a window said a terminal's press was shown, so its two minutes run; absent for the service's, whose door counts its own. */
  shown?: boolean;
};

/** How the person's answer went: answered, or no longer waiting, by the bridge's code (`gone`) or the host's own sentence. */
export type ConfirmationAnswer = { answered: true } | { answered: false; why: string | null; message: string };

/**
 * The terminal's presses the page words by their act (the census's names for them); any other is named by its route alone, which
 * the person can look up, never by a sentence the asker wrote.
 */
const WORDED = new Set([
  'SESSION_ARCHIVE', 'SESSION_DELETE', 'STOP_SESSION', 'RESOLVE_SESSION', 'SESSION_INPUT', 'LAND_SESSION_TREE', 'DISCARD_SESSION_TREE',
  'SWEEP', 'TREES_SYNC', 'SESSION_QUEUE_EDIT', 'SESSION_GO_ON_NEW', 'SESSION_START_FROM', 'SESSION_HAND_OVER', 'SESSION_HELPER_ASK',
  'SESSION_HELPER_APPROVE', 'SESSION_HELPER_DECLINE', 'SESSION_GO_ON_BRIEF',
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
 * refusal of the answer itself is said under its card (the UI/UX design's R3), and by a toast only where the card has gone with
 * its press by the time the answer returns, as a Confirm past its time finds it (NOTIFY2a, the notification design §3.4).
 */
export function Confirmations({ waiting, notice = false, onAnswer, notify, onShown, onDismissNotice }: {
  waiting: Confirmation[];
  /** Whether to say that the host in use holds a person key this window does not, so the person's acts are refused there. */
  notice?: boolean;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  /** The window's toasts: where a refusal is said once its card has gone with its press (NOTIFY2a). Absent, it is said nowhere. */
  notify?: Notify;
  /**
   * Said once of each terminal's press not yet shown, while this window is in front of the person (CONFIRMSEEN1): its two minutes
   * count from then. Absent, nothing is said.
   */
  onShown?: (waiting: Confirmation) => void;
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
          {waiting.map((each) => (
            <Card key={`${each.source}:${each.id}`} waiting={each} onAnswer={onAnswer} notify={notify} onShown={onShown} />
          ))}
        </section>
      )}
    </div>
  );
}

/** The element a terminal's press's card is drawn in, which *What needs you*'s row opens (CONFIRMSEEN1). */
export const confirmationAnchor = (id: string) => `confirmation-${id}`;

/**
 * Whether this window is in front of the person: on screen and focused (CONFIRMSEEN1). A window behind another, or minimized,
 * draws its card where nobody looks, which is how five presses once expired unseen.
 */
function looking(): boolean {
  return typeof document !== 'undefined' && document.visibilityState === 'visible' && document.hasFocus();
}

function Card({ waiting, onAnswer, notify, onShown }: {
  waiting: Confirmation;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  notify?: Notify;
  onShown?: (waiting: Confirmation) => void;
}) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const told = useRef(false);
  const latest = useRef({ waiting, onShown, notify });
  useEffect(() => {
    latest.current = { waiting, onShown, notify };
  });
  // NOTIFY2a: whether the card is still drawn when its answer returns. A Confirm past its time ends the press, and the pipe tells
  // every window before it answers, so the card has gone by then and its refusal is a toast (R3 where the place has gone).
  const drawn = useRef(true);
  useEffect(() => {
    drawn.current = true;
    return () => { drawn.current = false; };
  }, []);
  const unshown = waiting.source === 'pipe' && waiting.shown !== true && onShown !== undefined;

  // CONFIRMSEEN1: said once, as soon as this window is in front of the person: now, or when it comes in front.
  useEffect(() => {
    if (!unshown || told.current) return undefined;
    const check = () => {
      if (told.current || !looking()) return;
      told.current = true;
      latest.current.onShown?.(latest.current.waiting);
    };
    check();
    window.addEventListener('focus', check);
    document.addEventListener('visibilitychange', check);
    return () => {
      window.removeEventListener('focus', check);
      document.removeEventListener('visibilitychange', check);
    };
  }, [unshown]);
  const worded = waiting.source === 'pipe' && WORDED.has(waiting.route);
  const head = waiting.act
    ? t('person.ask.for', { act: waiting.act })
    : worded
      ? t('person.ask.to', { act: t(`person.route.${waiting.route}`) })
      : t('person.ask.for', { act: waiting.route });
  const asker = waiting.asker;

  // Said under the card while it is drawn, or toasted once it has gone with its press.
  const say = (text: string) => {
    if (drawn.current) setSaid(text);
    else latest.current.notify?.(text, 'error');
  };

  const press = async (confirm: boolean) => {
    setBusy(true);
    setSaid(null);
    try {
      const answer = await onAnswer(waiting, confirm);
      if (!answer.answered) say(answer.why === 'gone' || !answer.message ? t('person.gone') : answer.message);
    } catch (error) {
      say(sentence(error));
    } finally {
      if (drawn.current) setBusy(false);
    }
  };

  return (
    <div
      role="group"
      aria-label={head}
      id={waiting.source === 'pipe' ? confirmationAnchor(waiting.id) : undefined}
      tabIndex={-1}
      className="grid gap-1.5 border-l-[3px] border-warn bg-page/60 px-3 py-2 text-body text-ink"
    >
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
