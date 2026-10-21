import { readHarnessSettings, withDefault, writeHarnessSettings } from '../../src/agents/toolchain.ts';
import { withRotation } from '../../src/accounts/rotation.ts';

/**
 * A wiring laid for a test the way the driver's `profile order` and `profile default` write it. Those verbs are Daoris's driver program's
 * since PUSH1f2e2 (`daoris-driver agent profile …`, held by its own tests); the verbs still here only read what they write.
 */
export function ordered(path: string, agent: string, list: string[] | null, workspace: string | null): void {
  writeHarnessSettings(path, withRotation(readHarnessSettings(path), agent, list, workspace));
}

/** A scope's default laid as `profile default` writes it, or cleared with null. */
export function defaulted(path: string, agent: string, profile: string | null, workspace: string | null): void {
  writeHarnessSettings(path, withDefault(readHarnessSettings(path), agent, profile, workspace));
}
