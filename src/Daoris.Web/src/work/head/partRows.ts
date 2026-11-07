import type { ComponentProps } from 'react';
import type { AttendedSession } from '../AttendedSession';
import type { useAttendedRecord } from './useAttendedRecord';
import type { useAttendedSession } from './useAttendedSession';

export type PartRows = NonNullable<ComponentProps<typeof AttendedSession>['parts']>['rows'];

/** Each part's record and place as the strip reads them. */
export function partRowsOf(
  sessions: ReturnType<typeof useAttendedSession>['sessions']['data'],
  rootOf: (repository: string) => string | null | undefined,
  where: ReturnType<typeof useAttendedSession>['where'],
  spent: ReturnType<typeof useAttendedRecord>['spent'],
): PartRows {
  return Object.fromEntries((sessions ?? []).map((row) => [row.id, {
    session: row, root: rootOf(row.repository), where: where[row.id], spent: spent[row.id],
  }]));
}
