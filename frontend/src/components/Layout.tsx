import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material'
import { NavLink, Outlet } from 'react-router-dom'

/** Pages that exist so far; each later spec adds its own link. */
const NAV_ITEMS = [
  { to: '/rooms', label: 'Rooms' },
  { to: '/users', label: 'Users' },
  { to: '/reservations', label: 'Reservations' },
  { to: '/calendar', label: 'Calendar' },
]

export function Layout() {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ mr: 4 }}>
            Room Reservation
          </Typography>
          <Box component="nav" sx={{ display: 'flex', gap: 1 }}>
            {NAV_ITEMS.map((item) => (
              <Button
                key={item.to}
                component={NavLink}
                to={item.to}
                color="inherit"
                sx={{ '&.active': { bgcolor: 'rgba(255, 255, 255, 0.18)' } }}
              >
                {item.label}
              </Button>
            ))}
          </Box>
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <Outlet />
      </Container>
    </Box>
  )
}
