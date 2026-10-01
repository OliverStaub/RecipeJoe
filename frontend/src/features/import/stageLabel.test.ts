import { stageLabel } from './stageLabel';

it.each([
  ['Fetching', 'Seite wird geladen…'],
  ['Extracting', 'Rezept wird gelesen…'],
  ['Saving', 'Wird gespeichert…'],
] as const)('maps %s to its German label', (stage, label) => {
  expect(stageLabel(stage)).toBe(label);
});
