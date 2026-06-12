import { useMemo } from 'react'
import { useSecondsAgo } from '../hooks'
import { trendArrow, signed, rootCauseLabel } from '../utils'

function Skeleton({ className }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className}`} />
}

function fmtDuration(secs) {
  if (secs == null) return null
  const m = Math.floor(secs / 60)
  if (m >= 60) return `${Math.floor(m / 60)}h ${m % 60}m`
  return `${m}m ${secs % 60}s`
}

const SEV_BORDER = {
  Critical: 'border-red-300',
  Warning:  'border-yellow-300',
  None:     'border-green-300',
}

const SEV_TEXT = {
  Critical: 'text-red-600',
  Warning:  'text-yellow-600',
  None:     'text-green-600',
}

function Tile({ title, children, sev }) {
  const border = SEV_BORDER[sev] ?? 'border-gray-200'
  return (
    <div className={`flex-1 min-w-0 border-2 rounded-xl p-4 bg-white ${border}`}>
      <p className="text-xs font-medium text-gray-400 uppercase tracking-wide mb-2">{title}</p>
      {children}
    </div>
  )
}

function WaitBar({ waitMins, slaMins }) {
  const pct = Math.min(110, ((waitMins ?? 0) / (slaMins ?? 15)) * 100)
  const barColor = pct > 100 ? 'bg-red-500' : pct > 60 ? 'bg-yellow-400' : 'bg-green-500'
  return (
    <div className="mt-2">
      <div className="h-2 w-full bg-gray-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full transition-all ${barColor}`} style={{ width: `${Math.min(100, pct)}%` }} />
      </div>
      <p className="text-xs text-gray-400 mt-1">{waitMins?.toFixed(1) ?? '∞'} / {slaMins ?? '—'} min SLA</p>
    </div>
  )
}

export default function StatusRow({ status: s, loading, error, fetchedAt, breachStartMs, onRefresh }) {
  const secsAgo    = useSecondsAgo(fetchedAt)
  const breachDate = useMemo(() => breachStartMs ? new Date(breachStartMs) : null, [breachStartMs])
  const breachSecs = useSecondsAgo(breachDate)
  const sev        = s?.alertSeverity ?? 'None'
  const sevText    = SEV_TEXT[sev] ?? 'text-gray-500'

  if (loading) {
    return (
      <div className="border rounded-xl p-5 bg-white shadow-sm space-y-3">
        <div className="flex items-center gap-3">
          <Skeleton className="h-5 w-24" />
          <Skeleton className="h-5 w-32" />
          <Skeleton className="h-5 w-40" />
        </div>
        <div className="flex gap-3">
          {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-24 flex-1 rounded-xl" />)}
        </div>
      </div>
    )
  }

  const breachDuration = fmtDuration(breachSecs)
  const gapN = s?.gapPerMin ?? 0

  return (
    <div className="border rounded-xl p-5 bg-white shadow-sm space-y-3">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-3">
          <span className="text-xs font-mono text-gray-400">qbi-queue</span>
          <span className={`text-sm font-bold ${sevText}`}>
            {sev === 'Critical' ? '🔴 CRITICAL' : sev === 'Warning' ? '🟡 WARNING' : '✅ OK'}
          </span>
          <span className="text-sm text-gray-600">{rootCauseLabel(s?.rootCause)}</span>
          {breachDuration && (
            <span className="text-sm font-medium text-red-600">· {breachDuration}</span>
          )}
        </div>
        <div className="flex items-center gap-3 text-xs text-gray-400">
          {error && <span className="text-red-600">⚠ {error}</span>}
          <span>
            {secsAgo == null ? 'Loading…' : secsAgo < 4
              ? <span className="flex items-center gap-1"><span className="w-2 h-2 border border-blue-400 border-t-transparent rounded-full animate-spin inline-block" /> Just updated</span>
              : `${secsAgo}s ago`}
          </span>
          {onRefresh && (
            <button onClick={onRefresh} className="px-2 py-0.5 rounded hover:bg-gray-100 transition-colors">
              ↺ Refresh
            </button>
          )}
        </div>
      </div>

      {/* 4 tiles */}
      <div className="flex gap-3">

        {/* Tile 1 — Active Count */}
        <Tile title="Active Count" sev={sev}>
          <p className="text-4xl font-bold tabular-nums text-gray-800">{s?.activeCount ?? '—'}</p>
          <p className="text-sm text-gray-500 mt-1">
            {s?.trendLabel ?? '—'} {trendArrow(s?.trendLabel)}
            <span className={`ml-2 text-xs ${gapN > 0 ? 'text-red-500' : gapN < 0 ? 'text-green-500' : 'text-gray-400'}`}>
              ({signed(gapN)}/min)
            </span>
          </p>
        </Tile>

        {/* Tile 2 — Wait Time */}
        <Tile title="Wait Time" sev={sev}>
          <p className="text-3xl font-bold tabular-nums text-gray-800">
            {s?.waitTimeMinutes != null ? `${s.waitTimeMinutes.toFixed(1)}m` : '∞'}
          </p>
          <WaitBar waitMins={s?.waitTimeMinutes} slaMins={s?.slaMinutes} />
        </Tile>

        {/* Tile 3 — Throughput */}
        <Tile title="Throughput" sev={sev}>
          <div className="space-y-1.5">
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">In</span>
              <span className="font-medium text-purple-600 tabular-nums">
                {s?.incomingPerMin != null ? `${s.incomingPerMin.toFixed(1)}/min` : '—'}
              </span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Out</span>
              <span className="font-medium text-green-600 tabular-nums">
                {s?.outgoingPerMin != null ? `${s.outgoingPerMin.toFixed(1)}/min` : '—'}
              </span>
            </div>
            <div className="flex justify-between text-sm border-t border-gray-100 pt-1.5">
              <span className="text-gray-500">Gap</span>
              <span className={`font-medium tabular-nums ${gapN > 0 ? 'text-red-500' : gapN < 0 ? 'text-green-500' : 'text-gray-500'}`}>
                {signed(gapN)}/min
              </span>
            </div>
          </div>
        </Tile>

        {/* Tile 4 — DLQ */}
        <Tile title="Dead Letter Queue" sev={sev}>
          <p className={`text-4xl font-bold tabular-nums ${(s?.dlqCount ?? 0) > 0 ? 'text-red-600' : 'text-gray-800'}`}>
            {s?.dlqCount ?? 0}
          </p>
          {(s?.dlqCount ?? 0) > 0 && (
            <p className="text-xs text-red-500 mt-1">Poison messages — investigate</p>
          )}
          {(s?.dlqCount ?? 0) === 0 && (
            <p className="text-xs text-gray-400 mt-1">No dead letters</p>
          )}
        </Tile>

      </div>
    </div>
  )
}
