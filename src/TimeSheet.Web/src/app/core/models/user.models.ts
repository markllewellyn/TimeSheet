// Mirrors TimeSheet.Contracts.MeDto / UserDto (C#) 1:1.

export interface Me {
  id: number;
  displayName: string;
  email: string;
  role: 'Admin' | 'User';
}

export interface AppUser {
  id: number;
  entraObjectId: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  jobTitle: string | null;
  isActive: boolean;
}
