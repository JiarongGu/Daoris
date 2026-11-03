import type { TFunction } from 'i18next';
import type { ActorChosen, ActorValue, Session } from '../data/api';
import type { DetailRow } from './head/DetailsFold';
import { stamp } from '../locales/format';
import { CHOICE_NOT_OFFERED } from '../design/ui';
import { sessionOrigin } from './identity';

/** The agent's reported names, in first-use order. An absence never borrows the asked model. */
export function ranLine(t: TFunction, session?: Pick<Session, 'ran'> | null): string {
  const ran = session?.ran;
  const names = ran?.models.length ? ran.models.join(' · ') : t('work.role.notReported');
  return ran?.more ? `${names} · ${t('work.role.more', { n: ran.more })}` : names;
}

function askedValue(t: TFunction, value?: ActorValue | null): string {
  if (!value) return t('work.role.notRecorded');
  if (value.source === 'account-default') return t('work.role.accountDefault');
  const name = value.value ?? t('work.role.notReported');
  const source = value.source === 'entry' ? name : `${name} · ${t(`work.role.source.${value.source}`)}`;
  return value.source === 'entry' && !value.carried ? `${source} · ${t('work.role.notCarried')}` : source;
}

/**
 * The head's chip for the choice its actor ran (D205 §4.6 item 2, §7.1; MODELROLE1c2): *choice n* only where a later choice than the
 * first ran, *your pick* where the person picked the agent; none for choice 1, *as before*, a rule's agent or a handed quest.
 */
export function choiceChip(t: TFunction, session: Pick<Session, 'choice' | 'chosen'>): string | null {
  const choice = session.choice;
  if (choice === 'pick') return t('work.role.chip.pick');
  if (!choice || !/^[2-4]$/.test(choice)) return null;
  const number = t('work.role.chip.choice', { n: Number(choice) });
  const reason = chipReason(t, session.chosen);
  return reason === null ? number : `${number} · ${reason}`;
}

const BY_CODES = ['weight', 'share', 'budget', 'work'];
const CONDITION_CODES = ['limit', 'signed-out', 'model-refused', 'transient', 'failed', 'quiet', 'budget'];

/** The reason's code: a moved part by its condition, any other by its `by`; none for the stored order, a code the page does not know is *other*. */
function reasonCode(chosen: ActorChosen): string | null {
  if (chosen.by === 'first') return null;
  if (chosen.by === 'moved') return chosen.condition && CONDITION_CODES.includes(chosen.condition) ? chosen.condition : 'moved';
  return BY_CODES.includes(chosen.by) ? chosen.by : 'other';
}

function chipReason(t: TFunction, chosen?: ActorChosen | null): string | null {
  const code = chosen ? reasonCode(chosen) : null;
  return code === null ? null : t(`work.role.chip.reason.${code}`);
}

/** *Details*' sentence for how the choice was reached; the policy's absence on the record is said, not left blank. */
export function chosenLine(t: TFunction, chosen?: ActorChosen | null): string {
  if (!chosen) return t('work.role.chosen.none');
  if (chosen.by === 'moved') {
    const from = chosen.from ? t('work.role.chosen.movedFrom', { from: chosen.from }) : t('work.role.chosen.movedAway');
    return chosen.condition && CONDITION_CODES.includes(chosen.condition)
      ? t('work.role.chosen.movedOn', { from, why: t(`work.role.chosen.on.${chosen.condition}`) })
      : t('work.role.chosen.movedBare', { from });
  }
  return t(`work.role.chosen.by.${chosen.by === 'first' || BY_CODES.includes(chosen.by) ? chosen.by : 'other'}`);
}

/**
 * What a record ended `choice-not-offered` says in place of a failure (D205 §3.5(c), §7.1; MODELROLE1c3): here, the agent, the model and
 * the account it was refused on, the record's own; on a teammate's mirror, which carries no account (D47 §4), the code's sentence naming
 * none. Null for every other record.
 */
export function notStartedLine(t: TFunction, session: Pick<Session, 'id' | 'state' | 'noteParts' | 'profile'>): string | null {
  if (session.state !== 'failed') return null;
  const part = session.noteParts?.find((each) => each.code === CHOICE_NOT_OFFERED);
  if (!part || part.words !== undefined) return null;
  const values = part.values ?? {};
  const agent = typeof values.agent === 'string' ? values.agent : null;
  const model = typeof values.model === 'string' ? values.model : null;
  return !sessionOrigin(session) && session.profile && agent && model
    ? t('work.notStarted.here', { agent, model, account: session.profile })
    : t('work.notStarted.mirror');
}

/** Details compares no aliases: asked, acknowledged and ran are separate facts (D205). */
export function actorRows(t: TFunction, session: Session): DetailRow[] {
  const last = session.ran?.entries.at(-1);
  return [
    { label: t('work.role.title'), value: session.actor ? t(`work.role.name.${session.actor}`, { defaultValue: session.actor }) : t('work.role.notRecorded') },
    { label: t('work.role.choice'), value: session.choice === 'before' ? t('work.role.before') : session.choice ?? t('work.role.notRecorded') },
    { label: t('work.role.chosen.label'), value: chosenLine(t, session.chosen) },
    { label: t('work.role.asked'), value: `${askedValue(t, session.asked?.model)} · ${askedValue(t, session.asked?.effort)}` },
    { label: t('work.role.acknowledged'), value: last?.acknowledged ?? t('work.role.notReported') },
    { label: t('work.role.ran'), value: ranLine(t, session) },
    { label: t('work.role.effort'), value: last?.effort ?? t('work.role.notReported') },
    { label: t('work.role.changes'), value: session.ran?.entries.length ? [
      ...session.ran.entries.map((entry) => `${stamp(entry.at)} · ${entry.acknowledged ?? t('work.role.notReported')} · ${entry.effort ?? t('work.role.notReported')}`),
      ...(session.ran.entriesMore ? [t('work.role.more', { n: session.ran.entriesMore })] : []),
    ].join('; ') : null },
  ];
}
