import { motion } from 'framer-motion'
import { progressColor } from '../../lib/status'

interface ProgressBarProps {
  percent: number | null | undefined
  animate?: boolean
  color?: string
}

export function ProgressBar({ percent, animate = true, color }: ProgressBarProps) {
  const value = percent == null ? 0 : Math.min(100, Math.max(0, percent))
  const barColor = color ?? progressColor(percent)

  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/[0.06]">
      <motion.div
        className={`h-full rounded-full ${barColor}`}
        initial={animate ? { width: 0 } : false}
        animate={{ width: `${value}%` }}
        transition={{ duration: animate ? 0.6 : 0, ease: 'easeOut' }}
      />
    </div>
  )
}
