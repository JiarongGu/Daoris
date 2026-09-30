/** A key the frame answers wherever focus is: a field, a message box, or the terminal. */
export type FrameShortcut = 'quickAsk' | 'help' | 'rail' | 'panel' | 'right';

type Pressed = Pick<KeyboardEvent, 'key' | 'code' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>;

/**
 * Which of the frame's keys a press is, or null: VS Code's for the same things, so a hand that knows one
 * knows the other. Quick Ask on `Ctrl+Shift+Alt+L` (DOCK1d), by the key's place, since what `key` says
 * under Shift and Alt differs by layout; Ask Daoris on `F1` and `Ctrl+Alt+I` (HELP1); the regions on
 * `Ctrl+B`, `Ctrl+J` and `Ctrl+Alt+B` (DOCK1c).
 *
 * @remarks
 * One list, read by the application's key handler and by the terminal's renderer (CONSOLE4b), which
 * leaves these to the frame as VS Code's terminal leaves its commands to the workbench: a renderer that
 * took them would send the shell a ^B and the rail would never close.
 */
export function frameShortcut(event: Pressed): FrameShortcut | null {
  if (event.ctrlKey && event.shiftKey && event.altKey && event.code === 'KeyL') return 'quickAsk';
  const letter = event.key.toLowerCase();
  if (event.key === 'F1' || (letter === 'i' && event.ctrlKey && event.altKey)) return 'help';
  if (event.ctrlKey && !event.shiftKey && !event.metaKey && (letter === 'b' || (letter === 'j' && !event.altKey))) {
    return letter === 'j' ? 'panel' : event.altKey ? 'right' : 'rail';
  }
  return null;
}
