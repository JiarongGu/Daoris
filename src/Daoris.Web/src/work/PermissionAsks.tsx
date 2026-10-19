/**
 * One option an agent offered on a permission request it holds for the person (AUTONOMY1b3a, D184 point 3), as `PERMISSION_WAITS`
 * answers it: the `optionId` the answer names, the agent's own label, and the protocol's kind. Only what the person may choose is
 * listed: this call's allow and a rejection, never a standing allow, which is a rule's (D74).
 */
export type PermissionChoice = { id: string; name: string; kind: string };

/**
 * A tool's permission request a driven session holds for the person under its repository's asking permission prompts (AUTONOMY1b3a,
 * D184 point 3), as `PERMISSION_WAITS` answers it: the session, the request's id on the agent's wire, the call it asks for and its
 * title and kind as the agent wrote them (null where it gave none), what the person may choose, and when it was held.
 */
export type PermissionWait = {
  session: string;
  request: string;
  call: string | null;
  title: string | null;
  kind: string | null;
  choices: PermissionChoice[];
  since: string;
};

/** The person's answer as the driver took it (`SESSION_PERMISSION_ANSWER`): whether it stands, and where not, its code and sentence. */
export type PermissionAnswered = { answered: boolean; why: string | null; message: string };
