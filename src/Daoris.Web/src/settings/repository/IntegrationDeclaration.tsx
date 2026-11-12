import { useTranslation } from 'react-i18next';
import { SettingRow } from '../../design/ui';
import type { Answered } from '../../work/InlineConfirm';
import type { WorkflowIntegration } from '../../workflow/current';
import { DeclarationAcceptance, type IntegrationRequest } from './DeclarationAcceptance';

export type { IntegrationRequest };

/**
 * **A repository's declaration**, on its Setup beside the landed route: the one acceptance of the line's `daoris.gates.json`
 * (`DeclarationAcceptance`), whose one press serves landed verification and integration alike.
 *
 * @remarks
 * A molecule: every state is reached by its props, and the press goes out.
 */
export function IntegrationDeclaration({ repository, integration, busy = false, onAccept }: {
  repository: string;
  integration: WorkflowIntegration;
  busy?: boolean;
  onAccept: (request: IntegrationRequest, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow label={t('settings.integration.label')} why={t('settings.integration.why')}>
      <DeclarationAcceptance repository={repository} standing={integration} busy={busy} onAccept={onAccept} />
    </SettingRow>
  );
}
