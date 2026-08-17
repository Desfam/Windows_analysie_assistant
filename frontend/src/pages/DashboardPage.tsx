import { useMemo, useState } from 'react'
import { StatusBar } from '../components/StatusBar'
import { SettingsDialog } from '../components/SettingsDialog'
import { Sidebar } from '../components/sidebar/Sidebar'
import { EventsPanel } from '../components/events/EventsPanel'
import { EventDetailDrawer } from '../components/events/EventDetailDrawer'
import { defaultFilters, severityToLevelParam, type EventFilterState } from '../components/events/filterTypes'
import { useSettings } from '../hooks/useSettings'
import { useSystemData } from '../hooks/useSystemData'
import { useEventsData } from '../hooks/useEventsData'
import type { EventItem } from '../types'
import type { EventQueryParams } from '../services/api'

interface DashboardPageProps {
  onInvestigate?: (event: EventItem) => void
}

export function DashboardPage({ onInvestigate }: DashboardPageProps = {}) {
  const { settings, update, animationsEnabled } = useSettings()
  const [filters, setFilters] = useState<EventFilterState>(defaultFilters)
  const [selectedEvent, setSelectedEvent] = useState<EventItem | null>(null)
  const [settingsOpen, setSettingsOpen] = useState(false)

  const system = useSystemData(settings.systemIntervalSec)

  const eventParams = useMemo<EventQueryParams>(
    () => ({
      level: severityToLevelParam(filters.severity),
      hours: filters.hours,
      log: filters.log === 'all' ? undefined : filters.log,
      search: filters.search.trim() || undefined
    }),
    [filters]
  )

  const events = useEventsData(eventParams, settings.eventsIntervalSec)

  const machineName =
    system.data.summary.data?.machineName ?? events.data?.events[0]?.machineName ?? 'Dieser Rechner'

  const lastUpdated =
    [system.lastUpdated, events.lastUpdated]
      .filter((value): value is Date => value != null)
      .sort((a, b) => b.getTime() - a.getTime())[0] ?? null

  const handleFilterChange = (partial: Partial<EventFilterState>) => {
    setFilters((prev) => ({ ...prev, ...partial }))
  }

  const handleRefresh = () => {
    void system.refresh()
    void events.refresh()
  }

  return (
    <div className="flex h-full flex-col bg-base-900">
      <StatusBar
        machineName={machineName}
        lastUpdated={lastUpdated}
        loading={system.loading || events.loading}
        hasError={system.hasError || events.error != null}
        onRefresh={handleRefresh}
        onOpenSettings={() => setSettingsOpen(true)}
      />

      <main className="grid min-h-0 flex-1 grid-cols-1 gap-4 overflow-hidden p-4 lg:grid-cols-[minmax(280px,25%)_1fr]">
        <div className="min-h-0 overflow-y-auto pr-1">
          <Sidebar data={system.data} loading={system.loading} animate={animationsEnabled} />
        </div>

        <EventsPanel
          data={events.data}
          loading={events.loading}
          error={events.error}
          newKeys={events.newKeys}
          filters={filters}
          animate={animationsEnabled}
          onFilterChange={handleFilterChange}
          onSelect={setSelectedEvent}
          onInvestigate={onInvestigate}
        />
      </main>

      <EventDetailDrawer
        event={selectedEvent}
        animate={animationsEnabled}
        onClose={() => setSelectedEvent(null)}
      />

      <SettingsDialog
        open={settingsOpen}
        settings={settings}
        onChange={update}
        onClose={() => setSettingsOpen(false)}
      />
    </div>
  )
}
