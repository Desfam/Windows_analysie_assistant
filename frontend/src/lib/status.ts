import type { EventSeverity, HealthStatus } from '../types'

export interface StatusStyle {
  label: string
  dot: string
  text: string
  border: string
  badge: string
  bar: string
}

export const healthStyles: Record<HealthStatus, StatusStyle> = {
  Normal: {
    label: 'Normal',
    dot: 'bg-emerald-400',
    text: 'text-emerald-300',
    border: 'border-emerald-500/30',
    badge: 'bg-emerald-500/10 text-emerald-300 border border-emerald-500/20',
    bar: 'bg-emerald-400'
  },
  Warning: {
    label: 'Warnung',
    dot: 'bg-amber-400',
    text: 'text-amber-300',
    border: 'border-amber-500/30',
    badge: 'bg-amber-500/10 text-amber-300 border border-amber-500/20',
    bar: 'bg-amber-400'
  },
  Critical: {
    label: 'Kritisch',
    dot: 'bg-red-400',
    text: 'text-red-300',
    border: 'border-red-500/40',
    badge: 'bg-red-500/10 text-red-300 border border-red-500/20',
    bar: 'bg-red-400'
  },
  NotChecked: {
    label: 'Nicht geprüft',
    dot: 'bg-slate-500',
    text: 'text-slate-400',
    border: 'border-white/10',
    badge: 'bg-slate-500/10 text-slate-400 border border-white/10',
    bar: 'bg-slate-500'
  }
}

export interface SeverityStyle {
  label: string
  text: string
  border: string
  glow: string
  badge: string
  dot: string
  rank: number
}

export const severityStyles: Record<EventSeverity, SeverityStyle> = {
  Critical: {
    label: 'Kritisch',
    text: 'text-red-300',
    border: 'border-red-500/40',
    glow: 'shadow-[0_0_18px_rgba(239,68,68,0.25)]',
    badge: 'bg-red-500/10 text-red-300 border border-red-500/25',
    dot: 'bg-red-400',
    rank: 3
  },
  High: {
    label: 'Hoch',
    text: 'text-orange-300',
    border: 'border-orange-500/40',
    glow: 'shadow-[0_0_18px_rgba(249,115,22,0.2)]',
    badge: 'bg-orange-500/10 text-orange-300 border border-orange-500/25',
    dot: 'bg-orange-400',
    rank: 2
  },
  Warning: {
    label: 'Warnung',
    text: 'text-amber-300',
    border: 'border-amber-500/30',
    glow: '',
    badge: 'bg-amber-500/10 text-amber-300 border border-amber-500/20',
    dot: 'bg-amber-400',
    rank: 1
  }
}

export function progressColor(percent: number | null | undefined): string {
  if (percent == null) return 'bg-slate-500'
  if (percent >= 90) return 'bg-red-400'
  if (percent >= 75) return 'bg-orange-400'
  if (percent >= 60) return 'bg-amber-400'
  return 'bg-emerald-400'
}
