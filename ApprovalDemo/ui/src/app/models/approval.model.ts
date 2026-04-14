export interface ApprovalRequest {
  id: number;
  title: string;
  requestedBy: string;
  status: number; // 0=Pending, 1=Approved, 2=Rejected
  createdAt: string;
  decisionBy?: string;
  decisionAt?: string;
  rejectReason?: string;
}

export interface CreateRequestDto {
  title: string;
  requestedBy: string;
}

export interface DecisionDto {
  decisionBy: string;
  rejectReason?: string;
}

export interface StudentDirectoryItem {
  id: number;
  studentCode: string;
  fullName: string;
  gradeName: string;
  sectionName: string;
  photoUrl?: string;
  isActive: boolean;
}

export interface StaffDirectoryItem {
  id: number;
  staffCode: string;
  fullName: string;
  departmentName: string;
  teamName: string;
  designation: string;
  photoUrl?: string;
  isActive: boolean;
  isSystemAccount: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TeamOptionItem {
  departmentName: string;
  teamName: string;
  displayLabel: string;
}

export interface TlTeamAssignmentItem {
  id: string;
  tlStaffCode: string;
  departmentName: string;
  teamName: string;
  memberStaffIds: number[];
  taskDescription?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTlTeamAssignmentDto {
  tlStaffCode: string;
  departmentName: string;
  teamName: string;
  memberStaffIds: number[];
  taskDescription?: string;
}

export interface Task8ReportResponse {
  reportId: number;
  title: string;
  columns: string[];
  rows: Record<string, string>[];
  totalCount: number;
  page: number;
  pageSize: number;
  dataSourceNote?: string | null;
}
