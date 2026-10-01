import { HttpError } from '@/api/client';
import {
  failureMessage,
  isRetryable,
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

it.each(cases)('maps a Web %s to its German message', (kind, message) => {
  expect(failureMessage(kind, 'Web')).toBe(message);
});

const videoCases: [ImportFailure, string][] = [
  ['Unreachable', 'YouTube ist nicht erreichbar. Versuch es später nochmal.'],
  [
    'Blocked',
    'YouTube blockiert gerade den Zugriff. Versuch es später nochmal.',
  ],
  [
    'NotFound',
    'Dieses Video ist nicht verfügbar (privat, gelöscht oder eingeschränkt).',
  ],
  [
    'NoCaptions',
    'Dieses Video hat keine Untertitel, daraus kann kein Rezept gelesen werden.',
  ],
  ['NoRecipe', 'In diesem Video wurde kein Rezept gefunden.'],
  ['LlmUnavailable', 'Der LLM-Provider ist nicht erreichbar.'],
  [
    'LlmBadOutput',
    'Die KI hat keine brauchbare Antwort geliefert. Versuch es nochmal.',
  ],
];

it.each(videoCases)(
  'maps a Video %s to its German message',
  (kind, message) => {
    expect(failureMessage(kind, 'Video')).toBe(message);
  },
);

it.each<ImportFailure>([
  'Unreachable',
  'Blocked',
  'BadResponse',
  'LlmUnavailable',
  'LlmBadOutput',
  'SaveFailed',
])('offers a retry for %s', (kind) => {
  expect(isRetryable(kind)).toBe(true);
});

it.each<ImportFailure>([
  'InvalidUrl',
  'NotFound',
  'NoCaptions',
  'ForbiddenAddress',
  'NoRecipe',
])('offers only dismiss for %s', (kind) => {
  expect(isRetryable(kind)).toBe(false);
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
