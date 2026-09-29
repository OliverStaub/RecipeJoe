import { useEffect, useState } from 'react';

export type WakeLockStatus = 'active' | 'released' | 'unsupported';

const supported = () =>
  typeof navigator !== 'undefined' && 'wakeLock' in navigator;

export function useWakeLock(): WakeLockStatus {
  const [status, setStatus] = useState<WakeLockStatus>(
    supported() ? 'released' : 'unsupported',
  );

  useEffect(() => {
    if (!supported()) return;
    let sentinel: WakeLockSentinel | null = null;
    let cancelled = false;
    let requesting = false;

    async function acquire() {
      if (document.visibilityState !== 'visible' || sentinel || requesting)
        return;
      requesting = true;
      try {
        const lock = await navigator.wakeLock.request('screen');
        if (cancelled) {
          void lock.release();
          return;
        }
        sentinel = lock;
        lock.addEventListener('release', () => {
          if (sentinel === lock) sentinel = null;
          if (!cancelled) setStatus('released');
        });
        setStatus('active');
      } catch {
        if (!cancelled) setStatus('released');
      } finally {
        requesting = false;
      }
    }

    function onVisibilityChange() {
      if (document.visibilityState === 'visible') void acquire();
    }

    void acquire();
    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => {
      cancelled = true;
      document.removeEventListener('visibilitychange', onVisibilityChange);
      void sentinel?.release();
    };
  }, []);

  return status;
}
