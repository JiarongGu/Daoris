import { describe, expect, it } from 'vitest';
import { PERSON_WORDS } from '../bridge/generated/routes';
import { personWords, readPersonWords, unworded } from '../../scripts/person-words.mjs';

/**
 * The words of each person's press: the names check reads the generated `PERSON_WORDS` as JSON and requires each key in every
 * language, so a press marked a person's in the modules with no act's words fails the build.
 */
describe('the person\'s presses\' words', () => {
  it('reads the generated list as the page imports it', () => {
    expect(readPersonWords()).toEqual([...PERSON_WORDS]);
    expect(PERSON_WORDS).toContain('person.route.SESSION_INPUT');
  });

  it('reads a list over several lines, and none from a file that lists none', () => {
    const text = '// contracts: x\nexport const PERSON_WORDS: readonly string[] = [\n  "person.route.A",\n  "person.route.REMOTES.SET"\n];\n';
    expect(personWords(text)).toEqual(['person.route.A', 'person.route.REMOTES.SET']);
    expect(personWords('export type DriverRoute = \'A\';\n')).toBeNull();
  });

  it('finds each key a language leaves out or blank, and none it words', () => {
    const found = unworded(['person.route.A', 'person.route.B'], {
      en: { 'person.route.A': 'do a', 'person.route.B': 'do b' },
      zh: { 'person.route.A': '做 a', 'person.route.B': ' ' },
      fr: {},
    });
    expect(found.map((each) => `${each.language} ${each.key}`)).toEqual(['zh person.route.B', 'fr person.route.A', 'fr person.route.B']);
    expect(found.every((each) => each.rule === 'person')).toBe(true);
  });
});
