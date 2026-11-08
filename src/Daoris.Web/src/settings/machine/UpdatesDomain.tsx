import { useSayUpdate, useSystemWiring, useUpdateState } from '../../bridge/shell';
import { failure, useErrorNotify } from '../../design/ui';
import type { SettingsDomainProps } from '../domains';
import { UpdateSection } from './Update';
import { WiringVersions } from '../wiring/WiringVersions';

/**
 * Settings → Updates: the install's update, the query the banner reads and the same word it says, then the versions this
 * install carries, read from the shell's one reader (`SYSTEM_WIRING`). Machine-only like every machine domain: a browser is
 * never offered it.
 */
export function UpdatesDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const update = useUpdateState();
  const sayUpdate = useSayUpdate();
  const { query } = useSystemWiring();
  useErrorNotify(query.error, notify);

  return (
    <>
      <UpdateSection update={update.data} busy={sayUpdate.isPending} onSay={(mode) => sayUpdate.mutate(mode, { onError: failure(notify) })} />
      {/* An answer without the versions draws no card of them, never one of nothing. */}
      {query.data?.versions && (
        <WiringVersions
          versions={query.data.versions}
          development={query.data.folder?.kind === 'development'}
          onTools={() => onGo({ view: 'settings', section: 'tools' })}
        />
      )}
    </>
  );
}
