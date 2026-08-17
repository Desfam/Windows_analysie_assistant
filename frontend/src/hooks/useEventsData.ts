import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ApiError, api, type EventQueryParams } from '../services/api'
import type { EventsResponse } from '../types'

export function useEventsData(params: EventQueryParams, intervalSec: number) {
  const [data, setData] = useState<EventsResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [newKeys, setNewKeys] = useState<Set<string>>(new Set())

  const previousKeys = useRef<Set<string> | null>(null)
  const controllerRef = useRef<AbortController | null>(null)

  const paramsKey = JSON.stringify(params)
  // Neuen Filterlauf: Basis zurücksetzen, damit vorhandene Ereignisse nicht animiert werden.
  const stableParams = useMemo(() => params, [paramsKey]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    previousKeys.current = null
  }, [paramsKey])

  const load = useCallback(async () => {
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller

    setLoading(true)
    try {
      const result = await api.events(stableParams, controller.signal)
      if (controller.signal.aborted) return

      if (previousKeys.current) {
        const fresh = new Set<string>()
        for (const event of result.events) {
          const isImportant = event.severity === 'High' || event.severity === 'Critical'
          if (isImportant && !previousKeys.current.has(event.eventKey)) {
            fresh.add(event.eventKey)
          }
        }
        setNewKeys(fresh)
      } else {
        setNewKeys(new Set())
      }

      previousKeys.current = new Set(result.events.map((event) => event.eventKey))
      setData(result)
      setError(null)
      setLastUpdated(new Date())
    } catch (caught) {
      if ((caught as Error).name === 'AbortError') return
      setError(caught instanceof ApiError ? caught.message : 'Ereignisse konnten nicht geladen werden.')
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false)
      }
    }
  }, [stableParams])

  useEffect(() => {
    void load()
    const id = window.setInterval(() => void load(), intervalSec * 1000)
    return () => {
      window.clearInterval(id)
      controllerRef.current?.abort()
    }
  }, [load, intervalSec])

  return { data, loading, error, lastUpdated, newKeys, refresh: load }
}
