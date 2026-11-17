import type { UseMutationResult } from '@tanstack/react-query';
import type { PlaceRiskChange } from '../../settings/place/PlaceRisk';
import { type DriverState, useDriverChange } from '../driver';

/**
 * Change a place's risk or the owner's marks on it, over the record `daoris settings risk` edits: the window's press, the owner's own. An
 * agent's press of the same route goes on the owner's card in every mode. A refusal is the driver's sentence, verbatim.
 */
export const useSetPlaceRisk = () => useDriverChange('SET_PLACE_RISK') as UseMutationResult<DriverState, Error, PlaceRiskChange>;
