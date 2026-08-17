import type { ReactNode } from 'react'

interface InfoRowProps {
  label: string
  value: ReactNode
}

export function InfoRow({ label, value }: InfoRowProps) {
  const isMissing = value === 'Nicht verfügbar'
  return (
    <div className="flex items-start justify-between gap-3 py-1 text-sm">
      <span className="shrink-0 text-slate-400">{label}</span>
      <span className={`text-right ${isMissing ? 'text-slate-500 italic' : 'text-slate-100'}`}>
        {value}
      </span>
    </div>
  )
}
