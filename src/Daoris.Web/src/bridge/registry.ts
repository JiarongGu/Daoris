import { useMutation } from '@tanstack/react-query';
import { getBridge } from '@shenora/react';

// The registry's machine half (MOD3): a folder the shell names, and a declaration written in place (D48 §7).

/**
 * What the shell can say about a folder on this machine (D48 §7) — the one thing a page cannot find
 * out for itself, because a browser may never learn a filesystem path (D46/D47 §4).
 */
export type FolderInspection = {
  path: string;
  name: string;
  exists: boolean;
  adopted: boolean;
  git: boolean;
  summary?: string;
  owns: string[];
  accepts: string[];
  packs: string[];
  join: boolean;
  shareKnowledge: boolean;
};

/**
 * Ask the shell for a folder. Null is the person cancelling the dialog, which is an answer.
 *
 * @remarks
 * Not a query: it opens a modal, so it happens when the person asks and never on a refetch. The
 * registering that follows is an ordinary call to the loopback host — routing it through IPC too
 * would be a second door onto the same judgement.
 */
export const usePickFolder = () =>
  useMutation({
    mutationFn: () =>
      getBridge().invoke<FolderInspection | null>('DAORIS.REGISTRY', 'PICK_FOLDER', {}),
  });

/**
 * Edit a repository's own `daoris.json` through a form instead of a text editor (D48 §7).
 *
 * @remarks
 * Deliberately a different act from re-wiring, and kept visibly apart in the UI: this writes a TRACKED
 * file in that repository, and the diff lands uncommitted for its own review flow. Doctrine — rules,
 * knowledge, skills — stays unwritable from every surface (D31); the manifest is inert data (D26).
 */
export type DeclarationEdit = {
  path: string;
  summary: string;
  owns: string[];
  accepts: string[];
  join: boolean;
  shareKnowledge: boolean;
};

export const useWriteDeclaration = () =>
  useMutation({
    mutationFn: (edit: DeclarationEdit) =>
      getBridge().invoke<FolderInspection>('DAORIS.REGISTRY', 'WRITE_DECLARATION', { payload: edit }),
  });
