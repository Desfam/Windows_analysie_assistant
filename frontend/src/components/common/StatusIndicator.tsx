import { AlertTriangle, CircleCheck, CircleHelp, OctagonAlert } from 'lucide-react'
import type { HealthStatus } from '../../types'
import { healthStyles } from '../../lib/status'

interface StatusIndicatorProps {
  status: HealthStatus
  showLabel?: boolean
}

const icons = {
  Normal: CircleCheck,
  Warning: AlertTriangle,
  Critical: OctagonAlert,
  NotChecked: CircleHelp
}

export function StatusIndicator({ status, showLabel = false }: StatusIndicatorProps) {
  const style = healthStyles[status]
  const Icon = icons[status]

  return (
    <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${style.text}`}>
      <Icon className="h-4 w-4" aria-hidden />
      {showLabel && <span>{style.label}</span>}
    </span>
  )
}
