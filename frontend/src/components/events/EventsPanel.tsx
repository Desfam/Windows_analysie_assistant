import { AnimatePresence } from 'framer-motion'
import { AlertOctagon, Inbox, ShieldAlert } from 'lucide-react'
import type { EventItem, EventsResponse } from '../../types'
import { Skeleton } from '../common/Skeleton'
import { EventCard } from './EventCard'
import { EventFilters } from './EventFilters'
import type { EventFilterState } from './filterTypes'

interface EventsPanelProps {
  data: EventsResponse | null
  loading: boolean
  error: string | null
  newKeys: Set<string>
  filters: EventFilterState
  animate: boolean
  onFilterChange: (partial: Partial<EventFilterState>) => void
  onSelect: (event: EventItem) => void
  onInvestigate?: (event: EventItem) => void
}

export function EventsPanel({
  data,
  loading,
  error,
  newKeys,
  filters,
  animate,
  onFilterChange,
  onSelect,
  onInvestigate
}: EventsPanelProps) {
  const events = data?.events ?? []

  return (
    <section className="flex min-h-0 flex-1 flex-col gap-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-slate-100">Windows-Ereignisse</h2>
          <p className="text-sm text-slate-500">Relevante Kritisch-, Fehler- und Warnungsereignisse</p>
        </div>
      </div>

      <EventFilters filters={filters} counts={data?.counts ?? null} onChange={onFilterChange} />

      {data?.warnings?.map((warning) => (
        <div
          key={warning}
          className="flex items-start gap-2 rounded-lg border border-amber-500/20 bg-amber-500/[0.06] px-3 py-2 text-sm text-amber-200"
        >
          <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{warning}</span>
        </div>
      ))}

      <div className="min-h-0 flex-1 space-y-3 overflow-y-auto pr-1">
        {loading && !data ? (
          <SkeletonList />
        ) : error ? (
          <StateMessage
            icon={<AlertOctagon className="h-6 w-6 text-red-300" />}
            title="Ereignisse konnten nicht geladen werden"
            description={error}
          />
        ) : events.length === 0 ? (
          <StateMessage
            icon={<Inbox className="h-6 w-6 text-slate-400" />}
            title="Keine passenden Ereignisse"
            description="Im gewählten Zeitraum wurden keine Ereignisse gefunden, die den Filtern entsprechen."
          />
        ) : (
          <AnimatePresence initial={false}>
            {events.map((event) => (
              <EventCard
                key={event.id}
                event={event}
                isNew={newKeys.has(event.eventKey)}
                animate={animate}
                onSelect={onSelect}
                onInvestigate={onInvestigate}
              />
            ))}
          </AnimatePresence>
        )}
      </div>
    </section>
  )
}

function SkeletonList() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }).map((_, index) => (
        <div key={index} className="rounded-xl border border-white/[0.05] bg-base-700/60 p-4">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="mt-3 h-4 w-3/4" />
          <Skeleton className="mt-2 h-3 w-full" />
          <Skeleton className="mt-3 h-3 w-1/2" />
        </div>
      ))}
    </div>
  )
}

function StateMessage({
  icon,
  title,
  description
}: {
  icon: React.ReactNode
  title: string
  description: string
}) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-white/[0.05] bg-base-700/40 px-6 py-16 text-center">
      <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-white/[0.04]">{icon}</div>
      <h3 className="text-base font-medium text-slate-200">{title}</h3>
      <p className="mt-1 max-w-sm text-sm text-slate-500">{description}</p>
    </div>
  )
}
