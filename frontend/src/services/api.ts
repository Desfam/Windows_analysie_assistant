import type {
  CpuInfo,
  DiskInfo,
  EventsResponse,
  GpuInfo,
  HealthResponse,
  MemoryInfo,
  SystemSummary,
  WindowsInfo
} from '../types'

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message)
  }
}

function sessionToken(): string {
  const meta = document.querySelector('meta[name="x-session-token"]')
  return meta?.getAttribute('content') ?? ''
}

async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, {
      signal,
      headers: {
        'X-Session-Token': sessionToken()
      }
    })
  } catch (error) {
    if ((error as Error).name === 'AbortError') {
      throw error
    }
    throw new ApiError(0, 'Keine Verbindung zum lokalen Dienst.')
  }

  if (!response.ok) {
    throw new ApiError(response.status, `Anfrage fehlgeschlagen (${response.status}).`)
  }

  return (await response.json()) as T
}

export interface EventQueryParams {
  level?: string
  hours?: number
  log?: string
  search?: string
}

function buildEventsQuery(params: EventQueryParams): string {
  const query = new URLSearchParams()
  if (params.level) query.set('level', params.level)
  if (params.hours) query.set('hours', String(params.hours))
  if (params.log) query.set('log', params.log)
  if (params.search) query.set('search', params.search)
  const text = query.toString()
  return text ? `?${text}` : ''
}

export const api = {
  health: (signal?: AbortSignal) => request<HealthResponse>('/api/health', signal),
  summary: (signal?: AbortSignal) => request<SystemSummary>('/api/system/summary', signal),
  cpu: (signal?: AbortSignal) => request<CpuInfo>('/api/system/cpu', signal),
  memory: (signal?: AbortSignal) => request<MemoryInfo>('/api/system/memory', signal),
  gpus: (signal?: AbortSignal) => request<GpuInfo[]>('/api/system/gpus', signal),
  disks: (signal?: AbortSignal) => request<DiskInfo[]>('/api/system/disks', signal),
  windows: (signal?: AbortSignal) => request<WindowsInfo>('/api/system/windows', signal),
  events: (params: EventQueryParams, signal?: AbortSignal) =>
    request<EventsResponse>(`/api/events${buildEventsQuery(params)}`, signal)
}
