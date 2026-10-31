import { useIntegrate, useIntegration } from '../../bridge/repositories/integrate';
import { IntegrationTab } from './IntegrationTab';

/** A repository's Integration tab with its look and acts (INTEGRATE1h): read only while the tab draws it, the owner's acts the window's. */
export function RepositoryIntegration({ repository, notify }: { repository: string; notify?: (message: string) => void }) {
  const look = useIntegration(repository);
  const act = useIntegrate();
  const say = (message: string) => notify?.(message);
  return (
    <IntegrationTab
      repository={repository}
      look={look.data}
      reading={look.isPending}
      refusal={look.error ? String(look.error.message) : null}
      onDrop={(request) => act.mutate({ repository, act: 'drop', request }, { onSuccess: (answer) => say(answer.message) })}
      onRecheck={() => act.mutate({ repository, act: 'recheck' }, { onSuccess: (answer) => say(answer.message) })}
    />
  );
}
