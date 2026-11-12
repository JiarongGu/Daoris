import { type FormEvent, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Inline, MonoWell, SelectField } from '../design/ui';
import { ExternalLink, webAddress } from '../design/links';
import { authoredText } from '../locales/authored';
import { sentence } from '../locales/format';
import type { CommandArgShown, CommandShown, PluginCommandPress, PluginCommandRound } from './interfaces';

type ArgValue = string | number | boolean;

/** Where a run stands: its arguments and its confirm asked, a round under way, an ask waiting, or its end. */
type Stage = 'starting' | 'working' | 'asking' | 'ended' | 'cancelled' | 'refused';

const FIELD =
  'min-w-[min(14rem,100%)] flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:text-ink-faint';

/** The host a page to open names, said beside its door so the person sees where it goes. */
const hostOf = (address: string): string => new URL(address).host;

/** Each argument's value as the wire takes it: a number as a number, a box as yes or no, an empty field left out. */
function argsOf(args: readonly CommandArgShown[], typed: Record<string, string | boolean>): Record<string, ArgValue> {
  const values: Record<string, ArgValue> = {};
  for (const arg of args) {
    const value = typed[arg.name];
    if (arg.type === 'boolean') {
      if (value === true || arg.required) values[arg.name] = value === true;
    } else if (typeof value === 'string' && value.trim() !== '') {
      values[arg.name] = arg.type === 'number' ? Number(value) : value;
    }
  }
  return values;
}

/** One argument's field, by its type, labelled by its title and marked where it is required. */
function ArgField({ arg, value, language, onChange }: {
  arg: CommandArgShown; value: string | boolean | undefined; language: string; onChange: (value: string | boolean) => void;
}) {
  const { t } = useTranslation();
  const label = `${authoredText(arg.title, language)}${arg.required ? ` ${t('plugin.round.required')}` : ''}`;
  if (arg.type === 'boolean') return <CheckField checked={value === true} onChange={onChange} label={label} />;
  if (arg.type === 'choice') {
    return (
      <span className="flex flex-col gap-1 text-small text-ink-soft">
        <span>{label}</span>
        <SelectField value={typeof value === 'string' ? value : ''} onChange={onChange} ariaLabel={label} options={arg.choices.map((choice) => ({ value: choice, label: choice }))} />
      </span>
    );
  }
  return (
    <label className="flex flex-col gap-1 text-small text-ink-soft">
      <span>{label}</span>
      <input
        type={arg.type === 'number' ? 'number' : 'text'}
        value={typeof value === 'string' ? value : ''}
        onChange={(event) => onChange(event.target.value)}
        className={FIELD}
      />
    </label>
  );
}

/**
 * **The round card**: one of a plugin's commands run from the window, round by round, under the page's header. It asks the
 * command's arguments and its declared confirm first, where it has either, and starts at once where it has neither; then it draws
 * each round's lines in order, a page to open in the person's own browser with its host, a code in the mono face with *Copy*, and a
 * field for what the plugin asks, a password field where masked, with *Go on* and *Cancel*. Its end says done or the exit, and what
 * the plugin handed back.
 *
 * @remarks
 * **A masked value is the round's alone**: sent with the answers so far, cleared from its field at once, never drawn back, and
 * dropped with the card. **Cancel sends nothing more**: a round already under way finishes in its own process and its answer is
 * not drawn. A refused round says the driver's own sentence, since it names what to do.
 */
export function RoundCard({ plugin, pluginName, command, given, place, send, onEnded, onClose }: {
  plugin: string;
  pluginName: string;
  command: CommandShown;
  /** Arguments already known, the record a menu is opened on: filled in, still the person's to confirm. */
  given?: Record<string, string>;
  /** The place in force: `machine`, `workspace:<name>` or `repository:<id>`. */
  place: string;
  /** One round, as the bridge sends it. */
  send: (press: PluginCommandPress) => Promise<PluginCommandRound>;
  /** The run's last round, once it asks nothing more: the holder does what its `then` says. */
  onEnded: (round: PluginCommandRound) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = i18n.language.startsWith('zh') ? 'zh' : 'en';
  const asksFirst = command.args.length > 0 || command.confirm !== null;
  const [stage, setStage] = useState<Stage>(asksFirst ? 'starting' : 'working');
  const [typed, setTyped] = useState<Record<string, string | boolean>>({ ...given });
  const [rounds, setRounds] = useState<PluginCommandRound[]>([]);
  const [draft, setDraft] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  // The answers so far and the arguments, held for the run's rounds alone; a generation drops a cancelled round's late answer.
  const answers = useRef<Record<string, string>>({});
  const args = useRef<Record<string, ArgValue>>({});
  const generation = useRef(0);
  const last = rounds.at(-1) ?? null;
  const title = authoredText(command.title, language);

  const round = (run?: string) => {
    const mine = generation.current;
    setStage('working');
    void send({
      id: plugin, command: command.name, ...(run ? { run } : {}), args: args.current, answers: { ...answers.current }, place, language,
    }).then((answer) => {
      if (mine !== generation.current) return;
      setRounds((was) => [...was, answer]);
      if (answer.ask) setStage('asking');
      else {
        setStage('ended');
        answers.current = {};
        onEnded(answer);
      }
    }, (error: unknown) => {
      if (mine !== generation.current) return;
      answers.current = {};
      setRefusal(sentence(error));
      setStage('refused');
    });
  };

  // A command that asks nothing first starts as the card opens, once.
  const started = useRef(false);
  useEffect(() => {
    if (asksFirst || started.current) return;
    started.current = true;
    round();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- the first round is sent once, as the card opens
  }, []);

  const missing = command.args.some((arg) => arg.required && arg.type !== 'boolean' && !String(typed[arg.name] ?? '').trim());
  const start = (event: FormEvent) => {
    event.preventDefault();
    if (missing) return;
    args.current = argsOf(command.args, typed);
    round();
  };
  const answer = (event: FormEvent) => {
    event.preventDefault();
    if (!last?.ask) return;
    answers.current = { ...answers.current, [last.ask.name]: draft };
    setDraft('');
    round(last.run);
  };
  const cancel = () => {
    generation.current += 1;
    answers.current = {};
    setDraft('');
    setStage('cancelled');
  };
  const go = last?.then !== null && typeof last?.then === 'object' ? (last.then as { go?: { view?: unknown } }).go : undefined;
  const opened = last && stage === 'asking' ? webAddress(last.open) : null;

  return (
    <section
      aria-label={t('plugin.round.title', { plugin: pluginName, command: title })}
      className="mb-4 flex flex-col gap-2 rounded-control border border-line bg-page/60 px-3.5 py-2.5"
    >
      <h2 className="m-0 text-body font-medium text-ink">{t('plugin.round.title', { plugin: pluginName, command: title })}</h2>

      {stage === 'starting' && (
        <form className="flex flex-col gap-2" onSubmit={start}>
          {command.confirm !== null && <p className="m-0 text-body text-ink-soft">{authoredText(command.confirm, language)}</p>}
          {command.args.map((arg) => (
            <ArgField key={arg.name} arg={arg} value={typed[arg.name]} language={language} onChange={(value) => setTyped((was) => ({ ...was, [arg.name]: value }))} />
          ))}
          <span className="flex flex-wrap gap-2">
            <Button type="submit" variant="primary" disabled={missing}>{t('plugin.round.run')}</Button>
            <Button variant="ghost" onClick={onClose}>{t('plugin.round.cancel')}</Button>
          </span>
        </form>
      )}

      {rounds.some((each) => each.said.length > 0) && (
        <ul className="m-0 flex list-none flex-col gap-0.5 p-0 text-small text-ink-soft">
          {rounds.flatMap((each, at) => each.said.map((line, n) => <li key={`${at}-${n}`} className="break-words"><Inline text={line} /></li>))}
        </ul>
      )}

      {opened && (
        <span className="flex flex-wrap items-baseline gap-x-2 gap-y-1 text-small">
          <ExternalLink system href={opened} className="text-accent underline-offset-2 hover:underline">{t('plugin.round.open')}</ExternalLink>
          <span className="text-ink-faint">{hostOf(opened)}</span>
        </span>
      )}
      {last?.code && stage === 'asking' && (
        <span className="flex flex-wrap items-center gap-3">
          <span className="sr-only">{t('plugin.round.code')}</span>
          <code className="font-mono text-title tracking-wider text-ink">{last.code}</code>
          <Button variant="ghost" onClick={() => { void navigator.clipboard?.writeText(last.code!); setCopied(true); }}>
            {t(copied ? 'plugin.round.copied' : 'plugin.round.copy')}
          </Button>
        </span>
      )}

      {stage === 'asking' && last?.ask && (
        <form className="flex flex-col gap-1.5" onSubmit={answer}>
          <label className="flex flex-col gap-1 text-small text-ink-soft">
            <span>{last.ask.prompt}</span>
            <input
              autoFocus
              type={last.ask.masked ? 'password' : 'text'}
              autoComplete="off"
              spellCheck={false}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              className={FIELD}
            />
          </label>
          {last.ask.masked && <span className="text-meta text-ink-faint">{t('plugin.round.masked')}</span>}
          <span className="flex flex-wrap gap-2">
            <Button type="submit" variant="primary">{t('plugin.round.goOn')}</Button>
            <Button variant="ghost" onClick={cancel}>{t('plugin.round.cancel')}</Button>
          </span>
        </form>
      )}

      {stage === 'working' && (
        <span className="flex flex-wrap items-center gap-2 text-small text-ink-faint" aria-live="polite">
          {t('plugin.round.working')}
          <Button variant="ghost" onClick={cancel}>{t('plugin.round.cancel')}</Button>
        </span>
      )}
      {stage === 'ended' && last && (
        <p className="m-0 text-small text-ink-soft" role="status">
          {last.exit === 0 ? t('plugin.round.done') : t('plugin.round.failed', { exit: last.exit })}
        </p>
      )}
      {stage === 'ended' && typeof go?.view === 'string' && (
        <p className="m-0 text-small text-ink-faint"><Inline text={t('plugin.round.go', { view: go.view })} /></p>
      )}
      {stage === 'ended' && last?.data !== null && last?.data !== undefined && (
        <MonoWell label={t('plugin.round.data')} text={JSON.stringify(last.data, null, 2)} />
      )}
      {stage === 'cancelled' && <p className="m-0 text-small text-ink-soft" role="status">{t('plugin.round.cancelled')}</p>}
      {stage === 'refused' && refusal && (
        <p className="m-0 border-l-[3px] border-warn pl-3 text-small text-ink-soft" role="status"><Inline text={refusal} /></p>
      )}

      {(stage === 'ended' || stage === 'cancelled' || stage === 'refused') && (
        <span><Button variant="ghost" onClick={onClose}>{t('common.close')}</Button></span>
      )}
    </section>
  );
}
