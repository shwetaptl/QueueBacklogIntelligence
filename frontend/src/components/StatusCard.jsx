import { useMemo } from 'react'
import { useSecondsAgo } from '../hooks'
import { cardBg, trendArrow, signed } from '../utils'

function Skeleton({ className }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className}`} />
}

function SeverityBadge({ sev }) {
  const map = {
    Critical: ['bg-red-100 text-red-800',      '🔴 CRITICAL'],
    Warning:  ['bg-yellow-100 text-yellow-800', '🟡 WARNING'],
    None:     ['bg-green-100 text-green-800',   '✅ OK'],
  }
  const [cls, label] = map[sev] ?? ['bg-gray-100 text-gray-500', '⬜ UNKNOWN']
  return (
    <span className={`inline-flex items-center gap-1 rounded-full text-sm px-4 py-1.5 font-bold ${cls}`}>
      {label}
    </span>
  )
}

function fmtDuration(secs) {
  if (secs == null) return null
  const m = Math.floor(secs / 60)
  if (m >= 60) return `${Math.floor(m / 60)}h ${m % 60}m`
  return `${m}m ${secs % 60}s`
}

export default function StatusCard({ status: s, loading, error, fetchedAt, breachStartMs, onRefresh }) {
  const secsAgo    = useSecondsAgo(fetchedAt)
  const breachDate = useMemo(() => breachStartMs ? new Date(breachStartMs) : null, [breachStartMs])
  const breachSecs = useSecondsAgo(breachDate)

  if (loading) {
    return (
      <div className="border rounded-xl p-6 bg-white shadow-sm">
        <div className="grid grid-cols-3 gap-8">
          {[0, 1, 2].map(i => (
            <div key={i} className="space-y-3">
              <Skeleton className="h-5 w-28" />
              <Skeleton className="h-9 w-32" />
              <Skeleton className="h-4 w-20" />
            </div>
          ))}
        </div>
      </div>
    )
  }

  const gapN = s?.gapPerMin    ?? 0
  const accN = s?.acceleration ?? 0
  const gapCls = gapN > 0 ? 'text-red-600' : gapN < 0 ? 'text-green-600' : 'text-gray-600'
  const accCls = accN > 0 ? 'text-red-600' : accN < 0 ? 'text-green-600' : 'text-gray-600'
  const slaCls = {
    BREACHING: 'text-red-600 font-bold',
    OK:        'text-green-600 font-bold',
  }[s?.slaStatus] ?? 'text-gray-400'

  const breachDuration = fmtDuration(breachSecs)

  return (
    <div className={`border rounded-xl p-6 shadow-sm ${cardBg(s?.alertSeverity)}`}>
      {error && (
        <div className="mb-3 text-xs bg-red-50 border border-red-200 text-red-600 px-3 py-1.5 rounded">
          ⚠ API Error — {error} — retrying…
        </div>
      )}

      <div className="grid grid-cols-3 gap-8">
        {/* Left — identity */}
        <div className="space-y-3">
          <p className="text-xs font-mono text-gray-400">qbi-queue</p>
          <SeverityBadge sev={s?.alertSeverity} />
          <p className="text-sm font-medium text-gray-600">{s?.rootCause ?? '—'}</p>
        </div>

        {/* Center — key metrics */}
        <div className="space-y-3">
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">Active Messages</p>
            <p className="text-4xl font-bold tabular-nums text-gray-800">{s?.activeCount ?? '—'}</p>
          </div>
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">Wait Time</p>
            <p className="text-3xl font-bold tabular-nums text-gray-700">
              {s?.waitTimeMinutes != null ? `${s.waitTimeMinutes.toFixed(1)} min` : '∞'}
            </p>
          </div>
          <div className="flex gap-5 text-sm">
            <div>
              <p className="text-xs text-gray-400">SLA</p>
              <p className="font-medium text-gray-700">{s?.slaMinutes ?? '—'} min</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Trend</p>
              <p className="font-medium text-gray-700">
                {s?.trendLabel ?? '—'} {trendArrow(s?.trendLabel)}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-400">DLQ</p>
              <p className={`font-medium ${(s?.dlqCount ?? 0) > 0 ? 'text-red-600' : 'text-gray-700'}`}>
                {s?.dlqCount ?? 0}
              </p>
            </div>
          </div>
        </div>

        {/* Right — rates & SLA */}
        <div className="space-y-4">
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">Gap / min</p>
            <p className={`text-3xl font-bold tabular-nums ${gapCls}`}>{signed(gapN)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">Acceleration</p>
            <p className={`text-3xl font-bold tabular-nums ${accCls}`}>{signed(accN)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">SLA Status</p>
            <p className={`text-lg ${slaCls}`}>{s?.slaStatus ?? '—'}</p>
          </div>
          {breachDuration && (
            <div>
              <p className="text-xs text-gray-400 uppercase tracking-wide mb-0.5">Breach Duration</p>
              <p className="text-lg font-bold text-red-600">{breachDuration}</p>
            </div>
          )}
        </div>
      </div>

      {/* Footer — live counter + refresh */}
      <div className="mt-4 pt-3 border-t border-black/5 flex items-center justify-between text-xs text-gray-400">
        <div className="flex items-center gap-2">
          {secsAgo === null ? (
            <span>Loading…</span>
          ) : secsAgo < 4 ? (
            <>
              <span className="w-3 h-3 border-2 border-blue-400 border-t-transparent rounded-full animate-spin" />
              <span>Just updated</span>
            </>
          ) : (
            <span>Last updated: {secsAgo}s ago</span>
          )}
        </div>
        {onRefresh && (
          <button
            onClick={onRefresh}
            className="flex items-center gap-1 px-2 py-0.5 rounded hover:bg-black/5 transition-colors"
          >
            ↺ Refresh
          </button>
        )}
      </div>
    </div>
  )
}
