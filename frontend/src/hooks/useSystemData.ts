import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../services/api'
import type { CpuInfo, DiskInfo, GpuInfo, MemoryInfo, SystemSummary, WindowsInfo } from '../types'

export interface Section<T> {
  data: T | null
  failed: boolean
}

export interface SystemData {
  summary: Section<SystemSummary>
  cpu: Section<CpuInfo>
  memory: Section<MemoryInfo>
  gpus: Section<GpuInfo[]>
  disks: Section<DiskInfo[]>
  windows: Section<WindowsInfo>
}

const emptySection = <T,>(): Section<T> => ({ data: null, failed: false })

const emptyData: SystemData = {
  summary: emptySection(),
  cpu: emptySection(),
  memory: emptySection(),
  gpus: emptySection(),
  disks: emptySection(),
  windows: emptySection()
}

function toSection<T>(result: PromiseSettledResult<T>): Section<T> {
  return result.status === 'fulfilled'
    ? { data: result.value, failed: false }
    : { data: null, failed: true }
}

export function useSystemData(intervalSec: number) {
  const [data, setData] = useState<SystemData>(emptyData)
  const [loading, setLoading] = useState(true)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [hasError, setHasError] = useState(false)
  const controllerRef = useRef<AbortController | null>(null)

  const load = useCallback(async () => {
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller
    const { signal } = controller

    setLoading(true)
    const [summary, cpu, memory, gpus, disks, windows] = await Promise.allSettled([
      api.summary(signal),
      api.cpu(signal),
      api.memory(signal),
      api.gpus(signal),
      api.disks(signal),
      api.windows(signal)
    ])

    if (signal.aborted) return

    const next: SystemData = {
      summary: toSection(summary),
      cpu: toSection(cpu),
      memory: toSection(memory),
      gpus: toSection(gpus),
      disks: toSection(disks),
      windows: toSection(windows)
    }

    setData(next)
    setHasError(Object.values(next).every((section) => section.failed))
    setLastUpdated(new Date())
    setLoading(false)
  }, [])

  useEffect(() => {
    void load()
    const id = window.setInterval(() => void load(), intervalSec * 1000)
    return () => {
      window.clearInterval(id)
      controllerRef.current?.abort()
    }
  }, [load, intervalSec])

  return { data, loading, lastUpdated, hasError, refresh: load }
}
