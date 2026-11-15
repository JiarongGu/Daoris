export const LANDINGS: string;
export function recordLanding(home: string, landing: { commit: string | null; branch: string; at?: string }, options?: { env?: Record<string, string | undefined>; option?: string }): boolean;
