import {
  AlertTriangle,
  CheckCircle2,
  CircleDashed,
  CircleHelp,
  FlaskConical,
  GitBranch,
  Loader2,
  MessageCircleQuestion,
  Search,
  ShieldQuestion,
  Wrench,
  type LucideIcon
} from 'lucide-react'
import type { EvidenceLevel, ExecutionState, NodeKind, RiskLevel } from '../types'

export interface StateStyle {
  label: string
  text: string
  border: string
  bg: string
  dot: string
}

export const stateStyles: Record<ExecutionState, StateStyle> = {
  pending: {
    label: 'Wartet',
    text: 'text-slate-400',
    border: 'border-white/10',
    bg: 'bg-slate-500/10',
    dot: 'bg-slate-500'
  },
  ready: {
    label: 'Bereit',
    text: 'text-sky-300',
    border: 'border-sky-500/30',
    bg: 'bg-sky-500/10',
    dot: 'bg-sky-400'
  },
  running: {
    label: 'Wird ausgeführt',
    text: 'text-blue-300',
    border: 'border-blue-400/60',
    bg: 'bg-blue-500/15',
    dot: 'bg-blue-400'
  },
  completed: {
    label: 'Abgeschlossen',
    text: 'text-emerald-300',
    border: 'border-emerald-500/40',
    bg: 'bg-emerald-500/10',
    dot: 'bg-emerald-400'
  },
  failed: {
    label: 'Fehlgeschlagen',
    text: 'text-red-300',
    border: 'border-red-500/40',
    bg: 'bg-red-500/10',
    dot: 'bg-red-400'
  },
  skipped: {
    label: 'Übersprungen',
    text: 'text-slate-400',
    border: 'border-white/15 border-dashed',
    bg: 'bg-slate-500/5',
    dot: 'bg-slate-500'
  },
  cancelled: {
    label: 'Abgebrochen',
    text: 'text-amber-300',
    border: 'border-amber-500/30 border-dashed',
    bg: 'bg-amber-500/5',
    dot: 'bg-amber-400'
  },
  waitingForApproval: {
    label: 'Bestätigung nötig',
    text: 'text-orange-300',
    border: 'border-orange-500/50',
    bg: 'bg-orange-500/10',
    dot: 'bg-orange-400'
  },
  waitingForUser: {
    label: 'Wartet auf Eingabe',
    text: 'text-orange-300',
    border: 'border-orange-500/40',
    bg: 'bg-orange-500/10',
    dot: 'bg-orange-400'
  },
  blocked: {
    label: 'Blockiert',
    text: 'text-red-300',
    border: 'border-red-500/30',
    bg: 'bg-red-500/10',
    dot: 'bg-red-400'
  }
}

export interface RiskStyle {
  label: string
  description: string
  text: string
  bg: string
  border: string
}

export const riskStyles: Record<RiskLevel, RiskStyle> = {
  R0: {
    label: 'R0',
    description: 'Kein Risiko – nur Anzeige',
    text: 'text-emerald-300',
    bg: 'bg-emerald-500/10',
    border: 'border-emerald-500/20'
  },
  R1: {
    label: 'R1',
    description: 'Sehr geringes Risiko – nur Lesezugriff',
    text: 'text-emerald-300',
    bg: 'bg-emerald-500/10',
    border: 'border-emerald-500/20'
  },
  R2: {
    label: 'R2',
    description: 'Mittleres Risiko – Bestätigung empfohlen',
    text: 'text-orange-300',
    bg: 'bg-orange-500/10',
    border: 'border-orange-500/25'
  },
  R3: {
    label: 'R3',
    description: 'Hohes Risiko – ausdrückliche Bestätigung nötig',
    text: 'text-red-300',
    bg: 'bg-red-500/10',
    border: 'border-red-500/25'
  }
}

export interface KindStyle {
  label: string
  icon: LucideIcon
  accent: string
}

export const kindStyles: Record<NodeKind, KindStyle> = {
  problem: { label: 'Problem', icon: ShieldQuestion, accent: 'text-slate-300' },
  action: { label: 'Diagnoseaktion', icon: Search, accent: 'text-sky-300' },
  decision: { label: 'Entscheidung', icon: GitBranch, accent: 'text-violet-300' },
  evidence: { label: 'Beleg', icon: FlaskConical, accent: 'text-emerald-300' },
  repair: { label: 'Reparaturvorschlag', icon: Wrench, accent: 'text-orange-300' },
  verification: { label: 'Nachkontrolle', icon: CheckCircle2, accent: 'text-sky-300' },
  completion: { label: 'Abschluss', icon: CheckCircle2, accent: 'text-emerald-300' },
  userQuery: { label: 'Rückfrage', icon: MessageCircleQuestion, accent: 'text-orange-300' }
}

export const stateIcons: Record<ExecutionState, LucideIcon> = {
  pending: CircleDashed,
  ready: CircleDashed,
  running: Loader2,
  completed: CheckCircle2,
  failed: AlertTriangle,
  skipped: CircleDashed,
  cancelled: CircleDashed,
  waitingForApproval: ShieldQuestion,
  waitingForUser: MessageCircleQuestion,
  blocked: AlertTriangle
}

export interface EvidenceLevelStyle {
  label: string
  text: string
  bg: string
  border: string
  icon: LucideIcon
}

export const evidenceLevelStyles: Record<EvidenceLevel, EvidenceLevelStyle> = {
  strong: {
    label: 'Starke Hinweise',
    text: 'text-emerald-300',
    bg: 'bg-emerald-500/10',
    border: 'border-emerald-500/25',
    icon: CheckCircle2
  },
  some: {
    label: 'Einige Hinweise',
    text: 'text-amber-300',
    bg: 'bg-amber-500/10',
    border: 'border-amber-500/25',
    icon: AlertTriangle
  },
  unclear: {
    label: 'Bisher unklar',
    text: 'text-slate-400',
    bg: 'bg-slate-500/10',
    border: 'border-white/10',
    icon: CircleHelp
  },
  ruledOut: {
    label: 'Ausgeschlossen',
    text: 'text-slate-500',
    bg: 'bg-slate-500/5',
    border: 'border-white/10',
    icon: CircleDashed
  }
}

export function usePrefersReducedMotion(): boolean {
  if (typeof window === 'undefined') return false
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}
