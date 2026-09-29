import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

const debounceMs = 250;

/**
 * `input` follows the keystrokes; `q` is the trimmed, debounced query.
 * `?q=` and `q` follow each other: typing writes the URL, navigation rewrites the input.
 */
export function useLibrarySearch() {
  const [params, setParams] = useSearchParams();
  const urlQ = params.get('q') ?? '';
  const [input, setInput] = useState(urlQ);
  const [q, setQ] = useState(urlQ.trim());
  // What the URL held after our last read or write, to tell our own writes from navigation.
  const synced = useRef(urlQ);

  useEffect(() => {
    const timer = setTimeout(() => setQ(input.trim()), debounceMs);
    return () => clearTimeout(timer);
  }, [input]);

  useEffect(() => {
    if (q !== synced.current) {
      synced.current = q;
      setParams(q ? { q } : {}, { replace: true });
    }
  }, [q, setParams]);

  useEffect(() => {
    if (urlQ !== synced.current) {
      synced.current = urlQ;
      setInput(urlQ);
      setQ(urlQ.trim());
    }
  }, [urlQ]);

  return { input, setInput, q };
}
