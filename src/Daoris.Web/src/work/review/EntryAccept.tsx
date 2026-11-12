import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, CheckField, Inline } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import type { LandingConflict, LandsCommit, LeftOutCommit } from './LandingSync';

// Declared here, structurally equal to bridge/sessions/entries.ts's shapes, so this molecule imports no bridge domain.
/** One part as an entry's Accept would land it: its own plan, what holds it, and what superseded it. */
export type EntryAcceptPart = {
  session: string;
  form?: string | null;
  target?: string | null;
  own?: boolean | null;
  leftOut?: LeftOutCommit[] | null;
  lands?: LandsCommit[] | null;
  conflict?: LandingConflict | null;
  /** The gate's sentence where a gate holds it; a gate on one part holds its whole line of work. */
  held?: string | null;
  /** What its own Accept would be refused for (a standing branch somebody else made, a plugin's trouble), which holds the line's press too. */
  problem?: string | null;
  /** The earlier part whose tree it shares, whose landing carries it. */
  sameTreeAs?: string | null;
  /** A later part that redid its work (point 3): replayed after it, it conflicts or adds nothing. */
  superseded?: { by: string; says: string; conflicts: boolean } | null;
  /** A later part it conflicts with while having work of its own: never redone, so never offered for leaving out. */
  clash?: { by: string; says: string } | null;
  /** The revision of the review record its run stands on, which the Accept names for it, so a moved run is refused alone. */
  revision?: string | null;
};

/** Where an entry's parts would land: its one branch, or the line a merge goes into, and each part. */
export type EntryAcceptPlan = { entry: string; repository: string; form: string; target: string; parts: EntryAcceptPart[] };

/**
 * **One Accept for an ask's lines of work**: a press under an ask's heading in Sessions' list by ask; its ask says, for each
 * repository, where the parts land in the order they ran, what each leaves out because the line holds it, which part a later one
 * redid with the person's choice to leave it out, and what holds a part. The move lands them; nothing is destroyed, so it wears the
 * primary's hue. A single run's Accept is its review page's alone.
 *
 * @remarks
 * **A molecule**: the plans arrive as props, read by the frame once the ask is open, and the press goes out with the parts left out.
 * - **A superseded part lands unless the person leaves it out**: the choice is theirs, never a default.
 * - **A gate holding any part of an entry holds its repository's landing**: the ask says which and the move lands the others.
 */
export function EntryAccept({ offered, open, onOpen, plans, reading = false, busy = false, numberOf, onLand }: {
  /** How many parts offer work to land: what the press says it accepts. */
  offered: number;
  open: boolean;
  onOpen: (open: boolean) => void;
  /** Each entry's plan once read: one for an entry, one per repository for an ask. */
  plans?: readonly EntryAcceptPlan[] | null;
  /** The plans are being read. */
  reading?: boolean;
  /** A landing is on its way: the press waits for it. */
  busy?: boolean;
  /** A part's number in its entry's strip, to name it as the strip does; its id where it has none. */
  numberOf?: (session: string) => number | null;
  /** The move, with the parts the person chose to leave out, and how to tell the ask how it ended. */
  onLand: (leaveOut: string[], answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [leaving, setLeaving] = useState<ReadonlySet<string>>(new Set());
  const name = (session: string) => {
    const n = numberOf?.(session);
    return n ? t('work.parts.number', { n }) : session;
  };
  if (!open) {
    return (
      <Button variant="primary" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => onOpen(true)}>
        {t('work.accept.ask', { count: offered })}
      </Button>
    );
  }

  const read = !reading && plans ? plans : null;
  // A gate on a kept part holds its repository's work; a part that would be refused holds it the same way, unless the person
  // leaves that part out.
  const heldOf = (plan: EntryAcceptPlan) => plan.parts.some((part) => (part.held || part.problem) && !leaving.has(part.session));
  const gated = (plan: EntryAcceptPlan) => plan.parts.some((part) => part.held && !leaving.has(part.session));
  const toggle = (session: string) => setLeaving((now) => {
    const next = new Set(now);
    if (next.has(session)) next.delete(session); else next.add(session);
    return next;
  });

  return (
    <InlineConfirm
      tone="primary"
      block
      label={t('work.accept.title.ask')}
      says={read === null ? <p className="m-0 text-small text-ink-soft">{t('work.accept.reading')}</p> : (
        <div className="grid gap-2">
          {read.map((plan) => (
            <PlanLines
              key={plan.entry}
              t={t}
              plan={plan}
              name={name}
              leaving={leaving}
              onLeave={toggle}
              held={gated(plan)}
              refused={!gated(plan) && heldOf(plan)}
            />
          ))}
        </div>
      )}
      meanIt={t('work.accept.meanIt')}
      busy={busy}
      ready={read !== null}
      onConfirm={(answered) => onLand([...leaving], answered)}
      onClose={() => onOpen(false)}
    />
  );
}

/** One entry's plan in words: where its parts land, then each part's own lines, a superseded part with its choice. */
function PlanLines({ t, plan, name, leaving, onLeave, held, refused }: {
  t: TFunction;
  plan: EntryAcceptPlan;
  name: (session: string) => string;
  leaving: ReadonlySet<string>;
  onLeave: (session: string) => void;
  /** Under an ask, a gate holds a part of this entry, so its repository's work stays. */
  held: boolean;
  /** Under an ask, a part would be refused (and none is gated), so its repository's work stays. */
  refused: boolean;
}) {
  const { i18n } = useTranslation();
  const count = plan.parts.filter((part) => !part.sameTreeAs && !leaving.has(part.session)).length;
  return (
    <div className="grid gap-1 text-small text-ink-soft">
      <p className="m-0 font-semibold text-ink">{plan.repository}</p>
      <p className="m-0">
        <Inline text={plan.form === 'merge'
          ? t('work.accept.merges', { count, line: plan.target })
          : t('work.accept.lands', { count, branch: plan.target })} />
      </p>
      <ul className="m-0 grid gap-1 pl-4">
        {plan.parts.map((part) => {
          const part_ = name(part.session);
          return (
            <li key={part.session} className="grid gap-0.5">
              {part.own && part.target && <span><Inline text={t('work.accept.own', { part: part_, branch: part.target })} /></span>}
              {part.sameTreeAs && <span>{t('work.accept.sameTree', { part: part_, other: name(part.sameTreeAs) })}</span>}
              {part.leftOut && part.leftOut.length > 0 && (
                <span>{t('work.accept.leavesOut', { part: part_, count: part.leftOut.length })}</span>
              )}
              {part.conflict && <span><Inline text={t('work.accept.conflict', { part: part_, says: part.conflict.says })} /></span>}
              {part.held && (
                <span className="border-l-[3px] border-warn pl-2"><Inline text={t('work.accept.held', { part: part_, says: part.held })} /></span>
              )}
              {part.problem && (
                <span className="border-l-[3px] border-warn pl-2"><Inline text={t('work.accept.problem', { part: part_, says: part.problem })} /></span>
              )}
              {part.clash && (
                <span className="border-l-[3px] border-warn pl-2">
                  <Inline text={t('work.accept.clash', { part: part_, by: name(part.clash.by), says: part.clash.says })} />
                </span>
              )}
              {part.superseded && (
                <>
                  <span>
                    <Inline text={t(part.superseded.conflicts ? 'work.accept.superseded.conflicts' : 'work.accept.superseded.nothing', {
                      part: part_, by: name(part.superseded.by), says: part.superseded.says,
                    })} />
                  </span>
                  <CheckField
                    checked={leaving.has(part.session)}
                    onChange={() => onLeave(part.session)}
                    label={t('work.accept.leaveOut', {
                      part: new Intl.ListFormat(i18n.language, { type: 'conjunction' }).format(
                        [part.session, ...plan.parts.filter((each) => each.sameTreeAs === part.session).map((each) => each.session)].map(name)),
                    })}
                  />
                </>
              )}
            </li>
          );
        })}
      </ul>
      {held && <p className="m-0 border-l-[3px] border-warn pl-2">{t('work.accept.heldAsk', { repository: plan.repository })}</p>}
      {refused && <p className="m-0 border-l-[3px] border-warn pl-2">{t('work.accept.problemAsk', { repository: plan.repository })}</p>}
    </div>
  );
}
