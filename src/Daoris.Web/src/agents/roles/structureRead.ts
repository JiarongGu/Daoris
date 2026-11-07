import { useMemo } from 'react';
import { type RoleStructure, useDriver, useRoleStructure, useRoleStructureChain } from '../../bridge/driver';
import type { StructurePlaceAsk } from '../../data/wiring';
import { useRegistry } from '../../data/queries';
import { heldMarks, placesToAsk } from './roleStructure';
import type { StructureRead } from './StructureSections';

/**
 * **The read behind Work, Accounts, By repository and an agent's *Used by*** (COMPANYVIEW1d, the company-view design §4), scoped:
 * with a `chosen` place, that place and its parents alone; without one, this machine, each workspace and each *driven*
 * repository, never an imported one that is not driven, whatever the filters say, so a filter changes no read. The driver composes it
 * from the last readings alone, so asking probes nothing. `loading` is the first reading not landed and `failed` a
 * refusal: neither of them an empty list.
 */
export function useStructureRead(enabled: boolean, chosen?: StructurePlaceAsk | null): { answer: RoleStructure | null; read: StructureRead } {
  const registry = useRegistry('machine');
  const driver = useDriver();
  const rows = registry.data;
  const drivable = driver.data?.drivable;
  const places = useMemo(() => placesToAsk(rows ?? [], drivable ?? []), [rows, drivable]);
  const whole = useRoleStructure(places, undefined, { enabled: enabled && !chosen && drivable !== undefined });
  const chain = useRoleStructureChain(chosen ?? {}, undefined, { enabled: enabled && Boolean(chosen) });
  const structure = chosen ? chain : whole;
  const read: StructureRead = structure.data ? 'ok' : structure.isError ? 'failed' : 'loading';
  return { answer: structure.data ?? null, read };
}

/** Which sections of the list a held start marks (§3): from the read alone, and none while it is not read. */
export function useStructureMarks(enabled: boolean): { work: boolean; repository: boolean } {
  const { answer } = useStructureRead(enabled);
  return answer ? heldMarks(answer) : { work: false, repository: false };
}
