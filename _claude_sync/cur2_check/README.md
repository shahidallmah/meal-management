# Employee Meal Management

A web app (React) with Android and iOS apps (Capacitor), backed by a .NET 8 Web API and SQL Server.

```
MealManagement/
├─ database/            SQL Server scripts (run in order)
│   ├─ 01_Schema.sql
│   └─ 02_Seed.sql
├─ backend/             .NET 8 Web API (open MealManagement.sln in Visual Studio 2022)
│   └─ MealManagement.Api/
│       ├─ Controllers/  REST endpoints
│       ├─ Services/     business rules (meal calendar, auto-ON job, reports, auth)
│       ├─ Data/         EF Core entities + DbContext (database-first, matches 01_Schema.sql)
│       └─ Dtos/
├─ frontend/            React + TypeScript (Vite) — also the mobile app source
│   ├─ src/
│   ├─ android/         Android Studio project (Capacitor)
│   └─ ios/             Xcode project (Capacitor)
└─ docs/theme-preview.html
```

---

## 1. Database

1. In SSMS: `CREATE DATABASE MealManagement;`
2. Run `database/01_Schema.sql`, then `database/02_Seed.sql` against it.
3. **Existing database?** Run `database/04_CompanyLogo.sql` and `database/05_PasswordSetupCode.sql`. Both are safe to re-run, and `01_Schema.sql` / `02_Seed.sql` already include them for fresh installs.
   Then upload the logo in **Admin → Settings → Company logo**.

Seed data: settings, meal types (Lunch = regular; Dinner/Iftar = occasional), sample departments and designations.

## 2. API

1. Open `backend/MealManagement.sln` in Visual Studio 2022 (.NET 8 SDK).
2. Edit `appsettings.json`:
   - `ConnectionStrings:MealDb`: your SQL Server.
   - `Jwt:Key`: replace it with a long random secret (at least 32 characters).
   - `SeedAdmin`: the first admin login. It's created on first start if no admin exists.
3. Run (F5). Swagger opens at `http://localhost:5080/swagger`.

**First login:** mobile `01700000000`, password `Admin@123` (from `SeedAdmin`). Change it on the Profile page.

## 3. Web app

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173  (proxies /api to http://localhost:5080)
```

**Production, single site:** `npm run build:api` builds into `backend/MealManagement.Api/wwwroot`, so publishing the API serves the web app too (IIS or Kestrel).

## 4. Mobile apps (Android & iOS)

The apps bundle the React build and call the API over the network.

1. Create `frontend/.env.production` with the public API address:
   `VITE_API_URL=https://meal.yourcompany.com`
   Use HTTPS. Android blocks plain-HTTP calls by default.
2. Add the address's origin to `Cors:Origins` in the API only if it differs. The Capacitor origins
   (`https://localhost`, `capacitor://localhost`) are already allowed.
3. Build and sync:
   ```bash
   cd frontend
   npm run cap:sync
   npx cap open android     # Android Studio → Build → Generate Signed Bundle (AAB) for Play Store
   npx cap open ios         # on a Mac with Xcode → Product → Archive for App Store
   ```
4. Change `appId` in `capacitor.config.ts` (currently `com.company.mealmanagement`) to your own bundle id
   before the first store upload. App icons and splash screens can be generated with `npx @capacitor/assets generate`.

---

## Business rules implemented

| Rule | Where |
|---|---|
| Employees: Code, Name, Department, Designation, Mobile No (all mandatory), Status Active/InActive | Admin → Employees |
| Login with mobile no + password. After the admin registers an employee, the employee sets a password with a one-time code from the admin | Sign-in → "First time or forgot password? Use a code" |
| **Default eligible** employees: eligibility period with effective date and optional expire date. The system marks the regular meal (Lunch) ON automatically on every working day in the period | Employees → Eligibility; background job every 30 min |
| Other employees turn their own meal ON/OFF | My Meals |
| Changes allowed only for the next **N days** (setting, default 7). Each day locks at a **cut-off time** on the previous day (setting, default 17:00). Today and past days are locked | Settings |
| Option: default-eligible employees may or may not opt out | Settings → "Default-eligible employees can turn their meal OFF" |
| Admin can override anyone's meal for any date (bulk or single) | Meal Board |
| No regular meal on weekly off days (default Friday) and holidays | Settings, Holidays |
| Occasional meals (Dinner, Iftar …) on specific dates: auto-ON for all employees, or opt-in | Meals & Occasions |
| Daily cost per meal = **employee portion + company portion**. Set per day, or for a whole period. Days without an entry use the meal type's default cost | Daily Cost |
| Reports with Excel/CSV download: daily summary, employee statement (for payroll deduction), department summary, meal list for a date (kitchen), employee detail | Reports |
| Audit trail of every ON/OFF change (who, when, old to new) | `MealEntryLog` table |

### How meal status is decided

For an employee, date and meal type:

1. If a row exists in `MealEntry`, it decides (the employee's or the admin's choice, or an automatic entry).
2. Otherwise the default applies: ON if the employee is Active, the meal is served that day, and (regular meal) an eligibility period covers the date, or (occasion) the occasion is set to "all employees ON".

The background job writes the default ON rows for today through the booking window (plus a 7-day back-fill), so reports read straight from `MealEntry`. When an admin changes eligibility, status, holidays, occasions or settings, future dates are recalculated from **tomorrow onward**. Today and past days are never changed automatically.

## API overview

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/login`, `POST /api/auth/check-mobile`, `POST /api/auth/set-password` (one-time code), `GET /api/auth/me`, `POST /api/auth/change-password` |
| Employee self-service | `GET/PUT /api/my/meals`, `GET /api/my/history?from&to` |
| Employees (admin) | `GET/POST /api/employees`, `GET/PUT /api/employees/{id}`, `POST /api/employees/{id}/setup-code`, `POST /api/employees/{id}/eligibilities`, `PUT/DELETE /api/employees/eligibilities/{id}`, `POST /api/employees/eligibilities/bulk` |
| Meals (admin) | `GET /api/meal-board?date&mealTypeId`, `POST /api/meal-board/set` |
| Costs (admin) | `GET /api/costs?from&to`, `PUT /api/costs`, `POST /api/costs/bulk`, `DELETE /api/costs/{id}` |
| Setup (admin) | `/api/meal-types`, `/api/occasions`, `/api/holidays`, `/api/departments`, `/api/designations`, `/api/settings` |
| Dashboard & reports (admin) | `GET /api/dashboard`, `GET /api/reports`, `GET /api/reports/{code}`, `GET /api/reports/{code}/export?format=xlsx\|csv` |

## Suggested next steps

- **OTP on password creation/reset.** Right now, identity is checked with mobile no + employee code. If you have an SMS gateway, add an OTP step to `AuthService.CreatePasswordAsync`.
- **Bulk employee import** from Excel for the initial load.
- **Push reminders** before the cut-off, via `@capacitor/push-notifications`.

---

## Sign-in, first password and forgotten passwords

Mobile numbers and employee codes are known to colleagues, so they are **not** enough to set a password.
Employees need a **one-time code** issued by the admin:

1. **New employee:** when the admin registers an employee, an *activation code* is shown once.
   **Forgot password:** the admin opens **Employees** and clicks **Reset code** (or **Activation code**) on that row.
2. The admin gives the code to the employee **in person or by phone call**. *Copy instructions* copies a ready-made message.
3. The employee taps **First time or forgot password? Use a code** on the sign-in page, then enters mobile no + code + new password.

Rules:
- Each code works once and expires after *Settings → One-time code valid for* (default 24 h).
- A new code cancels the previous one, and 5 wrong tries block the code.
- Only a hash of the code is stored.
- An employee's current password keeps working until they use a reset code. To block access immediately, set the employee to **InActive**.
- Setting or changing a password signs out the employee's other devices. Changing an employee's role or status takes effect within a minute.
- Sign-in and set-password are limited to 10 attempts per minute per IP address.
