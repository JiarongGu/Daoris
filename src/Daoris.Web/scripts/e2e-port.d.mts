// The types of `e2e-port.mjs`, for the config and the test that import it. The script is plain JavaScript so the host
// script runs it on any Node the package supports; this declares what it exports.

export const webRoot: string;
export const E2E_BAND: { from: number; size: number };
export function e2ePort(root?: string, env?: Record<string, string | undefined>): number;
export function portIsFree(port: number): Promise<boolean>;
export function leasePort(port: number): Promise<boolean>;
export function releaseE2eLeases(): void;
export function freeE2ePort(
  root?: string,
  env?: Record<string, string | undefined>,
  isFree?: (port: number) => Promise<boolean>,
  lease?: (port: number) => Promise<boolean>,
): Promise<number>;
