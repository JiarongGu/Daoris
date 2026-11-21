import { useDriverChange } from './driver';

/** How long a step nobody's session answers stands before Daoris gives it up, in hours: the same `vacancyHours` `daoris settings vacancy <hours>` edits. */
export const useSetVacancy = () => useDriverChange('SET_VACANCY');
