// Mirrors TimeSheet.Contracts.MeDto / UserDto (C#) 1:1.

export interface Me {
  id: number;
  displayName: string;
  email: string;
  role: 'Admin' | 'User';
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
