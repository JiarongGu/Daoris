import { useTranslation } from 'react-i18next';
import { Inline } from '../ui';
import type { WorkflowGateState } from './gate';

/** The workflow's refusal beside either landing door; the driver owns the explanation and available actions. */
export function WorkflowHold({ gate }: { gate: WorkflowGateState }) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('workflow.gate.title')} className="border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
      <p className="m-0 font-medium">{t('workflow.gate.title')}</p>
      <p className="m-0"><Inline text={gate.says} /></p>
    </section>
  );
}
