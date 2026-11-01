import { fingerprintOf } from '../workflow/draftFingerprint';
import type { HelpWhere } from './where';

/**
 * The workflow draft open in the editor (FLOWCHART2d, D223; the flowchart design §4, §8.3), as the editor registers it while it is
 * mounted: Ask Daoris reads it as the person sends their words, so what it is told is frozen with that message, and an edit alone tells
 * nothing.
 */
export type OpenDraft = {
  /** The repository or workspace its workflow is for. */
  place: string;
  /** The graph exactly as the editor holds it (`graphOf`). */
  graph: { nodes?: readonly unknown[] } & Record<string, unknown>;
  /** The saved workflow it adds a version to, or the id it would save as new. */
  workflow?: string | null;
  /** The version it was opened from. */
  base?: number | null;
  /** Where it came from: `preset:<id>`, `<workflow>@v<n>` or `new`. */
  source?: string | null;
  /** The route the place chooses it for, where the editor was opened on one. */
  route?: string | null;
  pipeline?: 'development' | 'verification' | null;
  /** The step selected, by its id. */
  selected?: string | null;
  /** The problems the editor's judgement holds open on it, as the driver said them. */
  problems?: readonly { code: string; node?: string | null; wire?: string | null; words: string }[];
};

/** A draft as `SESSION_INPUT` carries it: the editor's, with the fingerprint of its graph. */
export type DraftSent = OpenDraft & { fingerprint: string };

let open: OpenDraft | null = null;

/** The editor's draft, held until the registration that made it lets go; a later registration replaces it. */
export function registerOpenDraft(draft: OpenDraft): () => void {
  open = draft;
  return () => {
    if (open === draft) open = null;
  };
}

/** The draft open now, or none. */
export function openDraftNow(): OpenDraft | null {
  return open;
}

/** The draft to send beside the person's words: none where there is none, or this conversation was told its fingerprint last. */
export function draftToTell(draft: OpenDraft | null, told: string | undefined): DraftSent | undefined {
  if (!draft) return undefined;
  const fingerprint = fingerprintOf(draft.graph);
  return fingerprint === told ? undefined : { ...draft, fingerprint };
}

/** The open draft as where the person is names it: its workflow, its place, how many steps, what is open on it. */
export function draftWhere(draft: OpenDraft | null): HelpWhere['draft'] {
  if (!draft) return null;
  return {
    workflow: draft.workflow ?? null, place: draft.place, base: draft.base ?? null, steps: draft.graph.nodes?.length ?? 0,
    problems: draft.problems?.length ?? 0, selected: draft.selected ?? null,
  };
}
