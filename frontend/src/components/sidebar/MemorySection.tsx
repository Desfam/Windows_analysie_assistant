import { MemoryStick } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { MemoryInfo } from '../../types'
import { formatBytes, formatPercent } from '../../lib/format'
import { InfoRow } from '../common/InfoRow'
import { ProgressBar } from '../common/ProgressBar'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<MemoryInfo>
  loading: boolean
  animate: boolean
}

export function MemorySection({ section, loading, animate }: Props) {
  const info = section.data

  return (
    <SectionCard
      title="Arbeitsspeicher"
      icon={MemoryStick}
      status={info?.status ?? 'NotChecked'}
      loading={loading && !info}
      failed={section.failed}
      animate={animate}
    >
      <div className="divide-y divide-white/[0.04]">
        <InfoRow label="Gesamt" value={formatBytes(info?.totalBytes)} />
        <InfoRow label="Verwendet" value={formatBytes(info?.usedBytes)} />
        <InfoRow label="Verfügbar" value={formatBytes(info?.availableBytes)} />
      </div>

      {info?.usagePercent != null && (
        <div className="mt-3 space-y-1.5">
          <div className="flex items-center justify-between text-xs text-slate-400">
            <span>Auslastung</span>
            <span className="text-slate-200">{formatPercent(info.usagePercent)}</span>
          </div>
          <ProgressBar percent={info.usagePercent} animate={animate} />
        </div>
      )}
    </SectionCard>
  )
}
