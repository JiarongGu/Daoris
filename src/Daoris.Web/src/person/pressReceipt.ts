/**
 * What a press that opened a place made (the driver's `PressReceipt`): a department's folder, line and set-up quest (`opened`), or a room's one
 * quest (`room`). The work a department is opened for is no quest yet: the service asks it once the set-up is done.
 */
export type PressReceiptNow = {
  kind: 'opened' | 'room';
  /** The department's name, or the repository the room is asked of. */
  repository: string;
  workspace: string | null;
  folder: string | null;
  line: string | null;
  /** A room's folder in its repository. */
  room: string | null;
  /** The quests the press asked, by id. */
  quests: string[];
  /** Whether every operation is done. */
  done: boolean;
};
