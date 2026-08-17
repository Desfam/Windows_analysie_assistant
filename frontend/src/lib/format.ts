export function formatBytes(bytes: number | null | undefined): string {
  if (bytes == null || bytes <= 0) {
    return 'Nicht verfügbar'
  }

  const units = ['Bytes', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit += 1
  }

  const digits = value >= 100 || unit <= 1 ? 0 : 1
  return `${value.toFixed(digits)} ${units[unit]}`
}

export function formatPercent(value: number | null | undefined): string {
  return value == null ? 'Nicht verfügbar' : `${value.toFixed(value >= 10 ? 0 : 1)} %`
}

export function formatGhz(value: number | null | undefined): string {
  return value == null ? 'Nicht verfügbar' : `${value.toFixed(2)} GHz`
}

export function orNotAvailable(value: string | number | null | undefined): string {
  if (value == null || value === '') {
    return 'Nicht verfügbar'
  }
  return String(value)
}

const dateFormat = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric'
})

const timeFormat = new Intl.DateTimeFormat('de-DE', {
  hour: '2-digit',
  minute: '2-digit'
})

const dateTimeFormat = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit'
})

export function formatDate(value: string | null | undefined): string {
  if (!value) return 'Nicht verfügbar'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Nicht verfügbar' : dateFormat.format(date)
}

export function formatTime(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : `${timeFormat.format(date)} Uhr`
}

export function formatDateTime(value: string | Date | null | undefined): string {
  if (!value) return 'Nicht verfügbar'
  const date = typeof value === 'string' ? new Date(value) : value
  return Number.isNaN(date.getTime()) ? 'Nicht verfügbar' : dateTimeFormat.format(date)
}

export function formatRelativeDay(value: string | null | undefined): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const now = new Date()
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const diffDays = Math.round((startOfToday.getTime() - startOfDate.getTime()) / 86_400_000)

  if (diffDays === 0) return 'Heute'
  if (diffDays === 1) return 'Gestern'
  return formatDate(value)
}
