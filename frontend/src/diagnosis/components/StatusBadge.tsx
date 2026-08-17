import type { ExecutionState } from '../types'
import { stateIcons, stateStyles } from '../lib/styles'

interface StatusBadgeProps {
  state: ExecutionState
  size?: 'sm' | 'md'
}

export function StatusBadge({ state, size = 'sm' }: StatusBadgeProps) {
  const style = stateStyles[state]
  const Icon = stateIcons[state]
  const padding = size === 'sm' ? 'px-2 py-0.5 text-[11px]' : 'px-2.5 py-1 text-xs'

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-md border font-medium ${padding} ${style.text} ${style.bg} ${style.border}`}
    >
      <Icon className={`h-3.5 w-3.5 ${state === 'running' ? 'animate-spin' : ''}`} aria-hidden />
      {style.label}
    </span>
  )
}
