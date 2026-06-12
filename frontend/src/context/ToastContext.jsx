import { createContext, useContext, useState, useCallback, useEffect } from 'react'

// ── 1. Create the context ──────────────────────────────────────────────────
// This is like defining a static property on a service locator.
// Any component below ToastProvider can read this context.
const ToastContext = createContext(null)

// ── 2. Provider — wraps the whole app, owns the toast state ───────────────
export function ToastProvider({ children }) {
  const [toast, setToast] = useState(null)

  // showToast is the function consumers call: showToast('Saved!', 'success')
  const showToast = useCallback((message, type = 'success') => {
    setToast({ message, type, id: Date.now() })
  }, [])

  // Auto-dismiss after 3 seconds whenever a new toast appears
  useEffect(() => {
    if (!toast) return
    const id = setTimeout(() => setToast(null), 3000)
    return () => clearTimeout(id)
  }, [toast])

  return (
    <ToastContext.Provider value={showToast}>
      {children}

      {/* Toast notification — fixed bottom-right, above everything */}
      {toast && (
        <div
          key={toast.id}
          className={`fixed bottom-6 right-6 z-50 px-5 py-3 rounded-xl text-white
            text-sm font-medium shadow-xl animate-pulse-once
            ${toast.type === 'success' ? 'bg-green-600' : 'bg-red-600'}`}
        >
          {toast.type === 'success' ? '✅' : '❌'} {toast.message}
        </div>
      )}
    </ToastContext.Provider>
  )
}

// ── 3. Custom hook — the clean way to consume this context ─────────────────
// Any component calls: const showToast = useToast()
export function useToast() {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>')
  return ctx
}
