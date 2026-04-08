import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApprovalRequest, CreateRequestDto, DecisionDto, PagedResult, StudentDirectoryItem } from '../models/approval.model';

@Injectable({
  providedIn: 'root'
})
export class ApprovalService {
  private apiUrl = 'https://it-sw-training-approval-backend.onrender.com/api/approval-requests';
  private studentApiUrl = 'https://it-sw-training-approval-backend.onrender.com/api/students';

  constructor(private http: HttpClient) { }

  getPendingRequests(): Observable<ApprovalRequest[]> {
    return this.http.get<ApprovalRequest[]>(`${this.apiUrl}/pending`);
  }

  createRequest(request: CreateRequestDto): Observable<any> {
    return this.http.post(this.apiUrl, request);
  }

  approveRequest(id: number, decision: DecisionDto): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/approve`, decision);
  }

  rejectRequest(id: number, decision: DecisionDto): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/reject`, decision);
  }

  getRequestById(id: number): Observable<ApprovalRequest> {
    return this.http.get<ApprovalRequest>(`${this.apiUrl}/${id}`);
  }

  getGrades(search = '', limit = 50): Observable<string[]> {
    return this.http.get<string[]>(`${this.studentApiUrl}/grades?search=${encodeURIComponent(search)}&limit=${limit}`);
  }

  getSections(grade = '', search = '', limit = 50): Observable<string[]> {
    return this.http.get<string[]>(
      `${this.studentApiUrl}/sections?grade=${encodeURIComponent(grade)}&search=${encodeURIComponent(search)}&limit=${limit}`
    );
  }

  getStudents(params: {
    grade?: string;
    section?: string;
    search?: string;
    page?: number;
    pageSize?: number;
    onlyActive?: boolean;
  }): Observable<PagedResult<StudentDirectoryItem>> {
    const grade = encodeURIComponent(params.grade ?? '');
    const section = encodeURIComponent(params.section ?? '');
    const search = encodeURIComponent(params.search ?? '');
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 24;
    const onlyActive = params.onlyActive ?? true;

    return this.http.get<PagedResult<StudentDirectoryItem>>(
      `${this.studentApiUrl}/directory?grade=${grade}&section=${section}&search=${search}&page=${page}&pageSize=${pageSize}&onlyActive=${onlyActive}`
    );
  }

  getStudentsByIds(ids: number[]): Observable<StudentDirectoryItem[]> {
    const csv = ids.join(',');
    return this.http.get<StudentDirectoryItem[]>(`${this.studentApiUrl}/by-ids?ids=${encodeURIComponent(csv)}`);
  }
}
