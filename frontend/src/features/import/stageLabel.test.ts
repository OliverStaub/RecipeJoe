import { stageLabel } from './stageLabel';

it.each([
  ['Web', 'Fetching', 'Seite wird geladen…'],
  ['Web', 'Extracting', 'Rezept wird gelesen…'],
  ['Web', 'Saving', 'Wird gespeichert…'],
  ['Video', 'Fetching', 'Video wird geladen…'],
  ['Video', 'Extracting', 'Rezept wird geschrieben…'],
  ['Video', 'Saving', 'Wird gespeichert…'],
] as const)(
  'maps a %s Import at %s to its German label',
  (kind, stage, label) => {
    expect(stageLabel(stage, kind)).toBe(label);
  },
);
