import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';
import type { DraftNode } from './graphDraftTypes';
import { isApproval } from './taskDraft';

/**
 * What a role's task waits for: its holder's *Done*, or an approval its holder gives by *Approve* or *Reject*, whose refusal goes on the
 * step's *If you ask for changes* way. Whoever holds the role says it. A molecule: the step and the choice arrive as props.
 */
export function TaskWaits({ node, onChoose }: { node: DraftNode; onChoose: (approval: boolean) => void }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-1">
      <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.task.waits')}</h4>
      <SelectField
        ariaLabel={t('workflow.edit.task.waits')}
        value={isApproval(node) ? 'approval' : 'task'}
        onChange={(value) => onChoose(value === 'approval')}
        options={[{ value: 'task', label: t('workflow.edit.task.done') }, { value: 'approval', label: t('workflow.edit.task.approval') }]}
      />
    </div>
  );
}
