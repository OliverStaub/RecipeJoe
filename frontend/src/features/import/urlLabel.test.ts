import { urlLabel } from './urlLabel';

it('drops a leading www.', () => {
  expect(urlLabel('https://www.example.com/')).toBe('example.com');
});

it('keeps the path, without a trailing slash root', () => {
  expect(urlLabel('https://example.com/recipes/suppe')).toBe(
    'example.com/recipes/suppe',
  );
});

it('falls back to the raw string for an unparseable URL', () => {
  expect(urlLabel('not a url')).toBe('not a url');
});
