import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';
import type { DraftNode } from './graphDraftTypes';
import { isApproval } from './humanDraft';

/**
 * What a person's step waits for (NODEKIT1 HUMAN): a task the person presses *Done* on, or an approval they *Approve* or *Reject*, whose refusal
 * goes on the step's *If you ask for changes* way. A molecule: the step and the choice arrive as props.
 */
export function HumanWaits({ node, onChoose }: { node: DraftNode; onChoose: (approval: boolean) => void }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-1">
      <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.human.waits')}</h4>
      <SelectField
        ariaLabel={t('workflow.edit.human.waits')}
        value={isApproval(node) ? 'approval' : 'task'}
        onChange={(value) => onChoose(value === 'approval')}
        options={[{ value: 'task', label: t('workflow.edit.human.task') }, { value: 'approval', label: t('workflow.edit.human.approval') }]}
      />
    </div>
  );
}
