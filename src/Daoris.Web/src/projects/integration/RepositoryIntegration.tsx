import { useIntegrate, useIntegration } from '../../bridge/repositories/integrate';
import { useSessionRelated } from '../../bridge/sessions/sessions';
import { SESSION_ACTIVE } from '../../data/api';
import { useSessions } from '../../data/queries';
import { RelatedSessions } from '../../work/head/RelatedSessions';
import { IntegrationTab } from './IntegrationTab';

/** A repository's Integration tab with its look and acts (INTEGRATE1h): read only while the tab draws it, the owner's acts the window's. */
export function RepositoryIntegration({ repository, notify }: { repository: string; notify?: (message: string) => void }) {
  const look = useIntegration(repository);
  const act = useIntegrate();
  const sessions = useSessions(repository, false);
  const say = (message: string) => notify?.(message);
  const live = (sessions.data ?? []).filter((one) => SESSION_ACTIVE.has(one.state));
  return (
    <IntegrationTab
      repository={repository}
      look={look.data}
      reading={look.isPending}
      refusal={look.error ? String(look.error.message) : null}
      onDrop={(request) => act.mutate({ repository, act: 'drop', request }, { onSuccess: (answer) => say(answer.message) })}
      onRecheck={() => act.mutate({ repository, act: 'recheck' }, { onSuccess: (answer) => say(answer.message) })}
      related={live.length > 0 ? <>{live.map((one) => <RelatedOf key={one.id} session={one.id} repository={repository} />)}</> : undefined}
    />
  );
}

/** One session's related read, read-only (no door): the Integration tab lists it, the session's own page opens it. */
function RelatedOf({ session, repository }: { session: string; repository: string }) {
  return <RelatedSessions repository={repository} related={useSessionRelated(session)} />;
}
