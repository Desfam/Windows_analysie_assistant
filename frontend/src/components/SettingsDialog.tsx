import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import type { AppSettings } from '../hooks/useSettings'

interface SettingsDialogProps {
  open: boolean
  settings: AppSettings
  onChange: (partial: Partial<AppSettings>) => void
  onClose: () => void
}

const intervalOptions = [10, 15, 30, 60, 120]

export function SettingsDialog({ open, settings, onChange, onClose }: SettingsDialogProps) {
  return (
    <AnimatePresence>
      {open && (
        <>
          <motion.div
            className="fixed inset-0 z-40 bg-black/50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />
          <motion.div
            className="fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-white/[0.08] bg-base-800 p-6 shadow-2xl"
            initial={{ opacity: 0, scale: 0.96 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.96 }}
          >
            <div className="mb-5 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-100">Einstellungen</h2>
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
                aria-label="Schließen"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="space-y-5">
              <SelectRow
                label="Systeminformationen aktualisieren"
                suffix="Sekunden"
                value={settings.systemIntervalSec}
                onChange={(value) => onChange({ systemIntervalSec: value })}
              />
              <SelectRow
                label="Windows-Ereignisse aktualisieren"
                suffix="Sekunden"
                value={settings.eventsIntervalSec}
                onChange={(value) => onChange({ eventsIntervalSec: value })}
              />

              <label className="flex items-center justify-between gap-3">
                <span>
                  <span className="block text-sm text-slate-200">Animationen reduzieren</span>
                  <span className="block text-xs text-slate-500">
                    Dezente Bewegungen und Übergänge deaktivieren
                  </span>
                </span>
                <button
                  type="button"
                  role="switch"
                  aria-checked={settings.reduceMotion}
                  onClick={() => onChange({ reduceMotion: !settings.reduceMotion })}
                  className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
                    settings.reduceMotion ? 'bg-indigo-500/70' : 'bg-white/[0.12]'
                  }`}
                >
                  <span
                    className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-transform ${
                      settings.reduceMotion ? 'translate-x-5' : 'translate-x-0.5'
                    }`}
                  />
                </button>
              </label>
            </div>

            <p className="mt-6 text-xs text-slate-500">
              Die Einstellungen werden lokal in diesem Browser gespeichert.
            </p>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}

function SelectRow({
  label,
  suffix,
  value,
  onChange
}: {
  label: string
  suffix: string
  value: number
  onChange: (value: number) => void
}) {
  return (
    <label className="flex items-center justify-between gap-3">
      <span className="text-sm text-slate-200">{label}</span>
      <span className="inline-flex items-center gap-2">
        <select
          value={value}
          onChange={(event) => onChange(Number(event.target.value))}
          className="rounded-lg border border-white/[0.08] bg-base-700 px-2.5 py-1.5 text-sm text-slate-100 focus:border-indigo-500/40 focus:outline-none"
        >
          {intervalOptions.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        <span className="text-xs text-slate-500">{suffix}</span>
      </span>
    </label>
  )
}
