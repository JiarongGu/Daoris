/**
 * One of Daoris's own plugins the install carries (PLUG9 d, D103), as the driver's `PLUGINS` answers it:
 * what it declares, what its README says it needs, and whether this machine has installed it. The
 * bridge leaves a null out, so an older shell's offer may carry no `problem`.
 *
 * Its card left with Settings → Plugins (UX6j, D150 §2.3): the Plugins place's list and offer page draw an offer now
 * (`plugins/catalog.ts`'s `OfferShown`), and this shape stays the bridge's until the plugins' files leave Settings.
 */
export type PluginOfferShown = {
  id: string;
  name: string;
  version: string;
  description: string;
  problem?: string | null;
  harnesses: string[];
  points: string[];
  servers: string[];
  needs: string[];
  installed: boolean;
  /** Its declared icon as its own bytes (PLUGUI2, D140 §3.2), never a path; absent until the host answers it. */
  icon?: string | null;
  iconProblem?: string | null;
};
