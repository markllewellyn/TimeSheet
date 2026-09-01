// Mirrors TimeSheet.Contracts.MeDto / UserDto (C#) 1:1.

export interface Me {
  id: number;
  displayName: string;
  email: string;
  role: 'Admin' | 'User';
  isProjectManager: boolean;
}

export interface AppUser {
  id: number;
  entraObjectId: string | null;
  isLocalAccount: boolean;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  jobRoleId: number | null;
  jobRoleName: string | null;
  isActive: boolean;
  payrollNumber: string;
}

/// A tenant member found via Entra directory search - FDD: "an administrator can enable or disable any
/// account within the SVG IT tenancy." isProvisioned/appUserId/appIsActive are null/false when nobody in the
/// app's own Staff list has this entraObjectId yet.
export interface TenantDirectoryUser {
  entraObjectId: string;
  displayName: string;
  email: string | null;
  isProvisioned: boolean;
  appUserId: number | null;
  appIsActive: boolean | null;
}
