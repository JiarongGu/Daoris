import { useState } from 'react';
import { useConvertPlan } from '../../bridge/shell';
import { DEFAULT_CHOICES, followsOf } from './convertPlan';

/**
 * The conversion as the application holds it (WORKFLOWCLEAN1g-i): whether its review is in front, and which places follow an old landing rule
 * (the census of the plan read for the default choices, the one `ConvertPanel` reads too), so a place's head can say so and offer *Convert…*.
 * The census is read only while the Agents place is in front.
 */
export function useConversion({ active }: { active: boolean }) {
  const [shown, setShown] = useState(false);
  const census = useConvertPlan(DEFAULT_CHOICES, { enabled: active });
  return {
    shown,
    show: () => setShown(true),
    close: () => setShown(false),
    follows: (place: { repository?: string | null; workspace?: string | null }) => followsOf(census.plan, place),
  };
}
