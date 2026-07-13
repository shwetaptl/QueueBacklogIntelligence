import { useState } from 'react'

const PRESETS = ['15m', '30m', '1h', '6h', '24h', '7d', 'custom']

function todayISO() {
  return new Date().toISOString().slice(0, 10)
}

// value: { preset: string } | { preset: 'custom', from: string, to: string }
export default function TimeRangeSelector({ value, onChange }) {
  const today = todayISO()
  // Local draft — only meaningful when preset === 'custom'
  const [draftFrom, setDraftFrom] = useState(value.from || today)
  const [draftTo,   setDraftTo]   = useState(value.to   || today)

  function handlePreset(p) {
    if (p === 'custom') {
      // Show pickers pre-filled with today but DON'T fire a fetch yet
      setDraftFrom(today)
      setDraftTo(today)
      onChange({ preset: 'custom', from: '', to: '' })
    } else {
      onChange({ preset: p })
    }
  }

  function handleApply() {
    if (draftFrom && draftTo) {
      onChange({ preset: 'custom', from: draftFrom, to: draftTo })
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      {PRESETS.map(p => (
        <button
          key={p}
          type="button"
          onClick={() => handlePreset(p)}
          className={`px-3 py-1 text-xs font-medium rounded-lg border transition-colors
            ${value.preset === p
              ? 'bg-blue-600 text-white border-blue-600'
              : 'bg-white text-gray-600 border-gray-200 hover:border-blue-400 hover:text-blue-600'
            }`}
        >
          {p === 'custom' ? 'Custom' : p}
        </button>
      ))}

      {value.preset === 'custom' && (
        <>
          <input
            type="date"
            value={draftFrom}
            onChange={e => setDraftFrom(e.target.value)}
            className="text-xs border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <span className="text-xs text-gray-400">to</span>
          <input
            type="date"
            value={draftTo}
            onChange={e => setDraftTo(e.target.value)}
            className="text-xs border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <button
            type="button"
            onClick={handleApply}
            disabled={!draftFrom || !draftTo}
            className="px-3 py-1 text-xs font-medium rounded-lg bg-blue-600 text-white
              hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Apply
          </button>
        </>
      )}
    </div>
  )
}
