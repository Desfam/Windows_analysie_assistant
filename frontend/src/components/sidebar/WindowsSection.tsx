import { AppWindow } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { WindowsInfo } from '../../types'
import { formatDate, orNotAvailable } from '../../lib/format'
import { InfoRow } from '../common/InfoRow'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<WindowsInfo>
  loading: boolean
  animate: boolean
}

export function WindowsSection({ section, loading, animate }: Props) {
  const info = section.data
  const updates = info?.recentUpdates ?? []

  return (
    <SectionCard
      title="Windows"
      icon={AppWindow}
      status={info?.status ?? 'NotChecked'}
      loading={loading && !info}
      failed={section.failed}
      animate={animate}
    >
      <div className="divide-y divide-white/[0.04]">
        <InfoRow label="Edition" value={orNotAvailable(info?.edition)} />
        <InfoRow label="Version" value={orNotAvailable(info?.version)} />
        <InfoRow label="Build" value={orNotAvailable(info?.build)} />
        <InfoRow label="Installiert am" value={formatDate(info?.installDate)} />
        <InfoRow
          label="Ausstehende Updates"
          value={info?.pendingUpdateCount ?? 'Nicht verfügbar'}
        />
      </div>

      {updates.length > 0 && (
        <div className="mt-3">
          <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-slate-500">
            Letzte Updates
          </p>
          <div className="space-y-1">
            {updates.slice(0, 5).map((update) => (
              <div key={update.id} className="flex items-center justify-between text-xs">
                <span className="text-slate-200">{orNotAvailable(update.id)}</span>
                <span className="text-slate-500">{formatDate(update.installedOn)}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </SectionCard>
  )
}
