import { setupSteps } from './help/setup';
import type { StarterDoor } from './help/starters';
import { useMachine } from './help/useMachine';
import { GetStarted } from './settings/GetStarted';

/**
 * Settings → Get started (SETUP1a, D97): the organism over the guide. It reads the machine through the
 * one reading Ask Daoris's starters share, and hands the steps to the molecule that draws them.
 *
 * @remarks
 * The doors lead out of Settings — to another domain, to Projects, to the Workspace menu's drawers — so
 * where they go is the application's to decide, as Ask Daoris's starters' doors are.
 */
export function GetStartedDomain({ onGo }: { onGo: (door: StarterDoor) => void }) {
  const { machine, settled } = useMachine();

  // Until every answer is in, a step would read *to do* of a machine that has it done.
  return <GetStarted steps={setupSteps(machine)} reading={!settled} helper={machine.helper} onGo={onGo} />;
}
