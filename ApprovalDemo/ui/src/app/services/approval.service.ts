import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApprovalRequest, CreateRequestDto, DecisionDto, PagedResult, StaffDirectoryItem, StudentDirectoryItem } from '../models/approval.model';

@Injectable({
  providedIn: 'root'
})
export class ApprovalService {
  private apiUrl = 'https://it-sw-training-approval-backend.onrender.com/api/approval-requests';
  private studentApiUrl = 'https://it-sw-training-approval-backend.onrender.com/api/students';
  private staffApiUrl = 'https://it-sw-training-approval-backend.onrender.com/api/staff';

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

  getGrades(search = '%', limit = 50): Observable<string[]> {
    const effectiveSearch = search && search.trim().length > 0 ? search : '%';
    return this.http.get<string[]>(`${this.studentApiUrl}/grades?search=${encodeURIComponent(effectiveSearch)}&limit=${limit}`);
  }

  getSections(grades: string[] = [], search = '%', limit = 50): Observable<string[]> {
    const effectiveSearch = search && search.trim().length > 0 ? search : '%';
    const gradesCsv = encodeURIComponent(grades.join(','));
    return this.http.get<string[]>(
      `${this.studentApiUrl}/sections?grades=${gradesCsv}&search=${encodeURIComponent(effectiveSearch)}&limit=${limit}`
    );
  }

  getStudents(params: {
    grades?: string[];
    sections?: string[];
    search?: string;
    page?: number;
    pageSize?: number;
    onlyActive?: boolean;
  }): Observable<PagedResult<StudentDirectoryItem>> {
    const grades = encodeURIComponent((params.grades ?? []).join(','));
    const sections = encodeURIComponent((params.sections ?? []).join(','));
    const effectiveSearch = params.search && params.search.trim().length > 0 ? params.search : '%';
    const search = encodeURIComponent(effectiveSearch);
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 24;
    const onlyActive = params.onlyActive ?? true;

    return this.http.get<PagedResult<StudentDirectoryItem>>(
      `${this.studentApiUrl}/directory?grades=${grades}&sections=${sections}&search=${search}&page=${page}&pageSize=${pageSize}&onlyActive=${onlyActive}`
    );
  }

  getStudentsByIds(ids: number[]): Observable<StudentDirectoryItem[]> {
    const csv = ids.join(',');
    return this.http.get<StudentDirectoryItem[]>(`${this.studentApiUrl}/by-ids?ids=${encodeURIComponent(csv)}`);
  }

  getDepartments(search = '%', limit = 50): Observable<string[]> {
    const effectiveSearch = search && search.trim().length > 0 ? search : '%';
    return this.http.get<string[]>(`${this.staffApiUrl}/departments?search=${encodeURIComponent(effectiveSearch)}&limit=${limit}`);
  }

  getTeams(departments: string[] = [], search = '%', limit = 50): Observable<string[]> {
    const effectiveSearch = search && search.trim().length > 0 ? search : '%';
    const departmentsCsv = encodeURIComponent(departments.join(','));
    return this.http.get<string[]>(
      `${this.staffApiUrl}/teams?departments=${departmentsCsv}&search=${encodeURIComponent(effectiveSearch)}&limit=${limit}`
    );
  }

  getStaff(params: {
    departments?: string[];
    teams?: string[];
    search?: string;
    page?: number;
    pageSize?: number;
    onlyActive?: boolean;
    excludeSystemAccounts?: boolean;
  }): Observable<PagedResult<StaffDirectoryItem>> {
    const departments = encodeURIComponent((params.departments ?? []).join(','));
    const teams = encodeURIComponent((params.teams ?? []).join(','));
    const effectiveSearch = params.search && params.search.trim().length > 0 ? params.search : '%';
    const search = encodeURIComponent(effectiveSearch);
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 24;
    const onlyActive = params.onlyActive ?? true;
    const excludeSystemAccounts = params.excludeSystemAccounts ?? true;

    return this.http.get<PagedResult<StaffDirectoryItem>>(
      `${this.staffApiUrl}/directory?departments=${departments}&teams=${teams}&search=${search}&page=${page}&pageSize=${pageSize}&onlyActive=${onlyActive}&excludeSystemAccounts=${excludeSystemAccounts}`
    );
  }

  getStaffByIds(ids: number[], excludeSystemAccounts = true): Observable<StaffDirectoryItem[]> {
    const csv = ids.join(',');
    return this.http.get<StaffDirectoryItem[]>(
      `${this.staffApiUrl}/by-ids?ids=${encodeURIComponent(csv)}&excludeSystemAccounts=${excludeSystemAccounts}`
    );
  }
}
