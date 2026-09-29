import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import * as Dialog from '@radix-ui/react-dialog';
import { Button, Icon, Tip } from '../ui';

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
 * **Where the palette is, and shaped like it**: near the top, not centred, since the conversation grows
 * downward; the title bar and the status bar stay live around it (`tokens.test.ts`'s scrim bounds).
 * Escape or a click outside closes it, and the conversation goes on without it.
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
    <Dialog.Root open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed bottom-6 left-12 right-0 top-9 z-20 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          aria-modal="true"
          // Into the message box, not onto the first button: the box is what it is opened for.
          onOpenAutoFocus={(event) => {
            const box = (event.currentTarget as HTMLElement | null)?.querySelector('textarea');
            if (box) {
              event.preventDefault();
              box.focus();
            }
          }}
          className="fixed left-1/2 top-[12vh] z-20 flex h-[min(34rem,72vh)] w-[min(42rem,92vw)] -translate-x-1/2 flex-col overflow-hidden rounded-overlay border border-line bg-overlay shadow-[0_12px_48px_rgb(15_12_8/0.22)] focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]"
        >
          <header className="flex shrink-0 items-center gap-2 border-b border-line px-3.5 py-2">
            <Icon name="help" size={15} className="text-ink-soft" />
            <Dialog.Title className="m-0 text-body font-semibold text-ink">{t('help.quick.title')}</Dialog.Title>
            <span className="ml-auto flex items-center gap-0.5">
              <Tip content={t('help.quick.expand')}>
                <Button variant="ghost" aria-label={t('help.quick.expand')} onClick={onExpand} className="h-7 w-7 justify-center px-0">
                  <Icon name="layoutRight" size={14} />
                </Button>
              </Tip>
              <Dialog.Close asChild>
                <Button variant="ghost" aria-label={t('help.quick.close')} className="h-7 w-7 justify-center px-0">
                  <Icon name="x" size={14} />
                </Button>
              </Dialog.Close>
            </span>
          </header>
          <div className="flex min-h-0 flex-1 flex-col">{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
