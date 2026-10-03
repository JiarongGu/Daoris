import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { SessionConsole } from '../SessionConsole';
import {
  type GitTarget, useToolDelete, useToolDownload, useToolGit, useToolLocation, useToolPick, useTools, useToolsEnded,
  useToolsLook, useToolStop, useToolUse,
} from '../shell';
import { failure, Inline, type Notify, Prose, useErrorNotify } from '../ui';
import { DomainLoading } from './DomainLoading';
import {
  choiceOf, endSaid, type ToolChoice, ToolCard, ToolLocations, type ToolLookShown, type ToolShown, type ToolUse,
} from './Tools';

/**
 * Settings → Tools (TOOLS7, D121 §4.1): each program Daoris runs beside its agents — Git, Node.js, PowerShell, GitHub
 * CLI, Azure CLI — as the system's, a managed version, or a file the person names, and the resource locations its
 * versions come from. The screen's half of `daoris tool` (D50), over the same `tools.json`. It holds the list, the
 * presses, the choice on each card and git's question; the cards and the locations below it hold none.
 *
 * @remarks
 * **A download is followed** (§3.6): the press answers once it has started, the card shows its console and its stop
 * while the shell says it runs, and its end is said wherever Settings is. **A switch of git is said before it applies**:
 * its press asks the two gits first (`TOOLS_GIT`), and the switch is the second press.
 */
export function ToolsDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  // The list at once, then again with each file's version, which starts each program: drawn from whichever is newer.
  const quick = useTools();
  const asked = useTools({ ask: true });
  const use = useToolUse();
  const download = useToolDownload();
  const stop = useToolStop();
  const remove = useToolDelete();
  const look = useToolsLook();
  const location = useToolLocation();
  const pick = useToolPick();
  useErrorNotify(quick.error, notify);
  const failed = failure(notify);

  const [choices, setChoices] = useState<Record<string, ToolChoice>>({});
  const [asking, setAsking] = useState<ToolUse | null>(null);
  const [looks, setLooks] = useState<ToolLookShown[] | null>(null);
  const target: GitTarget | null = asking
    ? { way: asking.action, ...(asking.version ? { version: asking.version } : {}), ...(asking.file ? { file: asking.file } : {}) }
    : null;
  const git = useToolGit(target);

  useToolsEnded((ended) => notify(endSaid(t, ended), ended.exitCode === 0 || ended.stopped ? 'ok' : 'error'));

  const state = asked.data ?? quick.data;
  if (!state && quick.isFetching) return <DomainLoading rows={5} />;
  if (!state) return null;

  const choiceFor = (tool: ToolShown) => choices[tool.tool] ?? choiceOf(tool);
  const choose = (tool: ToolShown, choice: ToolChoice) => setChoices((held) => ({ ...held, [tool.tool]: choice }));
  // Once a press applies, the card opens on what is now set.
  const settled = (tool: ToolShown) => setChoices(({ [tool.tool]: _, ...rest }) => rest);

  const apply = (tool: ToolShown, chosen: ToolUse) => {
    setAsking(null);
    use.mutate({ tool: tool.tool, ...chosen }, {
      onSuccess: (answer) => {
        settled(tool);
        if (answer.started) return;
        if (answer.ended) {
          notify(endSaid(t, answer.ended), answer.ended.exitCode === 0 ? 'ok' : 'error');
          return;
        }
        notify(chosen.action === 'file'
          ? t('settings.tools.set.file', { name: tool.name, file: answer.file ?? chosen.file ?? '' })
          : t('settings.tools.set.system', { name: tool.name }));
      },
      onError: failed,
    });
  };

  const browse = (tool: ToolShown) => pick.mutate(t('settings.tools.pickTitle', { name: tool.name }), {
    onSuccess: (answer) => {
      if (!answer.can) {
        notify(t('settings.tools.noPicker'), 'error');
        return;
      }
      if (answer.file) choose(tool, { ...choiceFor(tool), way: 'file', file: answer.file });
    },
    onError: failed,
  });

  return (
    <>
      <Prose className="mb-1"><Inline text={t('settings.tools.intro')} /></Prose>
      <Prose className="text-small"><Inline text={t('settings.tools.terminal')} /></Prose>

      {/* The file both doors edit does not read: every tool is refused until it is fixed, in the driver's own words. */}
      {state.problem && (
        <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
          <Inline text={t('settings.tools.fileProblem', { problem: state.problem })} />
        </p>
      )}

      {state.tools.map((tool) => {
        const isGit = tool.tool === 'git';
        const askingHere = isGit && asking ? {
          use: asking,
          reading: git.isFetching,
          answer: git.data,
          // A refusal is said on the card, where the question was asked, rather than in a toast.
          problem: git.error ? sentence(git.error) : undefined,
        } : null;
        return (
          <ToolCard
            key={tool.tool}
            tool={tool}
            choice={choiceFor(tool)}
            busy={use.isPending || download.isPending || remove.isPending}
            asking={askingHere}
            console={tool.running ? <SessionConsole id={`tools:${tool.tool}`} /> : undefined}
            onChoice={(choice) => choose(tool, choice)}
            onUse={(chosen) => apply(tool, chosen)}
            onAsk={isGit ? setAsking : undefined}
            onDownload={(version) => download.mutate({ tool: tool.tool, version }, {
              onSuccess: (answer) => {
                if (!answer.started && answer.ended) notify(endSaid(t, answer.ended), answer.ended.exitCode === 0 ? 'ok' : 'error');
              },
              onError: failed,
            })}
            onStop={() => stop.mutate(tool.tool, { onError: failed })}
            onDelete={(version) => remove.mutate({ tool: tool.tool, version }, {
              onSuccess: () => {
                settled(tool);
                notify(t('settings.tools.deleted', { name: tool.name, version }));
              },
              onError: failed,
            })}
            onBrowse={() => browse(tool)}
          />
        );
      })}

      <ToolLocations
        locations={state.locations}
        builtIn={state.builtIn}
        looks={looks}
        looking={look.isPending}
        busy={location.isPending}
        onLook={() => look.mutate(state.locations.length, {
          onSuccess: (answer) => {
            setLooks(answer.looks);
            const fetched = answer.looks.filter((each) => each.outcome === 'fetched').length;
            notify(t('settings.tools.looked', { fetched, count: answer.looks.length }),
              fetched === answer.looks.length ? 'ok' : 'error');
          },
          onError: failed,
        })}
        onAdd={(address) => location.mutate({ action: 'add', address }, {
          onSuccess: (answer) => notify(t(answer.added ? 'settings.tools.locations.added' : 'settings.tools.locations.already', { address })),
          onError: failed,
        })}
        onRemove={(address) => location.mutate({ action: 'remove', address }, {
          onSuccess: () => notify(t('settings.tools.locations.removed', { address })),
          onError: failed,
        })}
      />
    </>
  );
}
