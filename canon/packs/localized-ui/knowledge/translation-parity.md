---
name: translation-parity
applies_when: adding or changing a user-facing string, a translation catalogue, or a message a person reads
enforces: every catalogue holds the same keys in both directions; keys are structural, never the default language's text; chrome translates and content does not; a placeholder is part of the contract
---

# A missing translation works perfectly, in the wrong language

**Every language catalogue holds the same key set, checked both ways by something that fails the
build.** A translation that is simply absent does not error, does not warn, and does not look broken
to whoever wrote it — it renders the default language and waits for a reader who cannot read it.

## Why

This is the shape of defect worth the most doctrine: **it succeeds wrongly.** A crash teaches the same
lesson for free; a missing string is found by a person who is not the author, in a language the author
does not check, weeks later, and often is not reported at all because it looks like the product simply
being partly untranslated.

Two repositories in this family derived the same gate independently, from opposite ends — one shipping
a bilingual interface to readers, one adding a second language to a console — and neither found its
gaps by looking. Both found them by counting keys.

The second half is subtler and costs more to undo: **a catalogue that translates the wrong things.**
Once a message that is really a contract has been routed through translation, every later edit has to
be made twice and the two copies drift — and the drift is invisible in exactly the language nobody on
the team reads.

## How to apply

### Parity is a gate, and it runs in both directions

- **Compare key sets, not counts.** Sizes can match while the contents differ.
- **Both directions.** A key in the default language and missing elsewhere is an untranslated string.
  A key present only in a *translation* is a key somebody deleted from the default and left behind —
  dead weight, and the signal that the catalogues have been edited independently rather than together.
- **Fail the build.** Reported-and-continued is the state this already was in: everyone could see it
  and nobody acted, because the default language kept working.

### Keys are structural — never the default language's text

Use a key that names the *place*, not the sentence. Where the key **is** the English sentence, a
missing translation renders flawless English and there is nothing to detect: the fallback is
indistinguishable from a correct result, and the parity check above cannot exist, because every
catalogue trivially has the key it was named after.

The default language is a catalogue like any other. If it is special in the code, it will be special
in the mistakes.

### Chrome is translated; content and contracts are not

Three kinds of text reach a person, and only one of them belongs in a catalogue.

| Kind | Translated? | Why |
|---|---|---|
| Interface chrome — labels, headings, buttons, empty states | **Yes** | It is the product speaking, and it is finite |
| Data the system stores and renders back | **No** | It is the author's words; re-saying them is a different claim |
| A message whose exact wording *is* the contract — a refusal naming what to do next | **No, verbatim** | The sentence is the answer; a paraphrase is a different answer |

Getting the third row wrong is the expensive one. A refusal that names what holds something and what
to run is an instruction, and a translated instruction is a second instruction maintained by whoever
translated it. Where such a message must still reach a reader in their own language, pass the original
**through** the catalogue as a value rather than re-authoring it — one entry that is nothing but the
placeholder, so there is one copy of the sentence and it is the one the system produced.

### A placeholder is part of the contract, and needs its own check

A translation that drops an interpolation does not fail: it renders the fixed part and silently
discards the value. Where one entry exists to carry a message through, **every** occurrence of that
message becomes the same sentence — which reads as a system that has one error rather than a system
whose translations are broken. Assert the placeholder survives, per entry that has one.

### Parity proves the keys exist, not that the interface survives them

Key sets agreeing says nothing about a language whose strings are three times longer, or whose script
the layout was never tried with. Keep at least one end-to-end check that renders a non-default
language and looks at the result. It is the cheapest test in the suite and it is the only one that has
ever caught a layout.

Locale is more than the catalogue: dates, numbers and sort order are formatted for the active language
too, and a hand-built date string is a translation nobody registered.

### The catalogue is not the only place a language lives

Non-ASCII text that renders correctly can still be destroyed after it leaves the interface — through a
console, a redirected stream, a file written by a tool that assumed a byte is a character. That is a
different failure with a different home; the point here is only that a green parity check is not
evidence the text survives the trip. Check the bytes where they land, not just the screen.
