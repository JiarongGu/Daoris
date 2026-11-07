import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { standingOf, useDriver, useHarnesses, useSetDrivable, useSetHold, useSetTrees } from '../../bridge/shell';
import type { PlaceRow } from '../../data/wiring';
import { failure, type Notify } from '../../design/ui';
import { clockOf, sentence } from '../../locales/format';
import { stewardSetAt } from '../../person/standing';
import { doorOf } from '../tools';
import type { PlaceDriving } from './PlacePage';

/**
 * **A repository place's driving, for its head** (SETUPUX1h, the setup-UX design §2.1): the hold, and the ⋯'s drive and tree presses, each
 * the driver's own (`SET_DRIVABLE`, `SET_HOLD`, `SET_TREES`), the terminal's `daoris place …` twin beside. It reads the driver's state and
 * the roster, which the application holds, and nothing a section reads; what it shows of the place is the row `PLACES` answered.
 *
 * An organism holding the bridge's hooks, handed in by the application so the place imports none of them. Driving is set only by these
 * presses, never by editing the place's configuration.
 */
export function PlaceDrivingScope({ row, notify, onOpenAppointed, onOpenRepositories, children }: {
  row: PlaceRow;
  notify: Notify;
  onOpenAppointed?: () => void;
  onOpenRepositories?: () => void;
  children: (driving: PlaceDriving) => ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const setTrees = useSetTrees();
  const onError = failure(notify);
  const repository = row.repository ?? '';
  const standing = standingOf(driver.data?.standingConfirmation);
  const stewardSet = standing ? stewardSetAt(standing.stewardActs, repository) : null;
  const doors = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  return children({
    drivable: row.driven,
    held: row.held,
    ownTree: row.trees,
    busy: setDrivable.isPending || setHold.isPending || setTrees.isPending,
    note: !row.adopted && doorOf(roster.data?.adapter, doors) === 'native' ? t('projects.outside.directDoor') : undefined,
    stewardSet: stewardSet ? t('projects.setup.stewardSet', { time: clockOf(stewardSet, undefined, i18n.language) }) : null,
    terminal: `daoris place ${row.driven ? 'undrive' : 'drive'} ${repository}`,
    onDrive: (drivable, answered) => setDrivable.mutate({ repository, drivable }, {
      onSuccess: () => answered.done(),
      onError: (error) => answered.refused(sentence(error)),
    }),
    onHold: (held) => setHold.mutate({ repository, held }, { onError }),
    onTrees: (ownTree) => setTrees.mutate({ repository, ownTree }, { onError }),
    onOpenAppointed,
    onOpenRepositories,
  });
}
