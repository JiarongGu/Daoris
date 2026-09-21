import { useTranslation } from 'react-i18next';
import { useSessionConsole } from './shell';
import { MonoWell } from './ui';

/**
 * A session's console, live (D49 §2) — the transcript capture as it happens.
 *
 * @remarks
 * An organism, deliberately: it holds the hook so that `MonoWell` — which does the rendering, the
 * tail-following and the drop footer — holds none, and can therefore be reviewed in every state
 * without a driver attached.
 *
 * It renders only where a shell is attached, because only a driver has a stream to give — in a
 * browser the hook asks nobody and this stays absent, which is the same rule the stop control
 * follows.
 *
 * Shared by the quest drawer (a driven session's record) and the chat drawer (a conversation), for
 * the reason those two are one entity in the first place: the console does not care which way in a
 * session was entered.
 */
export function SessionConsole({ id, tall, fill, quiet }: {
  id: string;
  tall?: boolean;
  fill?: boolean;
  /**
   * What to say instead of an empty well when this session has said nothing and is not saying
   * anything (SURF8).
   *
   * @remarks
   * A bordered empty box reads as a text field, which is the worst thing a read-only region can
   * look like — measured on the real monitor window, where a parked session with no buffer left a
   * 15rem input in the middle of it. A sentence is also the more honest shape: "nothing was said"
   * and "the console is showing you nothing" are different claims.
   */
  quiet?: string;
}) {
  const { t } = useTranslation();
  const { lines, live, dropped } = useSessionConsole(id);

  const silent = lines.length === 0 && !live;

  if (silent && quiet) {
    return <p className="m-0 py-1 text-small text-ink-faint">{quiet}</p>;
  }

  // `fill` is the output panel's mode (D55): the person gave the panel a height, so an empty
  // console keeps it rather than collapsing the region they just resized.
  if (silent && !fill) return null;

  return (
    <div className={fill ? 'flex min-h-0 flex-1 flex-col' : 'mt-3'}>
      <MonoWell
        // In the panel the region's own header already names it; a second `console` above the
        // well is the label repeated, not a label.
        label={fill ? undefined : t('console.label')}
        live={live}
        dropped={dropped}
        tall={tall}
        fill={fill}
        text={lines.map((line) => line.text).join('\n')}
      />
    </div>
  );
}
