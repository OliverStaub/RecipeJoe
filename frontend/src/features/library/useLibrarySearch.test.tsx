import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { useLibrarySearch } from './useLibrarySearch';

function setup(initial = '/') {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={[initial]}>{children}</MemoryRouter>
  );
  return renderHook(
    () => ({
      search: useLibrarySearch(),
      location: useLocation(),
      navigate: useNavigate(),
    }),
    { wrapper },
  );
}

beforeEach(() => {
  vi.useFakeTimers();
});
afterEach(() => {
  vi.useRealTimers();
});

it('seeds input and query from ?q=', () => {
  const { result } = setup('/?q=suppe');

  expect(result.current.search.input).toBe('suppe');
  expect(result.current.search.q).toBe('suppe');
});

it('debounces the query by 250 ms while the input updates at once', () => {
  const { result } = setup();

  act(() => result.current.search.setInput('su'));
  act(() => vi.advanceTimersByTime(249));
  expect(result.current.search.input).toBe('su');
  expect(result.current.search.q).toBe('');

  act(() => vi.advanceTimersByTime(1));
  expect(result.current.search.q).toBe('su');
});

it('restarts the debounce on every keystroke', () => {
  const { result } = setup();

  act(() => result.current.search.setInput('su'));
  act(() => vi.advanceTimersByTime(200));
  act(() => result.current.search.setInput('sup'));
  act(() => vi.advanceTimersByTime(200));
  expect(result.current.search.q).toBe('');

  act(() => vi.advanceTimersByTime(50));
  expect(result.current.search.q).toBe('sup');
});

it('trims the query', () => {
  const { result } = setup();

  act(() => result.current.search.setInput('  suppe '));
  act(() => vi.advanceTimersByTime(250));

  expect(result.current.search.q).toBe('suppe');
  expect(result.current.location.search).toBe('?q=suppe');
});

it('mirrors the query to ?q= by replacing, and drops it when empty', () => {
  const { result } = setup('/?q=suppe');

  act(() => result.current.search.setInput('kuchen'));
  act(() => vi.advanceTimersByTime(250));
  expect(result.current.location.search).toBe('?q=kuchen');

  act(() => result.current.search.setInput(''));
  act(() => vi.advanceTimersByTime(250));
  expect(result.current.location.search).toBe('');
});

it('follows ?q= changes that come from outside the search box', () => {
  const { result } = setup('/?q=suppe');

  act(() => result.current.navigate('/?q=kuchen'));
  expect(result.current.search.input).toBe('kuchen');
  expect(result.current.search.q).toBe('kuchen');

  act(() => result.current.navigate('/'));
  expect(result.current.search.input).toBe('');
  expect(result.current.search.q).toBe('');
  expect(result.current.location.search).toBe('');
});

it('does not reset the input while a typed query is still being mirrored', () => {
  const { result } = setup('/?q=suppe');

  act(() => result.current.search.setInput('kuchen'));
  act(() => vi.advanceTimersByTime(250));

  expect(result.current.search.input).toBe('kuchen');
  expect(result.current.location.search).toBe('?q=kuchen');
});
