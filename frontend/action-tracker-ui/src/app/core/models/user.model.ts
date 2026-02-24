export interface TeamMember {
  id: string;
  fullName: string;
  role: string;
  department: string;
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  firstName: string;
  lastName: string;
  role: string;
  department: string;
  isActive: boolean;
  createdAt: string;
}
