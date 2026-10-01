import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, QuickPanel, Tip } from '../ui';

/**
 * Quick Ask (DOCK1d): Ask Daoris's conversation in a box at the palette's place, for one question
 * without opening a region — VS Code's Quick Chat.
 *
 * @remarks
 * **The same conversation, not a second one**: the box holds the organism the side bar holds, so a
 * question asked here is read on there, and *Open in the side bar* carries on in the region with nothing
 * lost. It opens from its key, from the palette's *Quick Ask*, and from the palette's last row, which
 * asks what was typed.
 *
 * **Where the palette is, and shaped like it** (`QuickPanel`): near the top, not centred, since the
 * conversation grows downward; the title bar and the status bar stay live around it. Escape or a click
 * outside closes it, and the conversation goes on without it.
 *
 * A molecule: the conversation arrives made, and the two presses go out.
 */
export function QuickAsk({ open, onClose, onExpand, children }: {
  open: boolean;
  onClose: () => void;
  /** Closes the box and opens Ask Daoris's region, on the same conversation. */
  onExpand: () => void;
  /** Ask Daoris's conversation, unframed. */
  children: ReactNode;
}) {
  const { t } = useTranslation();

  return (
    <QuickPanel
      open={open}
      onClose={onClose}
      title={t('help.quick.title')}
      header={{
        icon: 'help',
        closeLabel: t('help.quick.close'),
        actions: (
          <Tip content={t('help.quick.expand')}>
            <Button variant="ghost" aria-label={t('help.quick.expand')} onClick={onExpand} className="h-7 w-7 justify-center px-0">
              <Icon name="layoutRight" size={14} />
            </Button>
          </Tip>
        ),
      }}
      wide
      fill
      // Into the message box, not onto the first button: the box is what it is opened for.
      initialFocus="textarea"
    >
      <div className="flex min-h-0 flex-1 flex-col">{children}</div>
    </QuickPanel>
  );
}
