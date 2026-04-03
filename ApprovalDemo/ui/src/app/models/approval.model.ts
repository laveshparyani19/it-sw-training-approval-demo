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
