import type { ReactNode } from 'react'

interface CardProps {
  children: ReactNode
  className?: string
}

export function Card({ children, className = '' }: CardProps) {
  return (
    <div
      className={`rounded-2xl border border-white/[0.06] bg-base-700/80 shadow-card backdrop-blur-sm ${className}`}
    >
      {children}
    </div>
  )
}
