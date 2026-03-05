export interface UserListItem {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  isADUser: boolean;
  isActive: boolean;
  roles: string[];
  createdAt: string; // ISO date string
}

export interface UserListResponse {
  users: UserListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApiResponse<T> {
  data: T;
  success?: boolean;
  message?: string;
}

export interface RegisterExternalUserRequest {
  userName: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  password: string;
  roleName: string;
}

export interface RegisterADUserRequest {
  email: string;
  fullName: string;
  phoneNumber?: string;
  employeeId?: string;
  department?: string;
  jobTitle?: string;
  roleName: string;
}

export interface RegisterUserResponse {
  userId: string;
  userName: string;
  email: string;
  fullName: string;
  roles: string[];
  isADUser: boolean;
  createdAt: string;
}

export interface EmployeeSearchResult {
  employeeId: string;
  empNo?: string;
  ebsEmployeeNumber?: string;
  fullName: string;
  employeeArabicName?: string;
  email: string;
  department?: string;
  jobTitle?: string;
  phoneNumber?: string;
  alreadyRegistered: boolean;
}

export interface UpdateUserRoleRequest {
  userId: string;
  roleName: string;
}
