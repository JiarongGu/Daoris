import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useOverruleSupervisor } from '../../bridge/sessions/supervision';
import type { LineSupervision } from './lineSupervision';
import { SupervisorCourseCard } from './SupervisorCourseCard';

/** The newest decision the supervisor made on `session`'s referred points, with *Overrule* where this machine holds the record; none where it made none. */
export function SessionSupervisorCourse({ session, supervision, here }: { session: string; supervision: LineSupervision; here: boolean }) {
  const { t } = useTranslation();
  const overrule = useOverruleSupervisor(session);
  const [said, setSaid] = useState<string | null>(null);
  const press = async (decision: string) => {
    setSaid(null);
    const answer = await overrule.mutateAsync(decision).catch((error: Error) => ({ done: false, state: null, message: error.message }));
    if (!answer.done) setSaid('message' in answer ? answer.message : t('work.supervision.overruleRefused'));
  };
  return (
    <SupervisorCourseCard
      course={supervision.courses.at(-1) ?? null}
      onOverrule={here ? (decision) => void press(decision) : undefined}
      busy={overrule.isPending}
      said={said}
    />
  );
}
