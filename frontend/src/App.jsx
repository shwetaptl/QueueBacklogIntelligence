import { Routes, Route } from 'react-router-dom'
import { ToastProvider } from './context/ToastContext'
import Sidebar     from './components/Sidebar'
import Overview    from './pages/Overview'
import QueueDetail from './pages/QueueDetail'
import Settings    from './pages/Settings'

export default function App() {
  return (
    <ToastProvider>
      <div className="flex h-screen overflow-hidden bg-gray-50">
        <Sidebar />
        <main className="flex-1 overflow-y-auto">
          <div className="max-w-5xl mx-auto px-6 py-6">
            <Routes>
              <Route path="/"             element={<Overview />}    />
              <Route path="/queues/:name" element={<QueueDetail />} />
              <Route path="/settings"     element={<Settings />}    />
            </Routes>
          </div>
        </main>
      </div>
    </ToastProvider>
  )
}
