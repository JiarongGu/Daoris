import { useTranslation } from 'react-i18next';
import { Button, Tip } from '../../design/ui';
import type { Levels } from '../say';

/**
 * *Send now*, beside the default send while a turn runs: into the running turn where its agent steers; where the door takes
 * no word mid-turn but stops a turn keeping the session, the turn is stopped and the words are the next turn's first. Its tip
 * says which. Absent where the door does neither, since the press would only queue as *Send* does.
 */
export function SendNow({ levels, disabled, onPress }: { levels: Levels; disabled: boolean; onPress: () => void }) {
  const { t } = useTranslation();
  if (!levels.steers && !levels.stops) return null;
  return (
    <Tip content={t(levels.steers ? 'work.steer.nowTip' : 'work.steer.nowStopsTip')}>
      <Button type="button" disabled={disabled} onClick={onPress}>{t('work.steer.now')}</Button>
    </Tip>
  );
}
