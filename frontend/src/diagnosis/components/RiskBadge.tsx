import { ShieldCheck } from 'lucide-react'
import type { RiskLevel } from '../types'
import { riskStyles } from '../lib/styles'

interface RiskBadgeProps {
  risk: RiskLevel
  showIcon?: boolean
}

export function RiskBadge({ risk, showIcon = false }: RiskBadgeProps) {
  const style = riskStyles[risk]

  return (
    <span
      title={style.description}
      className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-semibold ${style.text} ${style.bg} ${style.border}`}
    >
      {showIcon && <ShieldCheck className="h-3.5 w-3.5" aria-hidden />}
      {style.label}
    </span>
  )
}
