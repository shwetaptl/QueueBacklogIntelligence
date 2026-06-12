import { useState, useCallback, useMemo, memo } from 'react'
import { useFetch } from '../hooks'
import { useToast } from '../context/ToastContext'
import QueueForm    from '../components/QueueForm'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api'

// ─────────────────────────────────────────────────────────────────────────────
// QueueRow — wrapped in React.memo
//
// React.memo tells React: "only re-render this component if its props changed."
// Without memo: every time Settings re-renders, ALL rows re-render, even
// rows whose data didn't change.
// With memo: React compares old props vs new props — skips rows that haven't changed.
//
// .NET analogy: like caching the rendered output of a partial view and
// only invalidating it when its model changes.
// ─────────────────────────────────────────────────────────────────────────────
const QueueRow = memo(function QueueRow({ queue, onEdit, onDelete, deleting }) {
  return (
    <tr className="border-b border-gray-100 hover:bg-gray-50 transition-colors">
      <td className="py-3 px-4">
        <div className="text-sm font-medium text-gray-800">{queue.queueName}</div>
        <div className="text-xs text-gray-400 font-mono truncate max-w-xs">
          {queue.namespace}
        </div>
      </td>
      <td className="py-3 px-4 text-sm text-gray-600">
        {queue.slaMinutes} min
      </td>
      <td className="py-3 px-4 text-sm">
        {queue.isEnabled
          ? <span className="text-green-600 font-medium">✅ Enabled</span>
          : <span className="text-gray-400">⏸ Disabled</span>
        }
      </td>
      <td className="py-3 px-4 text-sm text-gray-500">
        {queue.warningThreshold * 100}% / {queue.criticalThreshold * 100}%
      </td>
      <td className="py-3 px-4">
        <div className="flex gap-2">
          <button
            onClick={() => onEdit(queue)}
            className="text-xs px-3 py-1 rounded-lg bg-gray-100 text-gray-700
              hover:bg-gray-200 transition-colors font-medium"
          >
            Edit
          </button>
          <button
            onClick={() => onDelete(queue.queueName)}
            disabled={deleting === queue.queueName}
            className="text-xs px-3 py-1 rounded-lg bg-red-50 text-red-600
              hover:bg-red-100 transition-colors font-medium
              disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {deleting === queue.queueName ? 'Deleting…' : 'Delete'}
          </button>
        </div>
      </td>
    </tr>
  )
})

// ─────────────────────────────────────────────────────────────────────────────
// Settings page
// ─────────────────────────────────────────────────────────────────────────────
export default function Settings() {
  const showToast = useToast()

  // Fetch queue list — reuse useFetch with no auto-polling (null interval)
  // We'll manually refresh after mutations
  const queuesR = useFetch(`${API_BASE_URL}/queues`, null)

  // Which queue is being edited (null = add mode when form is open)
  const [editingQueue, setEditingQueue] = useState(null)

  // Is the form visible?
  const [showForm, setShowForm] = useState(false)

  // Which queue is currently being deleted (to show spinner on that row)
  const [deleting, setDeleting] = useState(null)

  // Is the form's save call in-flight?
  const [saving, setSaving] = useState(false)

  // ── useMemo — sorted queue list ──────────────────────────────────────────
  // useMemo memoises the sorted array — only recalculates when queuesR.data changes.
  // Without useMemo: sort() runs on every render even if data didn't change.
  //
  // .NET analogy: like a cached LINQ query — result is reused until input changes.
  const sortedQueues = useMemo(() => {
    if (!queuesR.data) return []
    return [...queuesR.data].sort((a, b) =>
      a.queueName.localeCompare(b.queueName)
    )
  }, [queuesR.data])

  // ── useCallback — stable handler references ──────────────────────────────
  // useCallback prevents these functions from being recreated on every render,
  // which would defeat React.memo on QueueRow (new function reference = new prop
  // = memo thinks something changed = re-renders all rows anyway).

  const handleEdit = useCallback((queue) => {
    setEditingQueue(queue)
    setShowForm(true)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [])

  const handleCancel = useCallback(() => {
    setShowForm(false)
    setEditingQueue(null)
  }, [])

  const handleDelete = useCallback(async (queueName) => {
    if (!window.confirm(`Delete "${queueName}"? This cannot be undone.`)) return

    setDeleting(queueName)
    try {
      const res = await fetch(`${API_BASE_URL}/queues/${queueName}`, {
        method: 'DELETE',
      })
      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        throw new Error(body.error ?? `HTTP ${res.status}`)
      }
      showToast(`Queue "${queueName}" deleted`)
      queuesR.refetch?.()         // refresh list
      window.location.reload()   // fallback — force fresh data
    } catch (e) {
      showToast(e.message, 'error')
    } finally {
      setDeleting(null)
    }
  }, [showToast])

  const handleSave = useCallback(async (formData) => {
    setSaving(true)
    const isEdit = editingQueue !== null
    const url    = isEdit
      ? `${API_BASE_URL}/queues/${editingQueue.queueName}`
      : `${API_BASE_URL}/queues`

    try {
      const res = await fetch(url, {
        method:  isEdit ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(formData),
      })
      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        throw new Error(body.error ?? `HTTP ${res.status}`)
      }
      showToast(
        isEdit
          ? `Queue "${formData.queueName}" updated`
          : `Queue "${formData.queueName}" added`
      )
      setShowForm(false)
      setEditingQueue(null)
      window.location.reload()   // refresh the list
    } catch (e) {
      showToast(e.message, 'error')
    } finally {
      setSaving(false)
    }
  }, [editingQueue, showToast])

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-lg font-bold text-gray-800">Queue Settings</h1>
          <p className="text-xs text-gray-400 mt-0.5">
            Configure which queues QBIS monitors
          </p>
        </div>
        {!showForm && (
          <button
            onClick={() => { setEditingQueue(null); setShowForm(true) }}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium
              rounded-lg hover:bg-blue-700 transition-colors"
          >
            + Add Queue
          </button>
        )}
      </div>

      {/* Add / Edit form — shown conditionally */}
      {showForm && (
        <QueueForm
          queue={editingQueue}
          onSave={handleSave}
          onCancel={handleCancel}
          saving={saving}
        />
      )}

      {/* Queue list */}
      <div className="border rounded-xl bg-white shadow-sm overflow-hidden">
        {queuesR.loading ? (
          <div className="p-8 text-center text-sm text-gray-400">
            Loading queues…
          </div>
        ) : queuesR.error ? (
          <div className="p-8 text-center text-sm text-red-500">
            ⚠ {queuesR.error}
          </div>
        ) : sortedQueues.length === 0 ? (
          <div className="p-8 text-center text-sm text-gray-400">
            No queues configured yet. Click <strong>+ Add Queue</strong> to start.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50">
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  Queue
                </th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  SLA
                </th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  Status
                </th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  Warn / Crit
                </th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody>
              {sortedQueues.map(q => (
                <QueueRow
                  key={q.queueName}
                  queue={q}
                  onEdit={handleEdit}
                  onDelete={handleDelete}
                  deleting={deleting}
                />
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
