import type { UseMutationResult } from '@tanstack/react-query';
import type { GrowthChange } from '../../settings/repository/Growth';
import { type DriverState, useDriverChange } from '../driver';

// The growth switch a repository's or a workspace's Setup sets, *New repositories and rooms*: the driver's own shape (`useDriverChange`), whose answer is
// the driver's state with the value set. What each repository resolves to is `LINES`' `growth`, in `lines.ts`.

/**
 * Change the growth switch of a repository or a workspace: whether an agent's press that opens a repository or a room runs as the access mode
 * says, is asked even in full access, or is refused, over the file `daoris settings growth` edits. The person's alone: this is a press of the
 * window, which no mode lets an agent make. A refusal is the driver's sentence, verbatim.
 */
export const useSetGrowth = () => useDriverChange('SET_GROWTH') as UseMutationResult<DriverState, Error, GrowthChange>;
