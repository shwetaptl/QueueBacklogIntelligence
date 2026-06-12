import { useState } from 'react'
import { parseUTC, fmtDateTime, rootCauseLabel } from '../utils'

function Skeleton({ className }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className}`} />
}

const TH = ({ right, sortable, active, asc, onClick, children }) => (
  <th
    className={`pb-2 text-xs font-medium text-gray-400 uppercase tracking-wide
      ${right ? 'text-right' : 'text-left'}
      ${sortable ? 'cursor-pointer hover:text-gray-600 select-none' : ''}`}
    onClick={sortable ? onClick : undefined}
  >
    {children}
    {active && <span className="ml-1">{asc ? '↑' : '↓'}</span>}
  </th>
)

function todayStr() {
  return new Date().toISOString().slice(0, 10)
}
function daysAgoStr(n) {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return d.toISOString().slice(0, 10)
}

export default function IncidentTable({ alerts, loading, error }) {
  const [fromDate,    setFromDate]    = useState(daysAgoStr(30))
  const [toDate,      setToDate]      = useState(todayStr())
  const [minDuration, setMinDuration] = useState('')
  const [sortAsc,     setSortAsc]     = useState(false)
  const [expandedId,  setExpandedId]  = useState(null)

  const fromMs = fromDate ? new Date(fromDate + 'T00:00:00Z').getTime() : 0
  const toMs   = toDate   ? new Date(toDate   + 'T23:59:59Z').getTime() : Infinity
  const minDur = Number(minDuration) || 0

  let rows = (alerts ?? []).filter(a => {
    const t = parseUTC(a.openedAtUtc)?.getTime() ?? 0
    return t >= fromMs && t <= toMs && (a.durationMinutes ?? 0) >= minDur
  })

  rows = [...rows].sort((a, b) => {
    const diff = new Date(a.openedAtUtc) - new Date(b.openedAtUtc)
    return sortAsc ? diff : -diff
  })

  if (loading) {
    return (
      <div className="border rounded-xl p-6 bg-white shadow-sm">
        <Skeleton className="h-5 w-48 mb-4" />
        <div className="space-y-3">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-9 w-full" />)}
        </div>
      </div>
    )
  }

  return (
    <div className="border rounded-xl p-6 bg-white shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
        <h2 className="text-sm font-semibold text-gray-700">Incident History</h2>
        {error && <span className="text-xs text-red-600">⚠ {error}</span>}
        <div className="flex flex-wrap items-center gap-2 text-xs">
          <span className="text-gray-400">From</span>
          <input
            type="date"
            value={fromDate}
            onChange={e => setFromDate(e.target.value)}
            className="border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <span className="text-gray-400">to</span>
          <input
            type="date"
            value={toDate}
            onChange={e => setToDate(e.target.value)}
            className="border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <span className="text-gray-400">Min duration</span>
          <input
            type="number"
            value={minDuration}
            onChange={e => setMinDuration(e.target.value)}
            placeholder="0"
            min="0"
            className="w-16 border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <span className="text-gray-400">min</span>
        </div>
      </div>

      {rows.length === 0 ? (
        <p className="text-sm text-gray-400 text-center py-10">
          No incidents match the current filters ✅
        </p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100">
              <TH
                sortable
                active
                asc={sortAsc}
                onClick={() => setSortAsc(v => !v)}
              >
                Started
              </TH>
              <TH>Duration</TH>
              <TH>Peak Severity</TH>
              <TH>Root Cause</TH>
              <TH right>Alerts Sent</TH>
              <TH right>Status</TH>
            </tr>
          </thead>
          <tbody>
            {rows.map(a => {
              const isExpanded = expandedId === a.incidentId
              return (
                <>
                  <tr
                    key={a.incidentId}
                    className="border-b border-gray-100 hover:bg-gray-50 transition-colors cursor-pointer"
                    onClick={() => setExpandedId(isExpanded ? null : a.incidentId)}
                  >
                    <td className="py-2.5 text-xs font-mono text-gray-500">
                      {fmtDateTime(a.openedAtUtc)}
                    </td>
                    <td className="py-2.5 text-gray-700">
                      {a.status === 'Resolved' ? `${a.durationMinutes} min` : `${a.durationMinutes} min (ongoing)`}
                    </td>
                    <td className="py-2.5">
                      {a.peakSeverity === 'Critical'
                        ? <span className="text-red-600 font-medium">🔴 Critical</span>
                        : <span className="text-yellow-600 font-medium">🟡 Warning</span>}
                    </td>
                    <td className="py-2.5 text-gray-600">{rootCauseLabel(a.firstRootCause)}</td>
                    <td className="py-2.5 text-right text-gray-600">{a.alertCount}</td>
                    <td className="py-2.5 text-right">
                      {a.status === 'Resolved'
                        ? <span className="text-green-600 font-medium">✅ Resolved</span>
                        : <span className="text-red-600 font-medium">🔴 Ongoing</span>}
                    </td>
                  </tr>
                  {isExpanded && (
                    <tr key={`${a.incidentId}-expand`} className="bg-gray-50">
                      <td colSpan={6} className="px-4 py-3 text-xs text-gray-600 border-b border-gray-100">
                        <div className="flex flex-wrap gap-x-8 gap-y-2">
                          <div>
                            <p className="text-gray-400 uppercase tracking-wide text-[10px] mb-0.5">Incident ID</p>
                            <p className="font-mono">{a.incidentId}</p>
                          </div>
                          <div>
                            <p className="text-gray-400 uppercase tracking-wide text-[10px] mb-0.5">Opened</p>
                            <p className="font-mono">{fmtDateTime(a.openedAtUtc)}</p>
                          </div>
                          {a.resolvedAtUtc && (
                            <div>
                              <p className="text-gray-400 uppercase tracking-wide text-[10px] mb-0.5">Resolved</p>
                              <p className="font-mono">{fmtDateTime(a.resolvedAtUtc)}</p>
                            </div>
                          )}
                          <div>
                            <p className="text-gray-400 uppercase tracking-wide text-[10px] mb-0.5">Total Alerts</p>
                            <p>{a.alertCount} notification{a.alertCount !== 1 ? 's' : ''} sent</p>
                          </div>
                          <div>
                            <p className="text-gray-400 uppercase tracking-wide text-[10px] mb-0.5">Duration</p>
                            <p>{a.durationMinutes} min{a.status !== 'Resolved' ? ' (still open)' : ''}</p>
                          </div>
                        </div>
                      </td>
                    </tr>
                  )}
                </>
              )
            })}
          </tbody>
        </table>
      )}
    </div>
  )
}
