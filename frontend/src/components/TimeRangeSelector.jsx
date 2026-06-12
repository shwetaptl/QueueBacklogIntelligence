const PRESETS = ['15m', '30m', '1h', '6h', '24h', '7d', 'custom']

function todayISO() {
  return new Date().toISOString().slice(0, 10)
}

// value: { preset: string } | { preset: 'custom', from: string, to: string }
export default function TimeRangeSelector({ value, onChange }) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      {PRESETS.map(p => (
        <button
          key={p}
          type="button"
          onClick={() => onChange(
            p === 'custom'
              ? { preset: 'custom', from: todayISO(), to: todayISO() }
              : { preset: p }
          )}
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
            value={value.from || ''}
            onChange={e => onChange({ ...value, from: e.target.value })}
            className="text-xs border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
          <span className="text-xs text-gray-400">to</span>
          <input
            type="date"
            value={value.to || ''}
            onChange={e => onChange({ ...value, to: e.target.value })}
            className="text-xs border border-gray-200 rounded-lg px-2 py-1 outline-none focus:ring-2 focus:ring-blue-400"
          />
        </>
      )}
    </div>
  )
}
