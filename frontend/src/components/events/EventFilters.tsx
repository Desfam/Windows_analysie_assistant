import { Search } from 'lucide-react'
import type { EventCounts } from '../../types'
import type {
  EventFilterState,
  HoursFilter,
  LogFilter,
  SeverityFilter
} from './filterTypes'

interface EventFiltersProps {
  filters: EventFilterState
  counts: EventCounts | null
  onChange: (partial: Partial<EventFilterState>) => void
}

interface Option<T> {
  value: T
  label: string
}

function Segmented<T extends string | number>({
  options,
  value,
  onSelect
}: {
  options: Option<T>[]
  value: T
  onSelect: (value: T) => void
}) {
  return (
    <div className="inline-flex rounded-lg border border-white/[0.06] bg-base-800/60 p-0.5">
      {options.map((option) => {
        const active = option.value === value
        return (
          <button
            key={String(option.value)}
            type="button"
            onClick={() => onSelect(option.value)}
            className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
              active
                ? 'bg-indigo-500/20 text-indigo-200'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}

const severityOptions: Option<SeverityFilter>[] = [
  { value: 'all', label: 'Alle' },
  { value: 'critical', label: 'Kritisch' },
  { value: 'high', label: 'Hoch' },
  { value: 'warning', label: 'Warnung' }
]

const logOptions: Option<LogFilter>[] = [
  { value: 'all', label: 'Alle Protokolle' },
  { value: 'System', label: 'System' },
  { value: 'Application', label: 'Anwendung' },
  { value: 'Microsoft-Windows-WindowsUpdateClient/Operational', label: 'Windows Update' }
]

const hoursOptions: Option<HoursFilter>[] = [
  { value: 1, label: '1 Stunde' },
  { value: 24, label: '24 Stunden' },
  { value: 168, label: '7 Tage' }
]

export function EventFilters({ filters, counts, onChange }: EventFiltersProps) {
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <Segmented
          options={severityOptions}
          value={filters.severity}
          onSelect={(value) => onChange({ severity: value })}
        />
        {counts && (
          <div className="ml-auto flex items-center gap-2 text-xs">
            <CountBadge label="Kritisch" value={counts.critical} tone="text-red-300 bg-red-500/10 border-red-500/20" />
            <CountBadge label="Hoch" value={counts.high} tone="text-orange-300 bg-orange-500/10 border-orange-500/20" />
            <CountBadge label="Warnungen" value={counts.warning} tone="text-amber-300 bg-amber-500/10 border-amber-500/20" />
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Segmented options={logOptions} value={filters.log} onSelect={(value) => onChange({ log: value })} />
        <Segmented options={hoursOptions} value={filters.hours} onSelect={(value) => onChange({ hours: value })} />

        <div className="relative ml-auto min-w-[220px] flex-1 md:max-w-xs">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
          <input
            type="search"
            value={filters.search}
            onChange={(event) => onChange({ search: event.target.value })}
            placeholder="Ereignis-ID, Quelle oder Text…"
            className="w-full rounded-lg border border-white/[0.06] bg-base-800/60 py-2 pl-9 pr-3 text-sm text-slate-100 placeholder:text-slate-500 focus:border-indigo-500/40 focus:outline-none"
          />
        </div>
      </div>
    </div>
  )
}

function CountBadge({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-md border px-2 py-1 ${tone}`}>
      <span>{label}</span>
      <span className="font-semibold tabular-nums">{value}</span>
    </span>
  )
}
