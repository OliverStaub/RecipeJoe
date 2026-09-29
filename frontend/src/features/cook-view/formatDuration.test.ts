import { formatDuration } from './formatDuration';

it.each([
  [20, '20 Min.'],
  [60, '1 Std.'],
  [75, '1 Std. 15 Min.'],
  [135, '2 Std. 15 Min.'],
  [1, '1 Min.'],
  [0, '0 Min.'],
])('formats %i minutes as %s', (minutes, text) => {
  expect(formatDuration(minutes)).toBe(text);
});
