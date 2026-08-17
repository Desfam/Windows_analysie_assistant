export type SeverityFilter = 'all' | 'critical' | 'high' | 'warning'

export type LogFilter = 'all' | 'System' | 'Application' | 'Microsoft-Windows-WindowsUpdateClient/Operational'

export type HoursFilter = 1 | 24 | 168

export interface EventFilterState {
  severity: SeverityFilter
  log: LogFilter
  hours: HoursFilter
  search: string
}

export const defaultFilters: EventFilterState = {
  severity: 'all',
  log: 'all',
  hours: 24,
  search: ''
}

export function severityToLevelParam(severity: SeverityFilter): string | undefined {
  switch (severity) {
    case 'critical':
      return 'critical'
    case 'high':
      return 'error'
    case 'warning':
      return 'warning'
    default:
      return undefined
  }
}
