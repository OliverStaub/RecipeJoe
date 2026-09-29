import { act, renderHook, waitFor } from '@testing-library/react';
import { useWakeLock } from './useWakeLock';

function stubWakeLock() {
  const sentinels: (EventTarget & { release: ReturnType<typeof vi.fn> })[] = [];
  const request = vi.fn(async () => {
    const sentinel = Object.assign(new EventTarget(), {
      release: vi.fn(async () => {}),
    });
    sentinels.push(sentinel);
    return sentinel;
  });
  vi.stubGlobal('navigator', { wakeLock: { request } });
  return { request, sentinels };
}

function setVisibility(state: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', {
    value: state,
    configurable: true,
  });
  document.dispatchEvent(new Event('visibilitychange'));
}

afterEach(() => {
  vi.unstubAllGlobals();
  setVisibility('visible');
});

it('is unsupported without navigator.wakeLock', () => {
  vi.stubGlobal('navigator', {});
  const { result } = renderHook(() => useWakeLock());
  expect(result.current).toBe('unsupported');
});

it('acquires on mount and is active', async () => {
  const { request } = stubWakeLock();
  const { result } = renderHook(() => useWakeLock());
  await waitFor(() => expect(result.current).toBe('active'));
  expect(request).toHaveBeenCalledWith('screen');
});

it('is released when the request is rejected', async () => {
  vi.stubGlobal('navigator', {
    wakeLock: {
      request: vi.fn(async () => {
        throw new DOMException('denied', 'NotAllowedError');
      }),
    },
  });
  const { result } = renderHook(() => useWakeLock());
  await waitFor(() => expect(result.current).toBe('released'));
});

it('becomes released when the browser releases the lock, and re-acquires when visible again', async () => {
  const { request, sentinels } = stubWakeLock();
  const { result } = renderHook(() => useWakeLock());
  await waitFor(() => expect(result.current).toBe('active'));

  act(() => {
    sentinels[0].dispatchEvent(new Event('release'));
  });
  expect(result.current).toBe('released');

  await act(async () => {
    setVisibility('visible');
  });
  await waitFor(() => expect(result.current).toBe('active'));
  expect(request).toHaveBeenCalledTimes(2);
});

it('releases on unmount', async () => {
  const { sentinels } = stubWakeLock();
  const { result, unmount } = renderHook(() => useWakeLock());
  await waitFor(() => expect(result.current).toBe('active'));
  unmount();
  expect(sentinels[0].release).toHaveBeenCalled();
});

it('does not request while the page is hidden', async () => {
  const { request } = stubWakeLock();
  setVisibility('hidden');
  const { result } = renderHook(() => useWakeLock());
  expect(result.current).toBe('released');
  expect(request).not.toHaveBeenCalled();
});

it('does not request twice while a request is pending', async () => {
  let resolve!: (lock: unknown) => void;
  const request = vi.fn(() => new Promise((r) => (resolve = r)));
  vi.stubGlobal('navigator', { wakeLock: { request } });
  const { result } = renderHook(() => useWakeLock());
  await act(async () => {
    setVisibility('visible');
  });
  expect(request).toHaveBeenCalledTimes(1);
  await act(async () => {
    resolve(Object.assign(new EventTarget(), { release: async () => {} }));
  });
  expect(result.current).toBe('active');
});

it('releases a lock that arrives after unmount, without updating state', async () => {
  let resolve!: (lock: unknown) => void;
  const release = vi.fn(async () => {});
  vi.stubGlobal('navigator', {
    wakeLock: { request: vi.fn(() => new Promise((r) => (resolve = r))) },
  });
  const { result, unmount } = renderHook(() => useWakeLock());
  unmount();

  await act(async () => {
    resolve(Object.assign(new EventTarget(), { release }));
  });

  expect(release).toHaveBeenCalledTimes(1);
  expect(result.current).toBe('released');
});

it('does not request again when the page becomes hidden', async () => {
  const { request, sentinels } = stubWakeLock();
  const { result } = renderHook(() => useWakeLock());
  await waitFor(() => expect(result.current).toBe('active'));
  act(() => {
    sentinels[0].dispatchEvent(new Event('release'));
  });

  await act(async () => {
    setVisibility('hidden');
  });

  expect(request).toHaveBeenCalledTimes(1);
});

it('stays released when the request is rejected and the page is visible again', async () => {
  const request = vi.fn(async () => {
    throw new DOMException('denied', 'NotAllowedError');
  });
  vi.stubGlobal('navigator', { wakeLock: { request } });
  const { result } = renderHook(() => useWakeLock());
  await waitFor(() => expect(request).toHaveBeenCalledTimes(1));

  await act(async () => {
    setVisibility('visible');
  });

  expect(request).toHaveBeenCalledTimes(2);
  expect(result.current).toBe('released');
});
