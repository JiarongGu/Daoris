import { createContext } from 'react';

/**
 * The column a place's page keeps for the chart of its open Workflow section: the element where the
 * page holds 64 rem and sets its sections in a column beside it, so the chart stands apart and the head and the three section lines keep
 * the first screen; `null` where the page holds less, and the section then offers the chart one press away. `undefined` outside a page,
 * where a Workflow section measures its own room.
 */
export const PlaceAside = createContext<HTMLElement | null | undefined>(undefined);
