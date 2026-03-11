export interface EmployeeProfile {
  empNo: string | null;
  ebsEmployeeNumber: string | null;

  employeeName: string | null;
  employeeArabicName: string | null;
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  title: string | null;

  emailAddress: string | null;

  position: string | null;
  positionName: string | null;
  job: string | null;
  grade: string | null;

  orgUnit: string | null;
  section: string | null;
  department: string | null;
  division: string | null;
  sector: string | null;
  college: string | null;

  vpName: string | null;
  svpName: string | null;

  supervisorName: string | null;

  nationality: string | null;
  gender: string | null;

  originalHireDate: string | null;
  hireDate: string;
  serviceYrs: number | null;
  contractType: string | null;
  employeeCategory: string | null;
  personType: string | null;
}
