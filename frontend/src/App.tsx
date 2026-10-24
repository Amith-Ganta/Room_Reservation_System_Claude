import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { RoomsPage } from './pages/RoomsPage'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/rooms" element={<RoomsPage />} />
        <Route path="*" element={<Navigate to="/rooms" replace />} />
      </Route>
    </Routes>
  )
}
