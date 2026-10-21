import { readHarnessSettings, withDefault, writeHarnessSettings } from '../../src/agents/toolchain.ts';

/**
 * A wiring laid for a test the way the driver's `profile default` writes it. That verb is Daoris's driver program's since PUSH1f2e2
 * (`daoris-driver agent profile …`, held by its own tests); the verbs still here only read what it writes. A list laid as `profile
 * order` writes it served `agent list`'s tests alone, which left with it (PUSH1f2e4).
 */
export function defaulted(path: string, agent: string, profile: string | null, workspace: string | null): void {
  writeHarnessSettings(path, withDefault(readHarnessSettings(path), agent, profile, workspace));
}
