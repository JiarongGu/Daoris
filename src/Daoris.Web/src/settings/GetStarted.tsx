import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type SetupState, type SetupStep, setupProgress } from '../help/setup';
import type { StarterDoor } from '../help/starters';
import { Button, Card, CheckField, CodeText, Icon, Pill, Prose } from '../ui';

const TONE: Record<Exclude<SetupState, 'desktop'>, 'done' | 'open' | 'neutral'> = {
  done: 'done',
  // Something waits on the person: the waiting hue, never an outcome's (platform-ux §3).
  todo: 'open',
  optional: 'neutral',
};

/**
 * A door's name: the drawer it opens, else the Settings domain, else what agents may do (an agent's page at that part,
 * UX6e), else the view.
 */
const doorKey = (door: StarterDoor) => door.drawer ?? door.section ?? (door.agentPart === 'rules' ? 'agentRules' : door.view);

/**
 * Get started (SETUP1a, D97): the steps a machine needs, in the order a setup goes, each with its state,
 * the screens that do it and the commands that do the same at a terminal, shown and copyable (D50).
 *
 * @remarks
 * **Every step already has a screen, so this orders them and says which is done** — it sets nothing
 * itself (the design's rejected wizard). *Set up with Ask Daoris* hands the conversation a first message
 * naming the steps not yet done; with no agent named for it, the head says step 2 comes first, and the
 * guide stands on its own (D24's no-model tier).
 *
 * **In a browser** only the steps it can know are listed, and a sentence says the rest is the desktop's
 * (D47 §4); neither the hand-off nor opening at start is offered there, since neither exists.
 *
 * A molecule: the steps arrive read, a door press, the hand-off and the at-start choice go out.
 */
export function GetStarted({ steps, reading = false, helper, atStart, onAtStart, onGo, onAsk }: {
  steps: readonly SetupStep[];
  /** Whether the machine is still being read: no step is shown until every answer is in. */
  reading?: boolean;
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
  /** Whether the guide opens at start while the first steps are to do — the viewer's own choice. */
  atStart?: boolean;
  /** Absent in a browser, which never opens it at start. */
  onAtStart?: (open: boolean) => void;
  onGo: (door: StarterDoor) => void;
  /** Open Ask Daoris on this first message. Absent in a browser, which has no Ask Daoris. */
  onAsk?: (message: string) => void;
}) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState<string | null>(null);
  const progress = reading ? null : setupProgress(steps);
  const numbered = steps.map((step, index) => ({ step, number: index + 1, title: t(`setup.step.${step.id}.title`) }));
  const shown = reading ? [] : numbered.filter(({ step }) => step.state !== 'desktop');
  const elsewhere = !reading && numbered.some(({ step }) => step.state === 'desktop');

  const copy = async (command: string) => {
    try {
      await navigator.clipboard.writeText(command);
      setCopied(command);
      setTimeout(() => setCopied((was) => (was === command ? null : was)), 1500);
    } catch {
      // No clipboard here: the command is on the page to select by hand.
    }
  };

  const ask = () => {
    const left = numbered
      .filter(({ step }) => step.state === 'todo' || step.state === 'optional')
      .map(({ step, number, title }) => {
        const named = t('setup.ask.step', { number, title });
        return step.state === 'optional' ? t('setup.ask.optional', { step: named }) : named;
      });
    onAsk?.(t('setup.ask.message', { steps: left.join(t('setup.ask.join')) }));
  };

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
        <div className="min-w-0">
          <Prose className="text-small">{t('setup.intro')}</Prose>
          {progress && (
            <p className="m-0 mt-1 text-small font-medium tabular-nums text-ink">
              {progress.done === progress.of ? t('setup.progressDone') : t('setup.progress', progress)}
            </p>
          )}
        </div>
        {onAsk && !reading && (helper
          ? (
            <Button variant="primary" onClick={ask}>
              <Icon name="help" size={14} />
              {t('setup.ask.button')}
            </Button>
          )
          : <Prose className="text-small text-ink-faint">{t('setup.askNeedsAgent')}</Prose>)}
      </div>

      {reading && <Prose className="mt-4 text-small text-ink-faint">{t('setup.reading')}</Prose>}

      {!reading && (
      <ol aria-label={t('setup.steps')} className="m-0 mt-4 list-none p-0">
        {shown.map(({ step, number, title }) => (
          <li
            key={step.id}
            // Where Ask Daoris's go to a step lands (HELP6), as a menu's part lands on its card.
            id={`settings-step-${step.id}`}
            aria-label={`${number}. ${title}`}
            className="flex scroll-mt-3 flex-wrap items-start justify-between gap-x-6 gap-y-2 border-t border-line py-3 first:border-t-0 first:pt-0 last:pb-0"
          >
            {/* Wrapping, not a fixed grid (SETUP1, looked at): beside a side bar the card is narrow, and a
                grid squeezed both columns until a command broke mid-word and a door's label took two lines.
                Here the doors drop under the text when there is no room beside it. */}
            <div className="min-w-[min(18rem,100%)] flex-1">
              <div className="flex flex-wrap items-center gap-2 text-body">
                <span className="tabular-nums text-ink-faint">{number}.</span>
                <span className="font-medium text-ink">{title}</span>
                {step.state !== 'desktop' && <Pill tone={TONE[step.state]}>{t(`setup.state.${step.state}`)}</Pill>}
              </div>
              <p className="m-0 mt-0.5 text-small text-ink-faint">{t(`setup.step.${step.id}.when`)}</p>
              {/* Not a nested list: a step is one item, and its commands are part of what it says. */}
              <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1">
                {step.commands.map((command) => (
                  <span key={command} className="inline-flex min-w-0 items-center gap-1">
                    <CodeText text={command} className="text-meta text-ink-soft" />
                    <button
                      type="button"
                      aria-label={t('setup.copy', { command })}
                      onClick={() => void copy(command)}
                      className="inline-flex cursor-pointer items-center gap-1 rounded-control border-0 bg-transparent p-0.5 text-meta text-ink-faint hover:text-ink"
                    >
                      <Icon name={copied === command ? 'check' : 'copy'} size={12} />
                      {copied === command && <span>{t('setup.copied')}</span>}
                    </button>
                  </span>
                ))}
              </div>
            </div>
            {step.doors.length > 0 && (
              <div className="flex shrink-0 flex-wrap items-center gap-2">
                {step.doors.map((door) => (
                  <Button
                    key={doorKey(door)}
                    // What is left to do is the control to press; a done step's screen is still there to change.
                    variant={step.state === 'todo' ? 'default' : 'ghost'}
                    className="text-small whitespace-nowrap"
                    onClick={() => onGo(door)}
                  >
                    {t(`setup.door.${doorKey(door)}`)}
                  </Button>
                ))}
              </div>
            )}
          </li>
        ))}
      </ol>
      )}

      {elsewhere && <Prose className="mt-3 text-small text-ink-faint">{t('setup.desktop')}</Prose>}

      {onAtStart && (
        <div className="mt-4 border-t border-line pt-3">
          <CheckField
            label={t('setup.atStart')}
            checked={atStart === false}
            onChange={(off) => onAtStart(!off)}
            className="text-small"
          />
        </div>
      )}
    </Card>
  );
}
