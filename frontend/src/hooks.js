import { useState, useEffect, useCallback, useRef } from 'react'

export function useInterval(callback, delay) {
  const ref = useRef(callback)
  useEffect(() => { ref.current = callback })
  useEffect(() => {
    if (!delay) return
    const id = setInterval(() => ref.current(), delay)
    return () => clearInterval(id)
  }, [delay])
}

export function useFetch(url, intervalMs) {
  const [data,      setData]      = useState(null)
  const [loading,   setLoading]   = useState(true)
  const [error,     setError]     = useState(null)
  const [fetchedAt, setFetchedAt] = useState(null)

  const load = useCallback(async () => {
    try {
      const res = await fetch(url)
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      setData(await res.json())
      setError(null)
      setFetchedAt(new Date())
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, [url])

  // When the URL changes (e.g. user picked a different time range), clear stale
  // data immediately and show the loading skeleton rather than keeping old data.
  // This does NOT fire on auto-refresh because the URL stays the same.
  useEffect(() => {
    setData(null)
    setLoading(true)
  }, [url])

  useEffect(() => { load() }, [load])
  useInterval(load, intervalMs)
  return { data, loading, error, fetchedAt, refetch: load }
}

export function useSecondsAgo(date) {
  const [secs, setSecs] = useState(null)

  const tick = useCallback(() => {
    if (!date) return
    setSecs(Math.round((Date.now() - date.getTime()) / 1000))
  }, [date])

  useEffect(() => { tick() }, [tick])
  useInterval(tick, date ? 1000 : null)

  return secs
}
