import { useTranslation } from 'react-i18next';
import { useAnswerPermission, usePermissionWaits } from '../../bridge/shell';
import { sentence } from '../../locales/format';
import { heldFor, PermissionAsks, permissionSaid } from '../../work/waits/PermissionAsks';

/**
 * The tool calls Ask Daoris's own conversation holds for the person in restricted mode, under the conversation: each with its
 * allow once and its refusal, answered as a working session's card is answered, through the one answer door. Nothing where none waits,
 * or where the conversation is not one of this machine's.
 */
export function AskHeld({ session, live }: { session: string | undefined; live: boolean }) {
  const { t } = useTranslation();
  const waits = usePermissionWaits(live);
  const answer = useAnswerPermission();
  const held = session ? heldFor(waits.data ?? [], session) : [];
  if (held.length === 0) return null;
  return (
    <section aria-labelledby={`ask-held-${session}`} className="mt-3 grid gap-1">
      <h3 id={`ask-held-${session}`} className="m-0 text-small font-semibold">{t('work.wait.permission.title')}</h3>
      <PermissionAsks
        waits={held}
        busy={answer.isPending}
        lead={t('work.wait.permission.lead')}
        onAnswer={(wait, option, answered) => answer.mutate({ session: wait.session, request: wait.request, option }, {
          onSuccess: (said) => (said.answered ? answered.done() : answered.refused(permissionSaid(t, said.why, said.message))),
          onError: (error) => answered.refused(sentence(error)),
        })}
      />
    </section>
  );
}
