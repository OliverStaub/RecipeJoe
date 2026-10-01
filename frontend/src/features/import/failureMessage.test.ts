import { HttpError } from '@/api/client';
import {
  failureMessage,
  toImportFailure,
  type ImportFailure,
} from './failureMessage';

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
  [
    'SaveFailed',
    'Das Rezept konnte nicht gespeichert werden. Versuch es nochmal.',
  ],
];

it.each(cases)('maps %s to its German message', (kind, message) => {
  expect(failureMessage(kind)).toBe(message);
});

it('passes a known kind through', () => {
  expect(toImportFailure({ kind: 'Blocked' })).toBe('Blocked');
});

it('reads the kind from an HTTP error body', () => {
  expect(toImportFailure(new HttpError(422, { kind: 'NoRecipe' }))).toBe(
    'NoRecipe',
  );
});

it('maps a network failure to Unreachable', () => {
  expect(toImportFailure(new TypeError('Failed to fetch'))).toBe('Unreachable');
});

it.each([{ kind: 'Brandneu' }, { title: 'validation' }, undefined])(
  'maps an unrecognised error %j to BadResponse',
  (error) => {
    expect(toImportFailure(error)).toBe('BadResponse');
  },
);

it('maps null to BadResponse', () => {
  expect(toImportFailure(null)).toBe('BadResponse');
});
