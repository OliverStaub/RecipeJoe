import { toImportFailure } from './api';

it('passes a known kind through', () => {
  expect(toImportFailure({ kind: 'Blocked' })).toBe('Blocked');
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
