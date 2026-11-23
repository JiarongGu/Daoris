import { useDriverChange } from './driver';

/** What the machine keeps free for the person's own work, in whole GB or null for auto: the same `reserveGb` `daoris settings reserve <gb>|auto` edits. */
export const useSetReserve = () => useDriverChange('SET_RESERVE');
