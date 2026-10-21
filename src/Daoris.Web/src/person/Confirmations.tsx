import { createContext, type RefObject, useContext, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, drawerOpen, Icon, type Notify, watchDrawers } from '../design/ui';
import { ago, clockOf, sentence, span } from '../locales/format';
import { useNow } from '../work/useNow';

/**
 * One value a press carries, as the card shows it (RPC1c, PERSONDOOR1b): its JSON pointer, its JSON type, and a string as it is
 * and anything else as its JSON, so a string "3" and a number 3 read apart.
 */
export type ConfirmationField = { name: string; type: string; value: string };

/**
 * A value a terminal's press's sentence names (NOTIFY2b, the notification design §3.2), by the role the modules' wording table
 * (`PipeWording`) gives it: its role, its pointer, and its value as `fields` holds it.
 */
export type ConfirmationNamed = { role: string; name: string; value: string };

/**
 * A press that waits for the person's confirmation in this window (PERSONDOOR1f, RPC1c; D156 §4.2): one a terminal sent over the
 * pipe (`pipe`), named by its route, or one asked at the service's own confirmations (`service`), named by its door and the act the
 * host's table gives it. Never a sentence the asker wrote.
 */
export type Confirmation = {
  source: 'pipe' | 'service';
  id: string;
  /** The module a terminal's press was sent to (`DAORIS.DRIVER`); absent for the service's. */
  module?: string;
  /** The route's name in the census (`SESSION_ARCHIVE`), or the service door's method and pattern. */
  route: string;
  /** The act the service's table names (its own words, shown as they are); none for a terminal's press, which the page words. */
  act?: string | null;
  fields: ConfirmationField[];
  /** The values a terminal's press's sentence names by role (NOTIFY2b); absent from a shell before them, which names none. */
  named?: ConfirmationNamed[];
  /** What sent it, as the system names the pipe's client; null where nothing can say (the service's). */
  asker?: { process?: string | null; pid?: number | null } | null;
  /** When it was asked. */
  asked?: string | null;
  /**
   * When it is refused: for a terminal's press, the end of its wait to be found until a window shows it, and of its two minutes from
   * then (CONFIRMSEEN1).
   */
  expires: string;
  /**
   * Whether it was shown: a terminal's press by any window's word, which starts its two minutes; the service's by this window's own
   * judgement, since its door hears none (NOTIFY2b).
   */
  shown?: boolean;
};

/** How the person's answer went: answered, or no longer waiting, by the bridge's code (`gone`) or the host's own sentence. */
export type ConfirmationAnswer = { answered: true } | { answered: false; why: string | null; message: string };

/** A session a press names, as the page names it (NOTIFY2b, §3.2); its workspace only where its repository's name is in two. */
export type PressSession = { title: string; repository: string; workspace?: string | null };

/** What the page names a press's references by: a session, a quest and an ask from the records it holds; none named by its id. */
export type PressNames = {
  session?: (id: string) => PressSession | null;
  quest?: (id: string) => string | null;
  ask?: (id: string) => string | null;
};

/**
 * The catalogue key of a terminal's press's act (NOTIFY2b): `person.route.ROUTE` for the driver's, `person.route.MODULE.ROUTE` for
 * another module's. The modules' twin is `PipeWording.Key`, whose census test holds every person's route worded in both catalogues.
 */
export function routeKey(module: string | undefined, route: string): string {
  const named = (module ?? 'DAORIS.DRIVER').toUpperCase();
  return named === 'DAORIS.DRIVER' ? `person.route.${route}` : `person.route.${named.replace(/^DAORIS\./, '')}.${route}`;
}

/** A session's short id, as the card says it beside its name: a mirrored record's own part, eight characters at most. */
export const shortId = (id: string) => (id.slice(id.lastIndexOf('/') + 1) || id).slice(0, 8);

/** The element a press's card is drawn in, which the toast's *Open* and a door find it by. */
export const confirmationAnchor = (id: string) => `confirmation-${id}`;

/**
 * The scroll area a card is drawn in (NOTIFY2b): the view's, which says it. A card in none is judged against the window's own.
 */
export const PressArea = createContext<RefObject<HTMLElement | null> | null>(null);

/**
 * The adoption notice (D156 §2.3): one line under the strip, about the window's own host, said once until dismissed. It stays
 * under the strip when the presses left it for *Notifications* (NOTIFY2b, the notification design §3.1).
 */
export function AdoptionNotice({ onDismiss }: { onDismiss?: () => void }) {
  const { t } = useTranslation();
  return (
    <div role="status" className="flex flex-wrap items-center gap-2 border-b border-l-[3px] border-line border-l-warn bg-raised px-3 py-1.5 text-small text-ink-soft">
      <span className="min-w-0 flex-1">{t('person.adopted')}</span>
      <Button variant="ghost" onClick={onDismiss}>{t('common.dismiss')}</Button>
    </div>
  );
}

/**
 * What waits for the person's Confirm (PERSONDOOR1f, RPC1c, NOTIFY2b; D156 point 4, the person-door design §4.2, the notification
 * design §3): a card per press of either source, drawn in *Notifications*' first group and nowhere across the page.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. **Its default is no** (design §4.2): nothing is
 * focused or pressed for the person, *Refuse* comes first, and a card nobody answers is refused when its time ends. A refusal of
 * the answer itself is said under its card (the UI/UX design's R3), and by a toast only where the card has gone with its press by
 * the time the answer returns, as a Confirm past its time finds it (NOTIFY2a, the notification design §3.4).
 */
export function Confirmations({ waiting, onAnswer, notify, onShown, names }: {
  waiting: Confirmation[];
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  /** The window's toasts: where a refusal is said once its card has gone with its press (NOTIFY2a). Absent, it is said nowhere. */
  notify?: Notify;
  /**
   * Said once of each press not yet shown, when its decision region is in view in a window in front of the person (NOTIFY2b, §3.4):
   * a terminal's two minutes count from then. Absent, nothing is said.
   */
  onShown?: (waiting: Confirmation) => void;
  names?: PressNames;
}) {
  if (waiting.length === 0) return null;
  // No landmark of its own: *Notifications*' group is the region these cards are drawn in.
  return (
    <div className="grid gap-2">
      {waiting.map((each) => (
        <Card key={`${each.source}:${each.id}`} waiting={each} onAnswer={onAnswer} notify={notify} onShown={onShown} names={names} />
      ))}
    </div>
  );
}

/**
 * Whether this window is in front of the person: on screen and focused (CONFIRMSEEN1). A window behind another, or minimized,
 * draws its card where nobody looks, which is how five presses once expired unseen.
 */
function looking(): boolean {
  return typeof document !== 'undefined' && document.visibilityState === 'visible' && document.hasFocus();
}

/** A box's edges, as the judgement reads them. */
type Box = { top: number; bottom: number; height: number };

/**
 * Whether a card's decision region is shown in its area (NOTIFY2b, the notification design §3.4): its visible height is at least
 * the smaller of its own height and its area's, so a region whole in its area is shown, and one taller than a short area is shown by
 * filling it, then only with its buttons' row whole in view too, since two minutes must not start on a card whose answer cannot be
 * seen. A region not laid out (no height) is not shown.
 */
export function regionShown(region: Box, area: Box, buttons: Box | null): boolean {
  if (region.height <= 0 || area.height <= 0) return false;
  const visible = Math.max(0, Math.min(region.bottom, area.bottom) - Math.max(region.top, area.top));
  if (visible + 1 < Math.min(region.height, area.height)) return false;
  if (region.height <= area.height + 1) return true;
  return buttons !== null && buttons.height > 0 && buttons.top >= area.top - 1 && buttons.bottom <= area.bottom + 1;
}

/** The window's own box, where a card is drawn in no area of a view's. */
const windowBox = (): Box => ({ top: 0, bottom: window.innerHeight, height: window.innerHeight });

/** The intersection's steps an observer wakes at: fine enough that a region coming to fill its area is heard. */
const STEPS = Array.from({ length: 101 }, (_, index) => index / 100);

/**
 * Says once that a card's decision region is shown (NOTIFY2b, §3.4): one judgement over its intersection with its area, the
 * window's size and focus, and a drawer's scrim, judged again at each change of any; its group's fold, its tab and where the view is
 * docked mount or unmount the card, which judges again as it mounts. The scroll stays the person's: nothing here moves it. A notice of
 * *Done and failed* is read by the same judgement (NOTIFY2f, §2.4), with no buttons' row.
 */
export function useShownOnce(enabled: boolean, region: RefObject<HTMLElement | null>, buttons: RefObject<HTMLElement | null>, said: () => void) {
  const area = useContext(PressArea);
  const latest = useRef(said);
  useEffect(() => { latest.current = said; });
  useEffect(() => {
    if (!enabled) return undefined;
    let told = false;
    const check = () => {
      const held = region.current;
      if (told || !held || !looking() || drawerOpen()) return;
      const room = area?.current ?? null;
      if (!regionShown(held.getBoundingClientRect(), room ? room.getBoundingClientRect() : windowBox(),
        buttons.current ? buttons.current.getBoundingClientRect() : null)) return;
      told = true;
      latest.current();
    };
    check();
    const room = area?.current ?? null;
    const seen = typeof IntersectionObserver === 'undefined' ? null : new IntersectionObserver(check, { root: room, threshold: STEPS });
    if (region.current) seen?.observe(region.current);
    if (buttons.current) seen?.observe(buttons.current);
    const sized = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(check);
    if (room) sized?.observe(room);
    if (region.current) sized?.observe(region.current);
    room?.addEventListener('scroll', check, { passive: true });
    window.addEventListener('focus', check);
    window.addEventListener('resize', check);
    document.addEventListener('visibilitychange', check);
    const unwatch = watchDrawers(check);
    return () => {
      seen?.disconnect();
      sized?.disconnect();
      room?.removeEventListener('scroll', check);
      window.removeEventListener('focus', check);
      window.removeEventListener('resize', check);
      document.removeEventListener('visibilitychange', check);
      unwatch();
    };
  }, [enabled, area, region, buttons]);
}

/** A box of a card's region, at most eight lines high and scrolled inside (§3.4), so a press of many values or long words is bounded. */
const BOX = 'max-h-[8lh] overflow-y-auto';

function Card({ waiting, onAnswer, notify, onShown, names }: {
  waiting: Confirmation;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  notify?: Notify;
  onShown?: (waiting: Confirmation) => void;
  names?: PressNames;
}) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const [details, setDetails] = useState(false);
  const region = useRef<HTMLDivElement>(null);
  const buttons = useRef<HTMLDivElement>(null);
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
  useShownOnce(waiting.shown !== true && onShown !== undefined, region, buttons,
    () => latest.current.onShown?.(latest.current.waiting));

  // Its time left once shown, stepping each second (R2); a service's from its ask, since its door counts its two minutes from then.
  const counting = waiting.source === 'service' || waiting.shown === true;
  const now = useNow(counting ? 1000 : null);

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

  const named = waiting.named ?? [];
  const head = headOf(t, waiting, named, names);
  const listed = new Set(named.map((each) => each.name));
  const also = waiting.fields.filter((field) => !listed.has(field.name));
  const words = named.filter((each) => each.role === 'words');
  const files = named.filter((each) => each.role === 'files');
  const others = named.filter((each) => !['words', 'files', 'level', 'form'].includes(each.role)
    && !(head.sessionSaid && each.role === 'session'));
  const asker = waiting.asker;
  const left = Math.max(0, new Date(waiting.expires).getTime() - now);

  return (
    <div
      role="group"
      aria-label={head.label}
      id={confirmationAnchor(waiting.id)}
      tabIndex={-1}
      data-press={`${waiting.source}:${waiting.id}`}
      className="grid gap-1.5 border-l-[3px] border-warn bg-page/60 px-3 py-2 text-body text-ink"
    >
      {/* The decision region (§3.4): its head through its buttons, bounded whatever the press carries. */}
      <div ref={region} data-press-region="" className="grid gap-1.5">
        <p className="m-0">{head.text}</p>
        {others.length > 0 && (
          <dl className="m-0 grid gap-y-0.5 text-small">
            {others.map((each) => (
              <div key={each.name} className="flex flex-wrap gap-x-2">
                <dt className="text-ink-soft">{t(`person.role.${each.role}`)}</dt>
                <dd className="m-0 min-w-0 break-words">{namedValue(t, each, names)}</dd>
              </div>
            ))}
          </dl>
        )}
        {words.map((each) => (
          // The words a press carries are its own field, quoted in the content's face and set apart from Daoris's sentence (§3.2).
          <blockquote key={each.name} className={`m-0 whitespace-pre-wrap break-words border-l-2 border-line pl-2 font-serif ${BOX}`}>
            “{each.value}”
          </blockquote>
        ))}
        {files.length > 0 && (
          <div className="grid gap-0.5 text-small">
            <p className="m-0">{t(head.readsFiles ? 'person.attach' : 'person.attachSent')}</p>
            <ul className={`m-0 list-none p-0 font-mono ${BOX}`}>
              {files.map((each) => <li key={each.name} className="break-all">{each.value}</li>)}
            </ul>
          </div>
        )}
        {also.length > 0 && (
          <div className="grid gap-0.5 text-small">
            <p className="m-0 text-ink-soft">{t('person.also')}</p>
            <dl aria-label={t('person.also')} className={`m-0 grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5 ${BOX}`}>
              {also.map((field) => (
                <div key={field.name} className="contents">
                  <dt className="font-mono text-ink-soft">{field.name}</dt>
                  <dd className="m-0 break-all font-mono">{field.type === 'string' ? JSON.stringify(field.value) : field.value}</dd>
                </div>
              ))}
            </dl>
          </div>
        )}
        <p className="m-0 text-small text-ink-soft">
          {asker?.process && asker.pid != null
            ? t('person.asker.process', { process: asker.process, pid: asker.pid })
            : asker?.pid != null
              ? t('person.asker.pid', { pid: asker.pid })
              : t('person.asker.unknown')}
          {waiting.asked ? <> · {t('person.asked', { ago: ago(waiting.asked) })}</> : null}
        </p>
        <p className="m-0 text-small text-warn">
          {counting ? t('person.left', { time: span(left) }) : t('person.until', { time: clockOf(waiting.expires) })}
        </p>
        <p className="m-0 text-small">{t('person.cannotTell')}</p>
        <div ref={buttons} data-press-buttons="" className="flex flex-wrap items-center gap-2">
          <Button disabled={busy} onClick={() => void press(false)}>{t('person.refuse')}</Button>
          <Button variant="primary" disabled={busy} onClick={() => void press(true)}>{t('person.confirm')}</Button>
          <button
            type="button"
            aria-expanded={details}
            onClick={() => setDetails((open) => !open)}
            className="ml-auto flex items-center gap-1 border-0 bg-transparent p-0 text-small text-ink-soft hover:text-ink"
          >
            {t('person.details')}
            <Icon name={details ? 'chevronDown' : 'chevronRight'} size={12} />
          </button>
        </div>
      </div>
      {said && <p role="alert" className="m-0 text-small text-warn">{said}</p>}
      {/* *Details*, below the region (§3.4): the route's name and every value by its pointer, raw, as the card showed them. */}
      {details && (
        <div className="grid gap-1 border-t border-line pt-1.5 text-small">
          <code className="text-ink-soft">{waiting.module ? `${waiting.module}.${waiting.route}` : waiting.route}</code>
          {waiting.fields.length > 0 && (
            <dl aria-label={t('person.values')} className="m-0 grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5">
              {waiting.fields.map((field) => (
                <div key={field.name} className="contents">
                  <dt className="font-mono text-ink-soft">{field.name}</dt>
                  <dd className="m-0 break-all font-mono">{field.type === 'string' ? JSON.stringify(field.value) : field.value}</dd>
                </div>
              ))}
            </dl>
          )}
        </div>
      )}
    </div>
  );
}

type T = (key: string, values?: Record<string, unknown>) => string;

/**
 * The card's sentence (§3.2): composed from Daoris's words for the route's act and the request's own values, never from a sentence
 * the asker wrote. `SESSION_INPUT` says the session, the words' level and that its files are read from this machine; any other names
 * its act, its named values beneath it; the service's says its door's own act; a route no catalogue words is named by itself.
 */
function headOf(t: T, waiting: Confirmation, named: ConfirmationNamed[], names?: PressNames): {
  text: string; label: string; sessionSaid: boolean; readsFiles: boolean;
} {
  if (waiting.act) {
    const text = t('person.ask.for', { act: waiting.act });
    return { text, label: text, sessionSaid: false, readsFiles: false };
  }
  const key = routeKey(waiting.module, waiting.route);
  const act = t(key);
  const worded = act !== key;
  const form = named.find((each) => each.role === 'form' && each.name === '/said');
  const readsFiles = form?.value === 'true';
  const session = named.find((each) => each.role === 'session');
  if (waiting.route === 'SESSION_INPUT' && session) {
    const level = named.find((each) => each.role === 'level')?.value;
    const text = t('person.say', {
      session: sessionNamed(t, session.value, names),
      level: t(`person.level.${level === 'ordinary' || level === 'now' ? level : 'urgent'}`),
    });
    return { text, label: worded ? t('person.ask.to', { act }) : text, sessionSaid: true, readsFiles };
  }
  if (!worded) {
    const text = t('person.ask.for', { act: waiting.route });
    return { text, label: text, sessionSaid: false, readsFiles };
  }
  const text = named.some((each) => each.role !== 'form' && each.role !== 'level')
    ? t('person.ask.on', { act })
    : t('person.ask.to', { act });
  return { text, label: t('person.ask.to', { act }), sessionSaid: false, readsFiles };
}

/** A session as Sessions names it, always with its repository and short id, and its workspace where its repository's name is in two. */
function sessionNamed(t: T, id: string, names?: PressNames): string {
  const session = names?.session?.(id);
  if (!session) return t('person.noSession', { id });
  return session.workspace
    ? t('person.sessionIn', { title: session.title, workspace: session.workspace, repository: session.repository, id: shortId(id) })
    : t('person.session', { title: session.title, repository: session.repository, id: shortId(id) });
}

/** A named value as its role names it: a session or a quest or an ask by its name and id, anything else as it is. */
function namedValue(t: T, each: ConfirmationNamed, names?: PressNames): string {
  if (each.role === 'session' || each.role === 'sessions') return sessionNamed(t, each.value, names);
  if (each.role === 'quest' || each.role === 'ask') {
    const name = (each.role === 'quest' ? names?.quest : names?.ask)?.(each.value);
    return name ? t('person.named', { name, id: `#${shortId(each.value)}` }) : `#${each.value}`;
  }
  return each.value;
}
