import type { HandedSection, InstructionAccount } from './conversation';

// What a session was handed, as the driver's account keeps it (CONTEXT1): the shapes its goldens hold
// (`golden/target-prompt/*.account.json`), shared by the molecule's stories and its tests.

const own = (name: string, chars: number, said: string): HandedSection =>
  ({ name, source: 'driver', said: `${said}: ${chars.toLocaleString('en')} characters, the driver's own words`, chars });

/** The driver's own sections, as every quest's instruction ends. */
const TAIL: HandedSection[] = [
  own('look', 623, 'where to look before asking'),
  {
    name: 'indexes', source: 'tree', said: "the repository's indexes: 213 characters, naming 3 of the 3 its tree keeps",
    chars: 213, shown: 3, of: 3,
  },
  own('attributed', 481, "the person's words met second-hand"),
  own('asking', 1081, 'asking another repository, and stopping for the person, a go-ahead asked once on the ask'),
  own('closing', 655, "the closing note's two lists, each item naming its source"),
  own('proposing', 298, 'proposing a rule'),
  own('boundary', 302, 'the boundary'),
];

/** The permission rules handed beside the instruction, counted by list. */
const RULES: HandedSection = {
  name: 'rules', source: 'permissions', shown: 18,
  said: "the permission rules: 18 handed beside it (14 allow, 0 ask, 4 deny), composed from this machine's rules for workspace `default` and repository `reports`; 1 hard denial and the tree guard",
};

/** A claim on an ask, with everything the look and the close speak to (the `full` golden), and the rules beside it. */
export const CLAIMED: InstructionAccount = {
  chars: 6786,
  sections: [
    { name: 'quest', source: 'quest', from: 'abc123', chars: 238, said: 'the quest: 238 characters, quest #abc123 asked by `ask #a1b2c3`, as the service answered it' },
    { name: 'carried', source: 'quest', from: 'abc123', chars: 99, said: 'what the quest carries: 99 characters from quest #abc123: 1 link' },
    { name: 'requirements', source: 'quest', from: 'abc123', chars: 777, shown: 1, of: 1, said: 'the requirements: 777 characters, 1 of 1 from quest #abc123' },
    { name: 'words', source: 'ask', from: 'a1b2c3', chars: 504, shown: 3, of: 3, said: "the person's words: 504 characters, 3 of 3 from ask #a1b2c3" },
    { name: 'go-aheads', source: 'ask', from: 'a1b2c3', chars: 554, shown: 2, of: 2, said: 'the go-aheads: 554 characters, 2 of 2 from ask #a1b2c3' },
    {
      name: 'standing', source: 'repository', from: 'reports', at: '2026-09-30T02:26:00+00:00', chars: 347,
      said: "the standing answer: 347 characters, the person's for `reports` on this machine, set 2026-09-30 02:26 UTC",
    },
    own('close', 340, 'how to take it and close it'),
    { name: 'reading', source: 'checkouts', shown: 1, chars: 274, said: 'the checkouts it may read: 274 characters, 1 checkout the person declared it may read and not change' },
    ...TAIL,
    RULES,
    { name: 'language', source: 'repository', chars: 0, none: 'not-set', said: 'the session language: none set for its repository or its workspace on this machine' },
    { name: 'map', source: 'tree', chars: 0, none: 'empty', said: 'the code map: none, since its tree keeps none' },
  ],
};

/** A repository's quest that carries nothing (the `bare` golden), on an agent that takes no rules from Daoris. */
export const BARE: InstructionAccount = {
  chars: 3710,
  sections: [
    { name: 'quest', source: 'quest', from: 'abc123', chars: 200, said: 'the quest: 200 characters, quest #abc123 asked by `console-api`, as the service answered it' },
    own('close', 340, 'how to take it and close it'),
    own('look', 585, 'where to look before asking'),
    own('attributed', 481, "the person's words met second-hand"),
    own('asking', 849, 'asking another repository, and stopping for the person'),
    own('closing', 655, "the closing note's two lists, each item naming its source"),
    own('proposing', 298, 'proposing a rule'),
    own('boundary', 302, 'the boundary'),
    { name: 'requirements', source: 'quest', from: 'abc123', chars: 0, none: 'empty', said: 'the requirements: none, since the quest names none' },
    { name: 'words', source: 'ask', chars: 0, none: 'no-ask', said: "the person's words: none, since no ask asked this quest" },
    { name: 'go-aheads', source: 'ask', chars: 0, none: 'no-ask', said: 'the go-aheads: none, since no ask asked this quest' },
    { name: 'standing', source: 'repository', from: 'console-ui', chars: 0, none: 'not-set', said: 'the standing answer: none set for `console-ui` on this machine' },
    { name: 'language', source: 'repository', chars: 0, none: 'not-set', said: 'the session language: none set for its repository or its workspace on this machine' },
    { name: 'map', source: 'tree', chars: 0, none: 'empty', said: 'the code map: none, since its tree keeps none' },
    { name: 'indexes', source: 'tree', chars: 0, none: 'empty', said: "the repository's indexes: none, since its tree names none" },
    { name: 'rules', source: 'permissions', shown: 0, none: 'agent', said: 'the permission rules: none handed, since its agent takes no rules from Daoris' },
  ],
};

/** The instruction's length as the record counts it: its handed sections' characters, which add up to it by construction. */
const counted = (sections: HandedSection[]): InstructionAccount =>
  ({ chars: sections.reduce((sum, section) => sum + (section.none ? 0 : section.chars ?? 0), 0), sections });

/** A carry-on whose every bound left something out, with a session language set for its workspace. */
export const CUT: InstructionAccount = counted([
    { name: 'quest', source: 'quest', from: 'abc123', chars: 326, said: 'the quest: 326 characters, quest #abc123 asked by `ask #a1b2c3`, as the service answered it' },
    {
      name: 'carried', source: 'quest', from: 'abc123', chars: 412, said: 'what the quest carries: 412 characters from quest #abc123: 1 link and 2 files',
      cuts: [{ code: 'files-elsewhere', count: 1, said: '1 file not on this machine, named without a path' }],
    },
    {
      name: 'requirements', source: 'quest', from: 'abc123', chars: 7980, shown: 3, of: 5, said: 'the requirements: 7,980 characters, 3 of 5 from quest #abc123',
      cuts: [{ code: 'requirements-bound', count: 2, limit: 8000, said: 'requirements 4 to 5 left out past its bound of 8,000 characters; `quest_list` shows each whole' }],
    },
    {
      name: 'words', source: 'ask', from: 'a1b2c3', chars: 8410, shown: 5, of: 8, said: "the person's words: 8,410 characters, 5 of 8 from ask #a1b2c3",
      cuts: [
        {
          code: 'words-older', count: 3, limit: 8000, from: '2026-10-01T03:26:00+00:00', to: '2026-10-01T05:26:00+00:00',
          said: "3 older words, said from 2026-10-01 03:26 UTC to 2026-10-01 05:26 UTC, left out past its bound of 8,000 characters; the ask's record keeps every one",
        },
        { code: 'words-long', count: 1, limit: 2000, said: '1 word cut at 2,000 characters' },
        { code: 'words-not-kept', from: '2026-10-01T02:21:00+00:00', said: 'words said before 2026-10-01 02:21 UTC were not kept on the ask' },
      ],
    },
    {
      name: 'go-aheads', source: 'ask', from: 'a1b2c3', chars: 7920, shown: 14, of: 30, said: 'the go-aheads: 7,920 characters, 14 of 30 from ask #a1b2c3',
      cuts: [
        { code: 'go-aheads-bound', count: 16, limit: 8000, said: "go-aheads 15 to 30 left out past its bound of 8,000 characters; the ask's page shows each" },
        { code: 'acts-long', count: 1, limit: 300, said: '1 act cut at 300 characters' },
      ],
    },
    {
      name: 'standing', source: 'repository', from: 'reports', at: '2026-09-30T02:26:00+00:00', chars: 2310,
      said: "the standing answer: 2,310 characters, the person's for `reports` on this machine, set 2026-09-30 02:26 UTC",
      cuts: [{ code: 'standing-long', count: 345, limit: 2000, said: '345 characters of it left out past its bound of 2,000' }],
    },
    {
      name: 'carry-on', source: 'record', chars: 2240, shown: 50, of: 57,
      said: "what the session before left: 2,240 characters: its record's end, the person's answer, 1 uncommitted change, its plan of 57 steps and its last words",
      cuts: [{ code: 'plan-bound', count: 7, limit: 50, said: '7 plan steps left out past its bound of 50' }],
    },
    own('close', 366, 'how to carry it on and close it'),
    { name: 'language', source: 'workspace', from: 'zh', chars: 236, said: 'the session language: 236 characters, `zh`, set for its workspace on this machine' },
    { name: 'map', source: 'tree', from: 'docs/code-map.json', chars: 534, said: 'the code map: 534 characters, naming `docs/code-map.json` in its tree' },
    {
      name: 'indexes', source: 'tree', chars: 402, shown: 8, of: 11, said: "the repository's indexes: 402 characters, naming 8 of the 11 its tree keeps",
      cuts: [{ code: 'indexes-bound', count: 3, limit: 8, said: '3 more counted, not named, past the 8 it names' }],
    },
    ...TAIL.filter((section) => section.name !== 'indexes'),
    {
      ...RULES, shown: 18,
      cuts: [{ code: 'rules-unread', said: "this machine's rules file could not be read, so only the defaults were handed" }],
    },
]);

/** The person's words could not be read for this start: their sentence handed, and said unread. */
export const UNREAD: InstructionAccount = {
  chars: 4120,
  sections: [
    { name: 'quest', source: 'quest', from: 'abc123', chars: 236, said: 'the quest: 236 characters, quest #abc123 asked by `ask #a1`, as the service answered it' },
    {
      name: 'words', source: 'ask', from: 'a1', chars: 122, said: "the person's words: 122 characters from ask #a1, saying they could not be read",
      cuts: [{ code: 'words-unread', said: 'they could not be read for this start, so none after the ask itself were handed' }],
    },
    ...BARE.sections.slice(1, 8),
    { name: 'go-aheads', source: 'ask', chars: 0, none: 'not-answered', said: 'the go-aheads: none, since the service answered none for ask #a1' },
  ],
};

/** A newer driver's account: a section, a cut and an absence this page has no words for, beside ones it has. */
export const NEWER: InstructionAccount = {
  chars: 4500,
  sections: [
    { name: 'quest', source: 'quest', from: 'abc123', chars: 200, said: 'the quest: 200 characters, quest #abc123 asked by `console-api`, as the service answered it' },
    { name: 'recall', source: 'knowledge', chars: 790, said: "what the workspace's knowledge recalled: 790 characters, 4 entries by meaning" },
    {
      name: 'words', source: 'ask', from: 'a1', chars: 300, shown: 2, of: 2, said: "the person's words: 300 characters, 2 of 2 from ask #a1",
      cuts: [{ code: 'words-budget', count: 1, said: '1 word left out by the session budget' }],
    },
    { name: 'standing', source: 'repository', chars: 0, none: 'held', said: 'the standing answer: held for the person' },
    { name: 'map', source: 'tree', chars: 0, none: 'empty', said: 'the code map: none, since its tree keeps none' },
  ],
};
