// Mirrors backend/MealManagement.Api/Dtos/Dtos.cs (JSON camelCase). Dates are "yyyy-MM-dd".

export type Role = 'Employee' | 'Admin';
export type Status = 'Active' | 'Inactive';
export type Source = 'Auto' | 'Employee' | 'Admin';

export interface User {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  mobileNo: string;
  role: Role;
  department: string;
  designation: string;
  email?: string | null;
}

export interface AuthResponse { token: string; expiresAt: string; user: User; }
export interface CheckMobileResponse { registered: boolean; hasPassword: boolean; isActive: boolean; }
export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number; }
export interface NamedItem { id: number; name: string; isActive: boolean; }

export interface MyMealItem {
  mealTypeId: number;
  mealTypeName: string;
  isRegular: boolean;
  isServed: boolean;
  notServedReason?: string | null;
  isOn: boolean;
  isEligible: boolean;
  /** Within the booking window and before the cut-off. */
  isOpen: boolean;
  /** isOpen, and the change is allowed (e.g. default meals may not be turned off). */
  canChange: boolean;
  source?: Source | null;
  lockTime: string;
  occasionTitle?: string | null;
  employeeCost: number;
  totalCost: number;
}
export interface MyMealDay { date: string; dayName: string; isToday: boolean; holiday?: string | null; meals: MyMealItem[]; }
export interface MyMeals { today: string; bookingWindowDays: number; cutoffTime: string; allowEligibleOptOut: boolean; days: MyMealDay[]; }

export interface MealHistoryItem {
  mealDate: string; mealTypeId: number; mealTypeName: string; source: Source;
  totalCost: number; employeeCost: number; companyCost: number;
}
export interface MealHistory {
  from: string; to: string; mealCount: number; totalCost: number; employeeCost: number; companyCost: number;
  items: MealHistoryItem[];
}

export interface Employee {
  employeeId: number; employeeCode: string; employeeName: string;
  departmentId: number; department: string; designationId: number; designation: string;
  mobileNo: string; email?: string | null; status: Status; role: Role; hasPassword: boolean;
  isDefaultEligible: boolean; lastLoginAt?: string | null;
}
export interface Eligibility {
  mealEligibilityId: number; mealTypeId: number; mealTypeName: string;
  effectiveDate: string; expireDate?: string | null; remarks?: string | null; isCurrent: boolean;
}
export interface EmployeeDetail { employee: Employee; eligibilities: Eligibility[]; }
export interface EmployeeSave {
  employeeCode: string; employeeName: string; departmentId: number; designationId: number;
  mobileNo: string; email?: string | null; status: Status; role: Role;
  defaultEligible?: boolean; eligibleFrom?: string | null; eligibleTo?: string | null;
}

export interface MealBoardRow {
  employeeId: number; employeeCode: string; employeeName: string; department: string; designation: string;
  mobileNo: string; isEligible: boolean; isOn: boolean; source?: Source | null; updatedAt?: string | null;
}
export interface MealBoard {
  date: string; mealTypeId: number; mealTypeName: string; isServed: boolean; notServedReason?: string | null;
  isOpenForEmployees: boolean; lockTime: string; onCount: number; offCount: number;
  costPerMeal: number; employeeCostPerMeal: number; totalCost: number; rows: MealBoardRow[];
}

export interface MealCount {
  date: string; mealTypeId: number; mealTypeName: string; isServed: boolean; notServedReason?: string | null;
  onCount: number; costPerMeal: number; employeeCostPerMeal: number; totalCost: number;
  employeeTotal: number; companyTotal: number; occasionTitle?: string | null;
}

export interface MealType {
  mealTypeId: number; code: string; name: string; isRegular: boolean;
  defaultTotalCost: number; defaultEmployeeCost: number; sortOrder: number; isActive: boolean;
}
export interface Occasion {
  mealOccasionId: number; mealTypeId: number; mealTypeName: string; occasionDate: string;
  title: string; autoOnForAll: boolean; isActive: boolean;
}
export interface Holiday { holidayId: number; holidayDate: string; dayName: string; description: string; }

export interface DailyCost {
  mealDate: string; dayName: string; mealTypeId: number; mealTypeName: string; isServed: boolean;
  notServedReason?: string | null; mealDailyCostId?: number | null; totalCost: number; employeeCost: number;
  companyCost: number; isDefault: boolean; remarks?: string | null; mealCount: number;
  dayTotal: number; dayEmployeeTotal: number; dayCompanyTotal: number;
}

export interface Setting { key: string; value: string; description?: string | null; }

export interface Dashboard {
  today: string;
  todayMeals: MealCount[];
  tomorrowMeals: MealCount[];
  month: { from: string; to: string; meals: number; totalCost: number; employeeTotal: number; companyTotal: number };
  employees: { active: number; inactive: number; defaultEligible: number; withoutPassword: number };
  trend: { date: string; meals: number; totalCost: number }[];
}

export interface ReportDefinition { code: string; name: string; description: string; needsDateRange: boolean; needsSingleDate: boolean; }
export interface ReportColumn { key: string; header: string; type: 'text' | 'number' | 'money' | 'date'; }
export interface ReportTable {
  code: string; title: string; subtitle: string; columns: ReportColumn[];
  rows: Record<string, unknown>[]; totals: Record<string, unknown>;
}
