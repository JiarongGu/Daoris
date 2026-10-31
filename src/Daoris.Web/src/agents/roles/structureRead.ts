import { useMemo } from 'react';
import { refusedNoRoute } from '../../bridge/call';
import { type RoleStructure, useRoleStructure } from '../../bridge/driver';
import { useRegistry } from '../../data/queries';
import { heldMarks, placesToAsk } from './structure';
import type { StructureRead } from './StructureSections';

/**
 * **The one read behind Work, Accounts, By repository and an agent's *Used by*** (COMPANYVIEW1d, the company-view design §4): this
 * machine, each workspace and each repository, every work, asked once whatever the filters say, so a filter changes no read. The driver
 * composes it from the last readings alone, so asking probes nothing (D150 point 5). `loading` is the first reading not landed,
 * `notOffered` a shell older than the route, and `failed` any other refusal: none of them an empty list.
 */
export function useStructureRead(enabled: boolean): { answer: RoleStructure | null; read: StructureRead } {
  const registry = useRegistry('machine');
  const rows = registry.data;
  const places = useMemo(() => placesToAsk(rows ?? []), [rows]);
  const structure = useRoleStructure(places, undefined, { enabled });
  const read: StructureRead = structure.data
    ? 'ok'
    : structure.isError ? (refusedNoRoute(structure.error) ? 'notOffered' : 'failed') : 'loading';
  return { answer: structure.data ?? null, read };
}

/** Which sections of the list a held start marks (§3): from the read alone, and none while it is not read. */
export function useStructureMarks(enabled: boolean): { work: boolean; repository: boolean } {
  const { answer } = useStructureRead(enabled);
  return answer ? heldMarks(answer) : { work: false, repository: false };
}
