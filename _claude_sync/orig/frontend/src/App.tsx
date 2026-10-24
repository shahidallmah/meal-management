import type { ReactNode } from 'react';
import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { ConfirmProvider, Spinner, ToastProvider } from './components/ui';
import AppShell from './components/AppShell';
import Login from './pages/Login';
import CreatePassword from './pages/CreatePassword';
import MyMeals from './pages/MyMeals';
import MealHistoryPage from './pages/History';
import Profile from './pages/Profile';
import Dashboard from './pages/admin/Dashboard';
import Employees from './pages/admin/Employees';
import MealBoardPage from './pages/admin/MealBoard';
import Costs from './pages/admin/Costs';
import MealTypes from './pages/admin/MealTypes';
import Holidays from './pages/admin/Holidays';
import MasterData from './pages/admin/MasterData';
import Reports from './pages/admin/Reports';
import SettingsPage from './pages/admin/Settings';

function RequireAuth({ children, admin }: { children: ReactNode; admin?: boolean }) {
  const { user, isAdmin, ready } = useAuth();
  if (!ready) return <Spinner />;
  if (!user) return <Navigate to="/login" replace />;
  if (admin && !isAdmin) return <Navigate to="/meals" replace />;
  return <>{children}</>;
}

function Home() {
  const { user, isAdmin, ready } = useAuth();
  if (!ready) return <Spinner />;
  if (!user) return <Navigate to="/login" replace />;
  return <Navigate to={isAdmin ? '/admin' : '/meals'} replace />;
}

// HashRouter works identically in the browser, under IIS and inside the Capacitor mobile apps.
export default function App() {
  return (
    <ToastProvider>
      <ConfirmProvider>
        <AuthProvider>
          <HashRouter>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route path="/create-password" element={<CreatePassword />} />
              <Route element={<RequireAuth><AppShell /></RequireAuth>}>
                <Route path="/meals" element={<MyMeals />} />
                <Route path="/history" element={<MealHistoryPage />} />
                <Route path="/profile" element={<Profile />} />
                <Route path="/admin" element={<RequireAuth admin><Dashboard /></RequireAuth>} />
                <Route path="/admin/meal-board" element={<RequireAuth admin><MealBoardPage /></RequireAuth>} />
                <Route path="/admin/employees" element={<RequireAuth admin><Employees /></RequireAuth>} />
                <Route path="/admin/costs" element={<RequireAuth admin><Costs /></RequireAuth>} />
                <Route path="/admin/meal-types" element={<RequireAuth admin><MealTypes /></RequireAuth>} />
                <Route path="/admin/holidays" element={<RequireAuth admin><Holidays /></RequireAuth>} />
                <Route path="/admin/master-data" element={<RequireAuth admin><MasterData /></RequireAuth>} />
                <Route path="/admin/reports" element={<RequireAuth admin><Reports /></RequireAuth>} />
                <Route path="/admin/settings" element={<RequireAuth admin><SettingsPage /></RequireAuth>} />
              </Route>
              <Route path="*" element={<Home />} />
            </Routes>
          </HashRouter>
        </AuthProvider>
      </ConfirmProvider>
    </ToastProvider>
  );
}
