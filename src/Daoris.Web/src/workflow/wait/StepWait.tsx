import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';
import { ExternalLink } from '../../design/links';
import type { RunStep, RunWait } from '../run';

// What a run's step waits on, said under it: its kind's declared wait (`NODE_KINDS`' `waits`) in the reader's words, with the facts the
// record names and, where it has them, since when and until when. The driver says which wait it is; the words are the page's.

const clock = (iso: string | null | undefined, language: string): string | null => {
  if (!iso) return null;
  const at = new Date(iso);
  return Number.isNaN(at.getTime()) ? null : at.toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit' });
};

/** A wait in words: `workflow.wait.<kind>.<code>` with its values, its since and until; one with no words is said as recorded. */
export function waitSaid(t: TFunction, kind: string, wait: RunWait, language = 'en'): string {
  const values: Record<string, string> = { ...(wait.values ?? {}), since: clock(wait.since, language) ?? '', until: clock(wait.until, language) ?? '' };
  // A plugin step's wait on a role, or its progress, is any kind's: said by the role and its holder, alike for the person and an agent.
  // The stored code and its value stay `actor` (a stored shape's word); the window's key says role.
  if (wait.code === 'actor' || wait.code === 'progress') {
    const holder = values.holder === 'agent' ? t('workflow.wait.holder.agent', { name: values.holderName ?? '' }) : t('workflow.wait.holder.person');
    return t(`workflow.wait.${wait.code === 'actor' ? 'role' : wait.code}`, { ...values, role: values.actor ?? '', holder, words: values.words ?? '' }).trim();
  }
  return t(`workflow.wait.${kind}.${wait.code}`, { ...values, defaultValue: t('workflow.wait.recorded', { code: wait.code }) });
}

/** The step's wait, one line under it; nothing where it waits on none. */
export function StepWait({ step }: { step: RunStep }) {
  const { t, i18n } = useTranslation();
  if (!step.wait) return null;
  const since = clock(step.wait.since, i18n.language);
  return (
    <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
      <Inline text={waitSaid(t, step.step.kind, step.wait, i18n.language)} />
      {since && <span className="text-meta text-ink-faint"> · {t('workflow.wait.since', { since })}</span>}
      {step.wait.values?.url && <> · <ExternalLink href={step.wait.values.url}>{t('workflow.wait.open')}</ExternalLink></>}
    </p>
  );
}
