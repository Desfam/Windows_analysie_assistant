import { MonitorSmartphone } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { SystemSummary } from '../../types'
import { formatDateTime, orNotAvailable } from '../../lib/format'
import { InfoRow } from '../common/InfoRow'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<SystemSummary>
  loading: boolean
  animate: boolean
}

export function GeneralSection({ section, loading, animate }: Props) {
  const info = section.data

  return (
    <SectionCard
      title="Allgemein"
      icon={MonitorSmartphone}
      status={info?.status ?? 'NotChecked'}
      loading={loading && !info}
      failed={section.failed}
      animate={animate}
    >
      <div className="divide-y divide-white/[0.04]">
        <InfoRow label="Rechnername" value={orNotAvailable(info?.machineName)} />
        <InfoRow label="Hersteller" value={orNotAvailable(info?.manufacturer)} />
        <InfoRow label="Modell" value={orNotAvailable(info?.model)} />
        <InfoRow label="Systemtyp" value={orNotAvailable(info?.systemType)} />
        <InfoRow label="Letzter Start" value={formatDateTime(info?.lastBootTime)} />
        <InfoRow label="Laufzeit" value={orNotAvailable(info?.uptime)} />
        <InfoRow label="Benutzer" value={orNotAvailable(info?.currentUser)} />
      </div>
    </SectionCard>
  )
}
