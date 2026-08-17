export type HealthStatus = 'Normal' | 'Warning' | 'Critical' | 'NotChecked'

export type EventSeverity = 'Warning' | 'High' | 'Critical'

export interface SystemSummary {
  machineName: string | null
  manufacturer: string | null
  model: string | null
  systemType: string | null
  lastBootTime: string | null
  uptime: string | null
  currentUser: string | null
  status: HealthStatus
}

export interface CpuInfo {
  manufacturer: string | null
  model: string | null
  physicalCores: number | null
  logicalProcessors: number | null
  usagePercent: number | null
  maxClockSpeedGhz: number | null
  status: HealthStatus
}

export interface MemoryInfo {
  totalBytes: number | null
  usedBytes: number | null
  availableBytes: number | null
  usagePercent: number | null
  status: HealthStatus
}

export interface GpuInfo {
  name: string | null
  manufacturer: string | null
  driverVersion: string | null
  videoMemoryBytes: number | null
  status: HealthStatus
}

export interface DiskInfo {
  driveLetter: string | null
  fileSystem: string | null
  totalBytes: number | null
  usedBytes: number | null
  freeBytes: number | null
  usagePercent: number | null
  status: HealthStatus
}

export interface WindowsUpdateEntry {
  id: string | null
  installedOn: string | null
}

export interface WindowsInfo {
  edition: string | null
  version: string | null
  build: string | null
  installDate: string | null
  recentUpdates: WindowsUpdateEntry[]
  pendingUpdateCount: number | null
  status: HealthStatus
}

export interface EventItem {
  id: string
  eventKey: string
  eventId: number
  providerName: string | null
  logName: string | null
  level: string | null
  severity: EventSeverity
  timestamp: string
  title: string | null
  summary: string | null
  originalMessage: string | null
  machineName: string | null
  count: number
  firstSeen: string
  lastSeen: string
  occurrences: string[]
  rawXml: string | null
  isKnownEvent: boolean
}

export interface EventCounts {
  critical: number
  high: number
  warning: number
  total: number
}

export interface EventsResponse {
  events: EventItem[]
  counts: EventCounts
  warnings: string[]
  accessDenied: boolean
  generatedAt: string
}

export interface HealthResponse {
  status: string
  application: string
  version: string
  machineName: string
  serverTime: string
}
