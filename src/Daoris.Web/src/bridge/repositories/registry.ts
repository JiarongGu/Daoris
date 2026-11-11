import { useMutation } from '@tanstack/react-query';
import { getBridge } from '@shenora/react';
import { call, type Payload } from '../call';

// The registry's machine half: a folder the shell names, and a declaration written in place.

/**
 * What the shell can say about a folder on this machine — the one thing a page cannot find
 * out for itself, because a browser may never learn a filesystem path.
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
  /**
   * What its manifest says it uses, as the file has it. The registry replaces a row's `uses` with
   * the declaration's, so a registration made from this answer states them.
   */
  uses: string[];
};

/** A folder's excludes changed as `daoris repository excludes` edits them; the driver reads the folder's manifest again. */
export const useSetFolderExcludes = () => useMutation({
  mutationFn: (variables: Payload<'REGISTRY_EXCLUDES'>) => call('REGISTRY_EXCLUDES', variables),
});

/** *Open*: a department's folder in the file manager, by its registered name; the module names the folder, never the page. */
export const useOpenDepartmentFolder = () => useMutation({
  mutationFn: (repository: string) => call('REGISTRY_OPEN_FOLDER', { repository }) as Promise<{ opened: boolean }>,
});

/**
 * Ask the shell for a folder. Null is the person cancelling the dialog, which is an answer.
 *
 * @remarks
 * Not a query: it opens a modal, so it happens when the person asks and never on a refetch. The
 * registering that follows is an ordinary call to the loopback host. The dialog is owned by this page's window, and the call waits
 * for as long as it is open: under the bridge's default 30 seconds a slow pick would answer nobody.
 */
export const usePickFolder = () =>
  useMutation({
    mutationFn: () => getBridge().invoke<FolderInspection | null>('DAORIS.REGISTRY', 'PICK_FOLDER', { timeoutMs: Infinity }),
  });

/**
 * Edit a repository's own `daoris.json` through a form instead of a text editor.
 *
 * @remarks
 * Deliberately a different act from re-wiring, and kept visibly apart in the UI: this writes a TRACKED
 * file in that repository, and the diff lands uncommitted for its own review flow. Doctrine — rules,
 * knowledge, skills — stays unwritable from every surface; the manifest is inert data.
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
