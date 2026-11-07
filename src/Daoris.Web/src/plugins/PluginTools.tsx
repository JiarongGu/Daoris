import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill } from '../design/ui';
import { PageSection } from '../work/frame/ViewMain';
import type { ToolCheckShown, ToolShown } from './catalog';

/** A press the driver turned down (`PLUGIN_TOOL_FIX`'s `why`), with its own sentence, beside the check it is made on. */
export type ToolRefused = { tool: string; check: number; why: string; message: string };

/** The check a console is open for, by its tool's id and its number from 1. */
export type ToolFixing = { tool: string; check: number };

/** The codes the page words itself; any other is said in the driver's own sentence. */
const CODED = new Set(['fix_not_declared', 'not_found', 'not_started', 'refused', 'unknown_tool', 'unknown_check', 'no_fix']);

/**
 * **What a plugin needs, and where it stands**: each tool it declares with its range and why, and
 * once the person has pressed *Check*, whether it is found and each readiness check passed. A check that does not pass has
 * one press, *Sign in* or *Set up*, which opens a console the person sees running that check's command and nothing else;
 * the tool is read again when it closes. A tool not found says the plugin's own words for getting it, and presses nothing.
 *
 * @remarks
 * **A molecule**: the standing and the press in flight come in by props and every press goes out. Reading starts nothing
 * (a check may read another tool's sign-in); only *Check* asks the machine, and only the person's press opens a console.
 */
export function PluginTools({ plugin, tools, checking = false, fixing = null, refused = null, onCheck, onFix }: {
  /** The plugin's id, which a press names. */
  plugin: string;
  tools?: ToolShown[];
  /** A Check is on its way. */
  checking?: boolean;
  /** A console is open for this check; every press waits for it. */
  fixing?: ToolFixing | null;
  /** The press the driver turned down, said beside its check. */
  refused?: ToolRefused | null;
  onCheck: (plugin: string) => void;
  onFix: (plugin: string, tool: string, check: number) => void;
}) {
  const { t } = useTranslation();
  if (!tools?.length) return null;
  const busy = checking || fixing !== null;
  return (
    <PageSection title={t('plugin.section.tools')}>
      <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1.5">
        <p className="m-0 min-w-0 flex-1 basis-64 text-small text-ink-soft"><Inline text={t('plugin.tools.body')} /></p>
        <Button disabled={busy} aria-busy={checking || undefined} aria-label={t('plugin.tools.checkNamed', { id: plugin })} onClick={() => onCheck(plugin)}>
          {t(checking ? 'plugin.tools.checking' : 'plugin.tools.check')}
        </Button>
      </div>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {tools.map((tool) => (
          <ToolRow key={tool.id} plugin={plugin} tool={tool} busy={busy} fixing={fixing} refused={refused} onFix={onFix} />
        ))}
      </ul>
    </PageSection>
  );
}

/** One tool: its name, range and why; what the machine said; and its checks. */
function ToolRow({ plugin, tool, busy, fixing, refused, onFix }: {
  plugin: string;
  tool: ToolShown;
  busy: boolean;
  fixing: ToolFixing | null;
  refused: ToolRefused | null;
  onFix: (plugin: string, tool: string, check: number) => void;
}) {
  const { t } = useTranslation();
  return (
    <li className="flex flex-col gap-1 text-small text-ink-soft">
      <span className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
        <span className="text-body font-medium text-ink">{tool.name}</span>
        <code className="font-mono text-meta text-ink-faint">{tool.id}</code>
        {tool.versions && <span className="text-ink-faint">{t('plugin.tools.versions', { versions: tool.versions })}</span>}
      </span>
      {tool.for && <span>{tool.for}</span>}
      {tool.problem && <span><Inline text={tool.problem} /></span>}
      {tool.found === false && (
        <span className="flex flex-col gap-0.5">
          <span>{t('plugin.tools.notFound')}</span>
          {tool.install
            ? (
              <span className="flex flex-wrap items-baseline gap-x-1.5">
                <span>{t('plugin.tools.install')}</span>
                <code className="font-mono text-meta text-ink">{tool.install}</code>
              </span>
            )
            : <span>{t('plugin.tools.noInstall')}</span>}
        </span>
      )}
      {tool.found === true && tool.sentence && <span><Inline text={tool.sentence} /></span>}
      {tool.checks.length > 0 && (
        <ul className="m-0 mt-0.5 flex list-none flex-col gap-1.5 p-0">
          {tool.checks.map((check, at) => (
            <CheckRow
              key={check.says}
              plugin={plugin}
              tool={tool}
              check={check}
              number={at + 1}
              busy={busy}
              open={fixing?.tool === tool.id && fixing.check === at + 1}
              refused={refused?.tool === tool.id && refused.check === at + 1 ? refused : null}
              onFix={onFix}
            />
          ))}
        </ul>
      )}
    </li>
  );
}

/** One readiness check: where it stands, and, when it does not pass, the press its fix makes. */
function CheckRow({ plugin, tool, check, number, busy, open, refused, onFix }: {
  plugin: string;
  tool: ToolShown;
  check: ToolCheckShown;
  number: number;
  busy: boolean;
  open: boolean;
  refused: ToolRefused | null;
  onFix: (plugin: string, tool: string, check: number) => void;
}) {
  const { t } = useTranslation();
  const unmet = check.ready === false;
  const label = t(check.signIn ? 'plugin.tools.signIn' : 'plugin.tools.setUp');
  return (
    <li className="flex flex-col gap-0.5 border-l-2 border-line pl-2.5">
      <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
        {check.ready === true && <Pill tone="done">{t('plugin.tools.ready')}</Pill>}
        {unmet && <Pill tone="open">{t('plugin.tools.notReady')}</Pill>}
        <span className="text-ink">{check.says}</span>
        {unmet && check.fix && check.press && (
          <Button
            disabled={busy}
            aria-busy={open || undefined}
            aria-label={t(check.signIn ? 'plugin.tools.signInNamed' : 'plugin.tools.setUpNamed', { says: check.says })}
            onClick={() => onFix(plugin, tool.id, number)}
          >
            {label}
          </Button>
        )}
      </span>
      {unmet && check.fix && check.press && (
        <span className="flex flex-wrap items-baseline gap-x-1.5 text-meta text-ink-faint">
          <span>{t('plugin.tools.runs')}</span>
          <code className="font-mono text-ink">{check.fix}</code>
        </span>
      )}
      {check.fix && !check.press && check.refusal && <span className="text-meta"><Inline text={check.refusal} /></span>}
      {open && <span className="text-meta text-ink"><Inline text={t('plugin.tools.open', { fix: check.fix ?? '' })} /></span>}
      {refused && (
        <span className="text-meta text-ink-danger">
          {CODED.has(refused.why)
            ? <Inline text={t(`plugin.tools.refused.${refused.why}`, { message: refused.message })} />
            : <Inline text={refused.message} />}
        </span>
      )}
    </li>
  );
}
