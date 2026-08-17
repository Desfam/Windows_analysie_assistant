import { Cpu } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { CpuInfo } from '../../types'
import { formatGhz, formatPercent, orNotAvailable } from '../../lib/format'
import { InfoRow } from '../common/InfoRow'
import { ProgressBar } from '../common/ProgressBar'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<CpuInfo>
  loading: boolean
  animate: boolean
}

export function CpuSection({ section, loading, animate }: Props) {
  const info = section.data

  return (
    <SectionCard
      title="Prozessor"
      icon={Cpu}
      status={info?.status ?? 'NotChecked'}
      loading={loading && !info}
      failed={section.failed}
      animate={animate}
    >
      <div className="divide-y divide-white/[0.04]">
        <InfoRow label="Modell" value={orNotAvailable(info?.model)} />
        <InfoRow label="Hersteller" value={orNotAvailable(info?.manufacturer)} />
        <InfoRow label="Physische Kerne" value={orNotAvailable(info?.physicalCores)} />
        <InfoRow label="Logische Prozessoren" value={orNotAvailable(info?.logicalProcessors)} />
        <InfoRow label="Max. Takt" value={formatGhz(info?.maxClockSpeedGhz)} />
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
