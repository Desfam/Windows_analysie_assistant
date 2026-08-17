import { useCallback, useEffect, useState } from 'react'

export interface AppSettings {
  systemIntervalSec: number
  eventsIntervalSec: number
  reduceMotion: boolean
}

const STORAGE_KEY = 'wda.settings.v1'

const defaults: AppSettings = {
  systemIntervalSec: 30,
  eventsIntervalSec: 15,
  reduceMotion: false
}

function load(): AppSettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaults
    const parsed = JSON.parse(raw) as Partial<AppSettings>
    return {
      systemIntervalSec: clamp(parsed.systemIntervalSec, 5, 300, defaults.systemIntervalSec),
      eventsIntervalSec: clamp(parsed.eventsIntervalSec, 5, 300, defaults.eventsIntervalSec),
      reduceMotion: parsed.reduceMotion ?? defaults.reduceMotion
    }
  } catch {
    return defaults
  }
}

function clamp(value: number | undefined, min: number, max: number, fallback: number): number {
  if (value == null || Number.isNaN(value)) return fallback
  return Math.min(max, Math.max(min, value))
}

export function useSettings() {
  const [settings, setSettings] = useState<AppSettings>(load)

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
  }, [settings])

  const update = useCallback((partial: Partial<AppSettings>) => {
    setSettings((prev) => ({ ...prev, ...partial }))
  }, [])

  const prefersReducedMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  return {
    settings,
    update,
    animationsEnabled: !settings.reduceMotion && !prefersReducedMotion
  }
}
