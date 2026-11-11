import { useEffect, useState } from 'react';
import { type PluginCommandPress, type PluginCommandRound, usePluginCommand } from '../bridge/pluginCommand';
import { usePluginSwitch } from '../bridge/pluginSwitch';
import { useWorkspaceHoldings } from '../data/queries';
import { useScope } from '../data/scope';
import { failure, type Notify } from '../design/ui';
import type { PluginShown } from './catalog';
import { tookCommand, useAskedCommand } from './commandAsks';
import { declaresInterfaces, NO_INTERFACES } from './interfaces';
import { type PlaceShown, PluginSetup } from './PluginSetup';
import { RoundCard } from './RoundCard';

/** The command whose round card is open: its plugin, its name, and the ask that opened it, which keys the card. */
type Running = { plugin: string; command: string; n: number; given?: Record<string, string> };

/**
 * **A plugin's commands and switches in the Plugins view**: the *Set up* tab's body, the round card of the command running, the
 * sign-out a removal asks first, and the palette's asks, which open the plugin's page at its card.
 *
 * @remarks
 * A run is at the window's place: its scope's workspace, else this machine. A sign-out before a removal runs the declared `forget`
 * one round, at this machine, and its end is said whether or not the plugin confirmed it; the removal goes on either way.
 */
export function usePluginCommands({ plugins, answered, onChoose, refetch, notify }: {
  plugins: readonly PluginShown[];
  /** The catalogue has an answer, so a palette's ask can find its plugin. */
  answered: boolean;
  onChoose: (item: string) => void;
  /** The catalogue asked again, after a command whose end does not say to stay. */
  refetch: () => void;
  notify: Notify;
}) {
  const command = usePluginCommand();
  const switching = usePluginSwitch();
  const holdings = useWorkspaceHoldings();
  const { workspace } = useScope();
  const asked = useAskedCommand();
  const [running, setRunning] = useState<Running | null>(null);
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!asked || !answered) return;
    tookCommand(asked.n);
    if (!plugins.some((plugin) => plugin.id === asked.plugin)) return;
    onChoose(asked.plugin);
    setRunning({ plugin: asked.plugin, command: asked.command, n: -asked.n, ...(asked.given ? { given: asked.given } : {}) });
  }, [asked, answered, plugins, onChoose]);

  const place = workspace ? `workspace:${workspace}` : 'machine';
  const places: PlaceShown[] = (holdings.data ?? []).flatMap((each) => [
    { key: `workspace:${each.name}` },
    ...each.members.map((member) => ({ key: `repository:${member}`, workspace: each.name })),
  ]);
  const send = (press: PluginCommandPress) => command.mutateAsync(press);
  const ended = (round: PluginCommandRound) => {
    if (round.then === 'back') setRunning(null);
    else if (round.then !== 'stay') refetch();
  };
  const run = (plugin: string, name: string) => {
    setCount((was) => was + 1);
    setRunning({ plugin, command: name, n: count + 1 });
  };

  /** What the chosen plugin's page draws of its interfaces. */
  const pageParts = (plugin: PluginShown) => {
    const interfaces = plugin.interfaces ?? NO_INTERFACES;
    const declares = declaresInterfaces(interfaces);
    const shown = running?.plugin === plugin.id ? interfaces.commands.find((each) => each.name === running.command) : undefined;
    const storage = interfaces.storage;
    return {
      setup: declares || plugin.points.length > 0 ? (
        <PluginSetup
          id={plugin.id} enabled={plugin.enabled} refused={Boolean(plugin.problem)} interfaces={interfaces} points={plugin.points}
          switches={plugin.switches ?? []} place={place} places={places} running={shown?.name ?? null} switching={switching.isPending}
          onRun={(each) => run(plugin.id, each.name)}
          onSwitch={(what, at, on) => switching.mutate({ id: plugin.id, what, place: at, on }, { onError: failure(notify) })}
        />
      ) : undefined,
      setupFirst: declares,
      round: shown && running ? (
        <RoundCard
          key={running.n} plugin={plugin.id} pluginName={plugin.name || plugin.id} command={shown} given={running.given} place={place} send={send}
          onEnded={ended} onClose={() => setRunning(null)}
        />
      ) : undefined,
      signOut: storage && (storage.forget || storage.forgetDisabled)
        ? { command: storage.forgetDisabled ? null : storage.forget, why: storage.forgetDisabled }
        : null,
    };
  };

  return { pageParts };
}
