/** What a `session` proposal's helper or pass door asks, read from the terminal command the driver wrote for it. */
export type SessionHelperDoor =
  | { door: 'helper'; session: string; question: string; agent: string | null; after: string | null; toMe: boolean }
  | { door: 'pass'; ask: string };

const HELPER = /^daoris session helper (\S+) "([\s\S]*)"((?: --agent \S+| --after \S+| --to-me)*)$/;
const PASS = /^daoris session pass (\S+)$/;

/** The door a `session` proposal's command names, or null for an archive or any other command. */
export function sessionHelperDoor(terminal: string): SessionHelperDoor | null {
  const pass = PASS.exec(terminal);
  if (pass) return { door: 'pass', ask: pass[1]! };
  const helper = HELPER.exec(terminal);
  if (!helper) return null;
  const flags = helper[3]!;
  return {
    door: 'helper',
    session: helper[1]!,
    question: helper[2]!,
    agent: /--agent (\S+)/.exec(flags)?.[1] ?? null,
    after: /--after (\S+)/.exec(flags)?.[1] ?? null,
    toMe: flags.includes('--to-me'),
  };
}
