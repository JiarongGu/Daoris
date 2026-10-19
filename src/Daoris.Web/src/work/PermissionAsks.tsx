import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { CodeText, Button, Inline } from '../design/ui';
import { cn } from '../lib/cn';
import { elapsed } from '../locales/format';
import { type Answered, Refused } from './InlineConfirm';
import { useNow } from './useNow';
import { SOUGHT } from './WaitAnchor';

/**
 * One option an agent offered on a permission request it holds for the person (AUTONOMY1b3a, D184 point 3), as `PERMISSION_WAITS`
 * answers it: the `optionId` the answer names, the agent's own label, and the protocol's kind. Only what the person may choose is
 * listed: this call's allow and a rejection, never a standing allow, which is a rule's (D74).
 */
export type PermissionChoice = { id: string; name: string; kind: string };

/**
 * A tool's permission request a driven session holds for the person under its repository's asking permission prompts (AUTONOMY1b3a,
 * D184 point 3), as `PERMISSION_WAITS` answers it: the session, the request's id on the agent's wire, the call it asks for and its
 * title and kind as the agent wrote them (null where it gave none), what the person may choose, and when it was held.
 */
export type PermissionWait = {
  session: string;
  request: string;
  call: string | null;
  title: string | null;
  kind: string | null;
  choices: PermissionChoice[];
  since: string;
};

/** The person's answer as the driver took it (`SESSION_PERMISSION_ANSWER`): whether it stands, and where not, its code and sentence. */
export type PermissionAnswered = { answered: boolean; why: string | null; message: string };

/** The person's answer to one held permission: the option they pressed, by the agent's id, and how the answer ended (UXFIX2). */
export type PermissionAnswer = (wait: PermissionWait, option: string, answered: Answered) => void;

/**
 * What the frame hands the head and the foot's strip for a session's held permissions (AUTONOMY1b3c, D168 point 3): every request
 * the shell read, of which the session's own are drawn, the answer's one door and its pending. Absent where nothing reads them (a
 * browser, a teammate's record).
 */
export type PermissionWaits = {
  waits: readonly PermissionWait[];
  onAnswer?: PermissionAnswer;
  /** An answer is on its way, pressed in either place: every press waits for it. */
  busy?: boolean;
};

/**
 * How long a held request waits before the driver refuses it, in minutes: `HeldPermissions.Bound`, which `PERMISSION_WAITS` does not
 * answer, so the page says the driver's constant (AUTONOMY1b3a: half the session's default 30-minute run).
 */
export const PERMISSION_BOUND_MINUTES = 15;

/** The requests one session holds, in the order the driver listed them (oldest first). */
export const heldFor = (waits: readonly PermissionWait[], session: string) => waits.filter((wait) => wait.session === session);

/** Each kind of choice the person may make, by the catalogue's key; a kind a later agent sends is said by the agent's own name. */
const CHOICE_WORDS: Readonly<Record<string, string>> = {
  allow_once: 'work.attention.act.allowOnce',
  reject_once: 'work.attention.act.refuseOnce',
  reject_always: 'work.attention.act.refuseAlways',
};

/** A choice's press, named for what it does in the reader's language, or by the agent's own label where its kind is not known. */
export const choiceLabel = (t: TFunction, choice: PermissionChoice) =>
  (CHOICE_WORDS[choice.kind] ? t(CHOICE_WORDS[choice.kind]!) : choice.name);

/** The codes an answer is refused by (AUTONOMY1b3a's `HeldPermissions.Answer`), each said in the catalogue's words. */
const REFUSALS = new Set(['unheld', 'unoffered', 'standing']);

/** An answer's refusal in the catalogue's words, by its code; a code a later driver adds is said in the driver's own sentence. */
export const permissionSaid = (t: TFunction, why: string | null, message: string) =>
  (why && REFUSALS.has(why) ? t(`work.wait.permission.refused.${why}`) : message || why || '');

/**
 * **A tool call its session holds for the person's answer** (AUTONOMY1b3c; D184 point 3 and its AUTONOMY1b3a note): under a
 * repository's asking permission prompts, a call its rules do not allow by themselves waits here, as the console asks its person,
 * with the call as the agent named it (content, shown as written), its kind, how long it has waited and the bound at which the
 * driver refuses it, and one press per choice the agent offered, named for what it does: *Allow once* lets this call alone run,
 * and a refusal refuses it. Never an allow the agent did not offer, and never a standing one, which is a rule's to give (D74).
 *
 * @remarks
 * **One press, no ask under it** (D52's in-flow cards): the allow widens nothing standing, only this call, as the console's yes
 * does; what widens the rules is a proposal, whose *Allow…* asks once. Nothing learns from the answer (D37).
 *
 * **Props only** (components §2): the frame holds the answer and its pending, and hands the head and the strip the same. Without
 * `onAnswer` no press is drawn: a browser or a teammate's record still says what waits. Each waits in place, and a refusal of the
 * answer is said beside it (UXFIX2b3b).
 */
export function PermissionAsks({ waits, onAnswer, busy = false, lead, marked }: {
  waits: readonly PermissionWait[];
  onAnswer?: PermissionAnswer;
  busy?: boolean;
  /** The line above the list, where it stands: absent, none. */
  lead?: string;
  /** The request a press led to, outlined, by its id. */
  marked?: string;
}) {
  // How long each has waited steps on its own: a held request moves no record.
  useNow(waits.length > 0 ? 30_000 : null);
  return (
    <>
      {lead && <p className="m-0 mb-2 text-small text-ink-soft">{lead}</p>}
      <ul className="m-0 grid list-none gap-2 p-0">
        {waits.map((wait) => (
          <PermissionAsk key={`${wait.session}#${wait.request}`} wait={wait} onAnswer={onAnswer} busy={busy} marked={marked === wait.request} />
        ))}
      </ul>
    </>
  );
}

function PermissionAsk({ wait, onAnswer, busy, marked }: {
  wait: PermissionWait;
  onAnswer?: PermissionAnswer;
  busy: boolean;
  marked: boolean;
}) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  const refusalId = useId();
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  // Every choice shares one pending and one refusal line: they answer the same request.
  const press = (option: string) => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onAnswer!(wait, option, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };
  const untitled = wait.kind ? t('work.wait.permission.untitledKind', { kind: wait.kind }) : t('work.wait.permission.untitled');

  return (
    <li
      tabIndex={-1}
      data-permission={wait.request}
      className={cn('grid gap-1 rounded-control border border-line bg-raised px-3 py-2', marked ? SOUGHT : 'outline-none')}
    >
      {/* The call as the agent named it (content, shown as written); its kind where it named one. */}
      <p className="m-0 min-w-0 break-words text-body font-semibold">
        {wait.title ? <CodeText text={wait.title} /> : <Inline text={untitled} />}
      </p>
      {wait.title && wait.kind && (
        <p className="m-0 text-small text-ink-soft"><Inline text={t('work.wait.permission.kind', { kind: wait.kind })} /></p>
      )}
      <p className="m-0 font-mono text-meta text-ink-faint">
        {t('work.wait.permission.waited', { span: elapsed(wait.since), minutes: PERMISSION_BOUND_MINUTES })}
      </p>
      {onAnswer && wait.choices.length > 0 && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {wait.choices.map((choice) => (
            <Button
              key={choice.id}
              variant={choice.kind === 'allow_once' ? 'primary' : undefined}
              disabled={busy || pending}
              aria-describedby={refusal ? refusalId : undefined}
              onClick={() => press(choice.id)}
            >
              {choiceLabel(t, choice)}
            </Button>
          ))}
          {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </li>
  );
}
