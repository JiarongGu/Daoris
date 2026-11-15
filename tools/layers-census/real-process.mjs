/**
 * A source whose type carries `[Trait(Category.Name, Category.Process)]` is a real-process test (or the support one holds): its subject
 * is a real process, a real git or a real watcher, so the starts it makes and the real waits on them are not counted against it. What it
 * reads as "now" is still counted: that takes a `TimeProvider` like any other. The trait is the one the test projects' `Category` names.
 */
const TRAIT = /\[\s*Trait\(\s*Category\.Name\s*,\s*Category\.Process\s*\)\s*\]/;

/** Whether the source `text` holds a type marked as a real-process one. */
export function isRealProcess(text) {
  return TRAIT.test(text);
}
