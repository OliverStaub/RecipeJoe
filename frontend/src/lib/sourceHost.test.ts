import { sourceHost } from './sourceHost';

it('returns the hostname of a URL', () => {
  expect(sourceHost('https://www.example.com/a/b?c=1')).toBe('www.example.com');
});

it('returns the input unchanged when it is not a URL', () => {
  expect(sourceHost('not a url')).toBe('not a url');
});
