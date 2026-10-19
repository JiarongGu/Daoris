import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, Inline } from '../../design/ui';
import { windowName } from './accounts';
import { type CurvePoint, curveProblem, curveSentence, MOST_POINTS, smartCurve } from './usageCurves';

// The usage curve as the switch's setting shows it (SWITCH1d; D165 point 2; the switch design §5): a small chart a window, the
// curve in effect with each account of the list where it stands now, and the editor *My own curve* opens on a window's points.
// Molecules: every state arrives as props and every save goes out, so each is a story.

/** One account of the list on a window now: how much of the window has gone and how much is used, each a percent. */
export type CurveAccount = { name: string; label: string; gone: number; used: number; over: boolean };

// The chart's own box, in its viewBox's units: the plot, with room for the ticks on its left and under it.
const BOX = { width: 240, height: 132, left: 32, right: 8, top: 8, bottom: 22 } as const;
const PLOT = { width: BOX.width - BOX.left - BOX.right, height: BOX.height - BOX.top - BOX.bottom };
const xOf = (gone: number) => BOX.left + (Math.min(Math.max(gone, 0), 100) / 100) * PLOT.width;
const yOf = (used: number) => BOX.top + (1 - Math.min(Math.max(used, 0), 100) / 100) * PLOT.height;

/** A curve across the whole window: its points, flat from the window's start to the first and from the last to its end. */
function across(points: readonly CurvePoint[]): CurvePoint[] {
  const first = points[0]!;
  const last = points[points.length - 1]!;
  return [
    ...(first[0] > 0 ? [[0, first[1]] as CurvePoint] : []),
    ...points,
    ...(last[0] < 100 ? [[100, last[1]] as CurvePoint] : []),
  ];
}

/**
 * **A window's curve, drawn** (the switch design §5): across, how much of the window's time has gone; up, how much is used. The
 * curve in effect is one 2px line with its room under it washed in, the person's own points marked on it, and each account of the
 * list a dot where it stands now, named beside it, over its curve in the warning hue. One series, so no legend: the window's name
 * is the chart's name, and each dot's tooltip says its account's numbers, which its row on the page says too.
 */
export function CurveChart({ window, points, own, accounts }: {
  window: string;
  /** The curve in effect on this window: the person's points under *My own curve*, else Daoris's. */
  points: CurvePoint[];
  /** Whether the points are the person's own, which are marked on the line. */
  own: boolean;
  accounts: CurveAccount[];
}) {
  const { t } = useTranslation();
  const line = across(points);
  const path = line.map(([gone, used], at) => `${at === 0 ? 'M' : 'L'} ${xOf(gone)} ${yOf(used)}`).join(' ');
  const room = `${path} L ${xOf(100)} ${yOf(0)} L ${xOf(0)} ${yOf(0)} Z`;
  const name = windowName(window, t);
  return (
    <figure className="m-0 min-w-0">
      <svg
        role="img"
        aria-label={t('harness.curve.chart', { window: name })}
        viewBox={`0 0 ${BOX.width} ${BOX.height}`}
        className="block h-auto w-full"
      >
        {/* Hairline gridlines at none, half and all used; the ticks in the muted ink. */}
        {[0, 50, 100].map((used) => (
          <g key={`y${used}`}>
            <line x1={xOf(0)} x2={xOf(100)} y1={yOf(used)} y2={yOf(used)} className="stroke-line" strokeWidth={1} />
            <text x={BOX.left - 4} y={yOf(used)} textAnchor="end" dominantBaseline="central" className="fill-ink-faint text-meta">{`${used}%`}</text>
          </g>
        ))}
        {[0, 50, 100].map((gone) => (
          <text key={`x${gone}`} x={xOf(gone)} y={BOX.height - 6} textAnchor={gone === 0 ? 'start' : gone === 100 ? 'end' : 'middle'} className="fill-ink-faint text-meta">
            {`${gone}%`}
          </text>
        ))}
        <path d={room} className="fill-accent opacity-10" />
        <path
          d={path}
          data-curve={line.map(([gone, used]) => `${gone}:${used}`).join(' ')}
          className="fill-none stroke-accent"
          strokeWidth={2}
          strokeLinejoin="round"
          strokeLinecap="round"
        />
        {own && points.map(([gone, used], at) => (
          <circle key={`p${at}`} data-point cx={xOf(gone)} cy={yOf(used)} r={4} className="fill-accent stroke-page" strokeWidth={2} />
        ))}
        {accounts.map((account) => {
          const x = xOf(account.gone);
          const y = yOf(account.used);
          // A name past the plot's middle is set to the dot's left, so it is never cut at the chart's edge.
          const left = account.gone > 60;
          return (
            <g key={account.name} data-account={account.name}>
              <title>{t('harness.curve.account', { account: account.label, used: `${Math.round(account.used)}%`, gone: `${Math.round(account.gone)}%` })}</title>
              <circle cx={x} cy={y} r={4} className={account.over ? 'fill-warn stroke-page' : 'fill-ink-soft stroke-page'} strokeWidth={2} />
              <text x={left ? x - 7 : x + 7} y={y} textAnchor={left ? 'end' : 'start'} dominantBaseline="central" className="fill-ink-soft text-meta">
                {account.label}
              </text>
            </g>
          );
        })}
      </svg>
      <figcaption className="mt-0.5 text-meta text-ink-faint">{t('harness.curve.axes')}</figcaption>
    </figure>
  );
}

type Draft = [elapsed: string, used: string][];

const draftOf = (points: readonly CurvePoint[]): Draft => points.map(([elapsed, used]) => [String(elapsed), String(used)]);

/** A field's number, or not a number where it holds none: an empty field is no point, never 0. */
const numberOf = (text: string) => (text.trim() === '' ? Number.NaN : Number(text));

/**
 * **A window's own points, edited** (the switch design §2, §5): *My own curve*'s points for one window, `[time gone, used]`, a
 * row each, added after the last and taken out, and saved whole as `--curve <window>=…` saves them. A window the person set no
 * points for takes Daoris's curve, and its own begin from Daoris's points. Points that break a rule are said in the door's own
 * sentence (`ACCOUNT_USE_CURVE`, the page's twin of its rules) and not saved. It holds only the points being typed; keyed by the
 * saved points, so a save that lands starts it afresh.
 */
export function CurvePoints({ window, points, hint, busy, onSave }: {
  window: string;
  /** The person's points for this window, or null where it takes Daoris's curve. */
  points: CurvePoint[] | null;
  /** The terminal's twin for this window. */
  hint: string;
  busy?: boolean;
  onSave: (points: CurvePoint[]) => void;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<Draft | null>(points ? draftOf(points) : null);
  const [problem, setProblem] = useState<string | null>(null);
  const name = windowName(window, t);

  if (!draft) {
    return (
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <p className="m-0 text-small text-ink-soft">{t('harness.curve.daoris', { window: name })}</p>
          <Button variant="ghost" disabled={busy} onClick={() => setDraft(draftOf(smartCurve(window) ?? [[0, 90]]))}>
            {t('harness.curve.setOwn')}
          </Button>
        </div>
        {hint && <div className="mt-1 text-small text-ink-faint"><Inline text={hint} /></div>}
      </div>
    );
  }

  const set = (at: number, part: 0 | 1, text: string) => {
    setProblem(null);
    setDraft(draft.map((row, index) => (index === at ? (part === 0 ? [text, row[1]] : [row[0], text]) : row)));
  };
  const add = () => {
    const last = draft[draft.length - 1]!;
    const after = Number.isFinite(numberOf(last[0])) ? Math.min(100, numberOf(last[0]) + 10) : 100;
    setProblem(null);
    setDraft([...draft, [String(after), last[1]]]);
  };
  const save = () => {
    const parsed = draft.map(([elapsed, used]) => [numberOf(elapsed), numberOf(used)] as CurvePoint);
    const wrong = curveProblem({ [window]: parsed });
    if (wrong) {
      setProblem(curveSentence(wrong));
      return;
    }
    onSave(parsed);
  };
  const field = 'w-[4.5rem] rounded-control border border-line-strong bg-raised px-2 py-1 text-right text-body text-ink';

  return (
    <fieldset className="m-0 min-w-0 border-0 p-0">
      <legend className="p-0 text-small font-medium text-ink">{t('harness.curve.points', { window: name })}</legend>
      <div className="mt-1 grid w-fit grid-cols-[auto_auto_auto] items-center gap-x-2 gap-y-1">
        <span className="text-meta text-ink-faint">{t('harness.curve.goneHead')}</span>
        <span className="text-meta text-ink-faint">{t('harness.curve.usedHead')}</span>
        <span />
        {draft.map(([elapsed, used], at) => (
          // Rows are positions, so a row's key is its place: taking one out renumbers the rest, as the door counts them.
          <div key={at} className="contents">
            <input
              type="number" min={0} max={100} step={1} value={elapsed} disabled={busy}
              aria-label={t('harness.curve.gone', { point: at + 1 })}
              onChange={(event) => set(at, 0, event.target.value)}
              className={field}
            />
            <input
              type="number" min={0} max={100} step={1} value={used} disabled={busy}
              aria-label={t('harness.curve.used', { point: at + 1 })}
              onChange={(event) => set(at, 1, event.target.value)}
              className={field}
            />
            <Button
              variant="ghost"
              disabled={busy || draft.length === 1}
              aria-label={t('harness.curve.remove', { point: at + 1 })}
              className="min-w-7 justify-center px-1.5"
              onClick={() => { setProblem(null); setDraft(draft.filter((_, index) => index !== at)); }}
            >
              <Icon name="x" size={13} />
            </Button>
          </div>
        ))}
      </div>
      <div className="mt-1.5 flex flex-wrap items-center gap-2">
        <Button variant="ghost" disabled={busy || draft.length >= MOST_POINTS} onClick={add}>
          <Icon name="plus" size={13} />
          {t('harness.curve.add')}
        </Button>
        <Button disabled={busy} onClick={save}>{t('harness.curve.save')}</Button>
        {points === null && (
          <Button variant="ghost" disabled={busy} onClick={() => { setProblem(null); setDraft(null); }}>{t('common.cancel')}</Button>
        )}
      </div>
      {problem && <p role="alert" className="m-0 mt-1 text-small text-ink [overflow-wrap:anywhere]">{problem}</p>}
      {hint && <div className="mt-1 text-small text-ink-faint"><Inline text={hint} /></div>}
    </fieldset>
  );
}
