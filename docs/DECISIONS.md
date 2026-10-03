# Decisions

Numbered, dated, with the reasoning. A decision recorded here is not re-litigated without a reason to
reopen it — and a decision that was *considered and rejected* is recorded too, because without the reason
someone reverses it later and rediscovers the problem.

**Each decision is its own file, `docs/decisions/D<n>.md`**, the number unpadded, so the path is the
citation: D19 is `docs/decisions/D19.md` (D134). A citation of this page with a number, written while the
decisions were one file here, means that number's file. A file holds its entry, its `## D<n> — …` heading
first, and the notes that amend it, each appended at its end after a blank line, dated and headed by the
work that made it; an amendment written elsewhere leaves a pointer there instead.

- **D114 was never taken.** Every other number from D1 to the highest file is.
- **The next number is reserved at dispatch** (D106): the one after both the highest file in
  `docs/decisions/` and every number already reserved for a branch in flight. A new decision is a new file.
- **`grep '^## D' docs/decisions/*.md` lists them all**, one heading per file.

This page holds no decision and no note, and there is no index: the folder and a search are the index.
`tools/doc-duplicates.mjs` refuses a decision or a note written here, and checks each file for what a union
merge can leave in it.
