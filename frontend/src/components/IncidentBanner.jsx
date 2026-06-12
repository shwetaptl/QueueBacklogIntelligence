import { useMemo } from 'react'
import { useSecondsAgo } from '../hooks'
import { rootCauseLabel } from '../utils'

function fmtDuration(secs) {
  if (secs == null || secs < 0) return null
  const m = Math.floor(secs / 60)
  if (m >= 60) return `${Math.floor(m / 60)}h ${m % 60}m`
  return `${m}m ${secs % 60}s`
}

export default function IncidentBanner({ status, breachStartMs, dismissedSev, onDismiss }) {
  const sev = status?.alertSeverity
  const breachDate = useMemo(
    () => (breachStartMs ? new Date(breachStartMs) : null),
    [breachStartMs]
  )
  const breachSecs = useSecondsAgo(breachDate)

  if (!sev || sev === 'None' || sev === dismissedSev) return null

  const isCritical = sev === 'Critical'
  const duration   = fmtDuration(breachSecs)
  const cause      = rootCauseLabel(status?.rootCause)
  const waitTime   = status?.waitTimeMinutes != null
    ? `${status.waitTimeMinutes.toFixed(1)} min` : '∞'

  return (
    <div className={`rounded-xl px-5 py-3 flex items-center justify-between gap-4 ${
      isCritical ? 'bg-red-600 text-white' : 'bg-yellow-400 text-yellow-950'
    }`}>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm font-medium">
        <span className="font-bold">{isCritical ? '🔴 CRITICAL' : '🟡 WARNING'}</span>
        {duration && <span>· Breach duration: {duration}</span>}
        <span>· Root cause: {cause}</span>
        <span>· Wait time: {waitTime}</span>
      </div>
      <button
        onClick={onDismiss}
        className={`shrink-0 text-xs px-3 py-1 rounded border transition-colors ${
          isCritical
            ? 'border-red-300 hover:bg-red-500'
            : 'border-yellow-600 hover:bg-yellow-300'
        }`}
      >
        Acknowledge
      </button>
    </div>
  )
}
