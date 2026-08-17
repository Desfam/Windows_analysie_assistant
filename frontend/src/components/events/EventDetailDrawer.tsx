import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { Check, ChevronDown, Copy, X } from 'lucide-react'
import type { EventItem } from '../../types'
import { severityStyles } from '../../lib/status'
import { formatDateTime } from '../../lib/format'

interface EventDetailDrawerProps {
  event: EventItem | null
  animate: boolean
  onClose: () => void
}

export function EventDetailDrawer({ event, animate, onClose }: EventDetailDrawerProps) {
  return (
    <AnimatePresence>
      {event && <DrawerContent event={event} animate={animate} onClose={onClose} />}
    </AnimatePresence>
  )
}

function DrawerContent({
  event,
  animate,
  onClose
}: {
  event: EventItem
  animate: boolean
  onClose: () => void
}) {
  const [copied, setCopied] = useState(false)
  const [showXml, setShowXml] = useState(false)
  const style = severityStyles[event.severity]

  const copyTechnical = async () => {
    const text = [
      `Schweregrad: ${style.label}`,
      `Ereignis-ID: ${event.eventId}`,
      `Provider: ${event.providerName ?? ''}`,
      `Protokoll: ${event.logName ?? ''}`,
      `Rechnername: ${event.machineName ?? ''}`,
      `Zeitpunkt: ${formatDateTime(event.lastSeen)}`,
      '',
      event.originalMessage ?? '',
      '',
      event.rawXml ?? ''
    ].join('\n')

    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
    }
  }

  return (
    <>
      <motion.div
        className="fixed inset-0 z-40 bg-black/50"
        initial={animate ? { opacity: 0 } : false}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        onClick={onClose}
      />
      <motion.aside
        className="fixed right-0 top-0 z-50 flex h-full w-full max-w-md flex-col border-l border-white/[0.08] bg-base-800 shadow-2xl"
        initial={animate ? { x: '100%' } : false}
        animate={{ x: 0 }}
        exit={{ x: '100%' }}
        transition={{ type: 'tween', duration: 0.25 }}
      >
        <header className="flex items-start justify-between gap-3 border-b border-white/[0.06] p-5">
          <div>
            <span className={`inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-semibold uppercase ${style.badge}`}>
              <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
              {style.label}
            </span>
            <h2 className="mt-2 text-lg font-semibold text-slate-100">{event.title}</h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
            aria-label="Schließen"
          >
            <X className="h-5 w-5" />
          </button>
        </header>

        <div className="flex-1 space-y-5 overflow-y-auto p-5">
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <Field label="Ereignis-ID" value={String(event.eventId)} />
            <Field label="Protokoll" value={event.logName ?? '—'} />
            <Field label="Provider" value={event.providerName ?? '—'} />
            <Field label="Rechnername" value={event.machineName ?? '—'} />
            <Field label="Zuletzt" value={formatDateTime(event.lastSeen)} />
            <Field label="Zuerst" value={formatDateTime(event.firstSeen)} />
          </dl>

          <Block title="Erklärung">
            <p className="text-sm leading-relaxed text-slate-300">{event.summary}</p>
          </Block>

          {event.count > 1 && (
            <Block title={`Wiederholungen (${event.count})`}>
              <div className="max-h-40 space-y-1 overflow-y-auto text-xs text-slate-400">
                {event.occurrences.map((time, index) => (
                  <div key={index} className="tabular-nums">
                    {formatDateTime(time)}
                  </div>
                ))}
              </div>
            </Block>
          )}

          <Block title="Originalmeldung">
            <pre className="whitespace-pre-wrap break-words rounded-lg bg-base-900/70 p-3 text-xs leading-relaxed text-slate-300">
              {event.originalMessage ?? 'Keine Meldung verfügbar.'}
            </pre>
          </Block>

          <div>
            <button
              type="button"
              onClick={() => setShowXml((value) => !value)}
              className="flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-200"
            >
              <ChevronDown className={`h-4 w-4 transition-transform ${showXml ? 'rotate-180' : ''}`} />
              Rohe Ereignisdaten (XML)
            </button>
            {showXml && (
              <pre className="mt-2 max-h-64 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-base-900/70 p-3 text-[11px] leading-relaxed text-slate-400">
                {event.rawXml ?? 'Keine Rohdaten verfügbar.'}
              </pre>
            )}
          </div>
        </div>

        <footer className="border-t border-white/[0.06] p-4">
          <button
            type="button"
            onClick={copyTechnical}
            className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-indigo-500/30 bg-indigo-500/10 py-2.5 text-sm font-medium text-indigo-200 transition-colors hover:bg-indigo-500/20"
          >
            {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
            {copied ? 'Kopiert' : 'Technische Daten kopieren'}
          </button>
        </footer>
      </motion.aside>
    </>
  )
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="mt-0.5 break-words text-slate-200">{value}</dd>
    </div>
  )
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500">{title}</h3>
      {children}
    </section>
  )
}
