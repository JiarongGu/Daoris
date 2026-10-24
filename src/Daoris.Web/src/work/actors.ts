import type { TFunction } from 'i18next';
import type { ActorValue, Session } from '../data/api';
import type { DetailRow } from './head/DetailsFold';
import { stamp } from '../locales/format';
import { CHOICE_NOT_OFFERED } from '../design/ui';
import { sessionOrigin } from './identity';

/** The agent's reported names, in first-use order. An absence never borrows the asked model. */
export function ranLine(t: TFunction, session?: Pick<Session, 'ran'> | null): string {
  const ran = session?.ran;
  const names = ran?.models.length ? ran.models.join(' · ') : t('work.actor.notReported');
  return ran?.more ? `${names} · ${t('work.actor.more', { n: ran.more })}` : names;
}

function askedValue(t: TFunction, value?: ActorValue | null): string {
  if (!value) return t('work.actor.notRecorded');
  if (value.source === 'account-default') return t('work.actor.accountDefault');
  const name = value.value ?? t('work.actor.notReported');
  const source = value.source === 'entry' ? name : `${name} · ${t(`work.actor.source.${value.source}`)}`;
  return value.source === 'entry' && !value.carried ? `${source} · ${t('work.actor.notCarried')}` : source;
}

/**
 * The head's chip for the choice its actor ran (D205 §4.6 item 2, §7.1; MODELROLE1c2): *choice n* only where a later choice than the
 * first ran, *your pick* where the person picked the agent; none for choice 1, *as before*, a rule's agent or a handed quest.
 */
export function choiceChip(t: TFunction, session: Pick<Session, 'choice'>): string | null {
  const choice = session.choice;
  if (choice === 'pick') return t('work.actor.chip.pick');
  return choice && /^[2-4]$/.test(choice) ? t('work.actor.chip.choice', { n: Number(choice) }) : null;
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
    { label: t('work.actor.title'), value: session.actor ? t(`work.actor.name.${session.actor}`, { defaultValue: session.actor }) : t('work.actor.notRecorded') },
    { label: t('work.actor.choice'), value: session.choice === 'before' ? t('work.actor.before') : session.choice ?? t('work.actor.notRecorded') },
    { label: t('work.actor.asked'), value: `${askedValue(t, session.asked?.model)} · ${askedValue(t, session.asked?.effort)}` },
    { label: t('work.actor.acknowledged'), value: last?.acknowledged ?? t('work.actor.notReported') },
    { label: t('work.actor.ran'), value: ranLine(t, session) },
    { label: t('work.actor.effort'), value: last?.effort ?? t('work.actor.notReported') },
    { label: t('work.actor.changes'), value: session.ran?.entries.length ? [
      ...session.ran.entries.map((entry) => `${stamp(entry.at)} · ${entry.acknowledged ?? t('work.actor.notReported')} · ${entry.effort ?? t('work.actor.notReported')}`),
      ...(session.ran.entriesMore ? [t('work.actor.more', { n: session.ran.entriesMore })] : []),
    ].join('; ') : null },
  ];
}
