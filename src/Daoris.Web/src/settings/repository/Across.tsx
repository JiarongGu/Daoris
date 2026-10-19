// Reading across (READ1, D107) as the driver's ACROSS answer tells it: the shapes a repository's Setup and a workspace's page
// read. Its list left with Settings → Permissions (UX6g, D150 §3.1).

/** What said whether a checkout is read across (D107): set for its repository, for its workspace, or neither. */
export type ReadSource = 'repository' | 'workspace' | 'default';

/** One repository here, as the driver's ACROSS answer tells it. */
export type RepositoryAcross = {
  repository: string;
  workspace: string;
  /** Whether it has a checkout on this machine; one without is set by name all the same. */
  checkout: boolean;
  /** Whether agents outside it read its checkout, as the driver resolved it. */
  read: boolean;
  source: ReadSource;
  /** What its sessions were declared to write into. */
  writesTo: string[];
};

/** A change to reading: a repository's or a workspace's, cleared when it carries no `read`. */
export type ReadChange = { repository?: string; workspace?: string; read?: boolean };

/** A relationship declared, or taken back. */
export type WriteChange = { repository: string; to: string; allow: boolean };
