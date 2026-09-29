import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

const debounceMs = 250;

/** `input` follows the keystrokes; `q` is the trimmed, debounced query, mirrored to `?q=`. */
export function useLibrarySearch() {
  const [params, setParams] = useSearchParams();
  const [input, setInput] = useState(params.get('q') ?? '');
  const [q, setQ] = useState(input.trim());

  useEffect(() => {
    const timer = setTimeout(() => setQ(input.trim()), debounceMs);
    return () => clearTimeout(timer);
  }, [input]);

  useEffect(() => {
    setParams(q ? { q } : {}, { replace: true });
  }, [q, setParams]);

  return { input, setInput, q };
}
