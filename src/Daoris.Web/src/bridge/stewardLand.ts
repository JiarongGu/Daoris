import { useMutation } from '@tanstack/react-query';
import { call } from './call';

/** The press of *Overrule* on an allowed `land` waiting its window: the request, and the session it would land. */
export type StewardLandPress = { ask: string; session: string };

/**
 * The person's *Overrule* of a steward's `land` that a supervisor allowed and that waits its window (`STEWARD_LAND_OVERRULE`, the
 * person's alone): it does not land, and their own card for it follows. The answer says whether it took; the driver's state tells the
 * line, so nothing is read again here. Absent in a browser, where nothing holds the steward.
 */
export const useOverruleStewardLand = () =>
  useMutation({
    mutationFn: async (press: StewardLandPress): Promise<boolean> => {
      const said = (await call('STEWARD_LAND_OVERRULE', press)) as { done?: unknown } | null;
      return said?.done === true;
    },
  });
