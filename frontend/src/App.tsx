import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { RoomsPage } from './pages/RoomsPage'
import { UsersPage } from './pages/UsersPage'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/rooms" element={<RoomsPage />} />
        <Route path="/users" element={<UsersPage />} />
        <Route path="*" element={<Navigate to="/rooms" replace />} />
      </Route>
    </Routes>
  )
}
