import { failureMessage, type ImportFailure } from './failureMessage';

const cases: [ImportFailure, string][] = [
  ['InvalidUrl', 'Das ist keine gültige Webadresse.'],
  [
    'Unreachable',
    'Die Seite ist nicht erreichbar. Prüfe die Adresse und versuch es nochmal.',
  ],
  [
    'Blocked',
    'Diese Seite blockiert automatische Zugriffe und kann nicht importiert werden.',
  ],
  ['NotFound', 'Diese Seite existiert nicht.'],
  ['BadResponse', 'Die Seite hat etwas geliefert, das wir nicht lesen können.'],
  ['ForbiddenAddress', 'Diese Adresse ist nicht erlaubt.'],
  ['NoRecipe', 'Auf dieser Seite wurde kein Rezept gefunden.'],
];

it.each(cases)('maps %s to its German message', (kind, message) => {
  expect(failureMessage(kind)).toBe(message);
});
