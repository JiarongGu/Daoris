import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMachineLog, useOpenLogFolder } from '../shell';
import { failure, type Notify, useErrorNotify } from '../ui';
import { LOG_FILTERS, LogList, type LogFilters } from './Logs';

/**
 * The machine log read back (LOG1c, D94): the filters are held here and applied by the shell, the same
 * reading `daoris-driver logs` prints at a terminal (D50).
 */
export function LogsDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<LogFilters>(LOG_FILTERS);
  const log = useMachineLog(filters);
  const open = useOpenLogFolder();
  useErrorNotify(log.error, notify);

  return (
    <LogList
      reading={log.data}
      filters={filters}
      busy={log.isFetching}
      onFilters={setFilters}
      onRefresh={() => void log.refetch()}
      onOpenFolder={() => open.mutate(undefined, {
        onSuccess: (answer) => {
          if (!answer.opened && answer.folder) notify(t('settings.logs.notOpened', { folder: answer.folder }));
        },
        onError: failure(notify),
      })}
    />
  );
}
