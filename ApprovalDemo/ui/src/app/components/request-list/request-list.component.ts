import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApprovalService } from '../../services/approval.service';
import { ApprovalRequest, StudentDirectoryItem } from '../../models/approval.model';

@Component({
  selector: 'app-request-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="page-shell">
      <div class="aurora"></div>
      <div class="grain"></div>

      <div class="dashboard-shell">
        <header class="dashboard-header">
          <div class="brand-wrap">
            <span class="brand-dot"></span>
            <h1 class="brand-title">IT SW Training manual</h1>
          </div>

          <div class="header-actions">
            <div class="stat-chip">
              <span class="stat-value">{{ requests.length }}</span>
              <span class="stat-label">Open</span>
            </div>
            <button class="btn btn-refresh" (click)="loadRequests()" [disabled]="loading">
              {{ loading ? 'Refreshing...' : 'Refresh List' }}
            </button>
          </div>
        </header>

        <div class="dashboard-body">
          <aside class="sidebar" aria-label="Sidebar Navigation">
            <p class="sidebar-label">Training Tasks</p>
            <button class="sidebar-link" [class.active]="activeTask === 'task2'" (click)="switchTask('task2')">Task 2</button>
            <button class="sidebar-link" [class.active]="activeTask === 'task9'" (click)="switchTask('task9')">Task 9</button>
          </aside>

          <main class="content-panel">
            <header class="content-header" *ngIf="activeTask === 'task2'">
              <p class="eyebrow">Operations Console</p>
              <p class="subtitle">Track, review, and resolve incoming requests quickly.</p>
            </header>

            <header class="content-header" *ngIf="activeTask === 'task9'">
              <p class="eyebrow">Student Observation</p>
              <p class="subtitle">Select active students by grade/section or direct search, then review selected profiles.</p>
            </header>

            <ng-container *ngIf="activeTask === 'task2'">
            <div *ngIf="!loading" class="table-controls">
              <div class="search-box">
                <input
                  type="text"
                  [(ngModel)]="searchTerm"
                  (ngModelChange)="onSearchChange()"
                  placeholder="Search by title, requester, or ID"
                  aria-label="Search approval requests"
                />
              </div>

              <div class="control-right">
                <label for="page-size">Rows</label>
                <select id="page-size" [(ngModel)]="pageSize" (ngModelChange)="onPageSizeChange()">
                  <option *ngFor="let size of pageSizeOptions" [ngValue]="size">{{ size }}</option>
                </select>
              </div>
            </div>

            <div *ngIf="loading" class="loader-box">
              <div class="spinner"></div>
              <p>Loading requests from API...</p>
            </div>

            <div *ngIf="!loading" class="table-wrap">
              <table class="approval-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Title</th>
                    <th>Requested By</th>
                    <th>Created At</th>
                    <th class="actions-col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let request of paginatedRequests">
                    <td><span class="id-pill">#{{ request.id }}</span></td>
                    <td class="title-cell">{{ request.title }}</td>
                    <td>{{ request.requestedBy }}</td>
                    <td>{{ request.createdAt | date:'medium' }}</td>
                    <td>
                      <div class="action-group">
                        <button class="btn btn-approve" (click)="approve(request.id)">Approve</button>
                        <button class="btn btn-reject" (click)="selectForRejection(request.id)">Reject</button>
                      </div>
                    </td>
                  </tr>
                  <tr *ngIf="filteredRequests.length === 0">
                    <td colspan="5" class="no-data">No matching requests found. Try a different search.</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div *ngIf="!loading && filteredRequests.length > 0" class="pagination-row">
              <p class="page-summary">
                Showing {{ pageStart }}-{{ pageEnd }} of {{ filteredRequests.length }}
              </p>

              <div class="page-actions">
                <button class="btn btn-page" (click)="goToPreviousPage()" [disabled]="currentPage === 1">Previous</button>
                <span class="page-indicator">Page {{ currentPage }} / {{ totalPages }}</span>
                <button class="btn btn-page" (click)="goToNextPage()" [disabled]="currentPage === totalPages">Next</button>
              </div>
            </div>
            </ng-container>

            <ng-container *ngIf="activeTask === 'task9'">
              <div class="student-filters">
                <div class="student-filter-item">
                  <label for="grade-select">Grade</label>
                  <select id="grade-select" [(ngModel)]="selectedGrade" (ngModelChange)="onGradeChanged()">
                    <option value="">All Grades</option>
                    <option *ngFor="let grade of gradeOptions" [value]="grade">{{ grade }}</option>
                  </select>
                </div>

                <div class="student-filter-item">
                  <label for="section-select">Section</label>
                  <select id="section-select" [(ngModel)]="selectedSection" (ngModelChange)="reloadStudentsFromStart()">
                    <option value="">All Sections</option>
                    <option *ngFor="let section of sectionOptions" [value]="section">{{ section }}</option>
                  </select>
                </div>

                <div class="student-search-box">
                  <label for="student-search">Search Student</label>
                  <input
                    id="student-search"
                    type="text"
                    [(ngModel)]="studentSearch"
                    (ngModelChange)="onStudentSearchChanged()"
                    placeholder="Name or student code"
                    aria-label="Search students"
                  />
                </div>
              </div>

              <div *ngIf="studentLoading" class="loader-box">
                <div class="spinner"></div>
                <p>Loading students...</p>
              </div>

              <div *ngIf="!studentLoading" class="student-grid">
                <article
                  *ngFor="let student of studentResults"
                  class="student-card"
                  [class.selected]="isSelected(student.id)"
                  (click)="toggleStudent(student)">
                  <img [src]="student.photoUrl || defaultStudentPhoto" [alt]="student.fullName" loading="lazy" />
                  <div>
                    <p class="student-name">{{ student.fullName }}</p>
                    <p class="student-meta">{{ student.studentCode }} | {{ student.gradeName }}-{{ student.sectionName }}</p>
                  </div>
                </article>
              </div>

              <p *ngIf="!studentLoading && studentResults.length === 0" class="no-data">
                No students match your filters.
              </p>

              <div *ngIf="studentTotal > 0" class="pagination-row">
                <p class="page-summary">
                  Showing {{ studentPageStart }}-{{ studentPageEnd }} of {{ studentTotal }} students
                </p>

                <div class="page-actions">
                  <button class="btn btn-page" (click)="goToPreviousStudentPage()" [disabled]="studentPage === 1">Previous</button>
                  <span class="page-indicator">Page {{ studentPage }} / {{ studentTotalPages }}</span>
                  <button class="btn btn-page" (click)="goToNextStudentPage()" [disabled]="studentPage === studentTotalPages">Next</button>
                </div>
              </div>

              <section class="selected-students-panel">
                <div class="selected-header">
                  <h3>Selected Students ({{ selectedStudents.length }})</h3>
                  <button class="btn btn-page" (click)="clearSelectedStudents()" [disabled]="selectedStudents.length === 0">Clear All</button>
                </div>

                <div class="selected-grid" *ngIf="selectedStudents.length > 0">
                  <article *ngFor="let student of selectedStudents" class="selected-card">
                    <img [src]="student.photoUrl || defaultStudentPhoto" [alt]="student.fullName" loading="lazy" />
                    <div>
                      <p class="student-name">{{ student.fullName }}</p>
                      <p class="student-meta">{{ student.studentCode }} | {{ student.gradeName }}-{{ student.sectionName }}</p>
                    </div>
                    <button class="remove-selected" (click)="removeSelected(student.id, $event)">Remove</button>
                  </article>
                </div>

                <p *ngIf="selectedStudents.length === 0" class="no-data">No students selected yet.</p>
              </section>
            </ng-container>
          </main>
        </div>
      </div>

      <div *ngIf="showToast" class="toast" [class.success]="toastType === 'success'" [class.error]="toastType === 'error'">
        <span>{{ toastMessage }}</span>
        <button aria-label="Close" (click)="closeToast()">x</button>
      </div>

      <div *ngIf="selectedId !== null" class="modal-overlay">
        <div class="modal-content">
          <h3>Reject Request #{{ selectedId }}</h3>
          <p>Add a short reason to keep an audit trail.</p>
          <textarea [(ngModel)]="rejectReason" rows="3" placeholder="Enter reason..."></textarea>
          <div class="modal-actions">
            <button class="btn btn-confirm-reject" (click)="confirmReject()" [disabled]="!rejectReason">Confirm Rejection</button>
            <button class="btn btn-cancel" (click)="cancelRejection()">Cancel</button>
          </div>
        </div>
      </div>
    </section>
  `,
  styleUrl: './request-list.component.scss'
})
export class RequestListComponent implements OnInit {
  activeTask: 'task2' | 'task9' = 'task2';

  requests: ApprovalRequest[] = [];
  loading = true;
  selectedId: number | null = null;
  rejectReason = '';
  searchTerm = '';
  currentPage = 1;
  pageSize = 10;
  pageSizeOptions = [5, 10, 20, 50];
  showToast = false;
  toastMessage = '';
  toastType: 'success' | 'error' | 'info' = 'info';
  private toastTimerId: number | null = null;

  gradeOptions: string[] = [];
  sectionOptions: string[] = [];
  selectedGrade = '';
  selectedSection = '';
  studentSearch = '';
  studentResults: StudentDirectoryItem[] = [];
  selectedStudents: StudentDirectoryItem[] = [];
  selectedStudentIds = new Set<number>();
  studentLoading = false;
  studentPage = 1;
  studentPageSize = 24;
  studentTotal = 0;
  defaultStudentPhoto = 'https://via.placeholder.com/84x84.png?text=Student';
  private studentSearchDebounceId: number | null = null;

  get filteredRequests(): ApprovalRequest[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      return this.requests;
    }

    return this.requests.filter((request) =>
      request.title.toLowerCase().includes(term)
      || request.requestedBy.toLowerCase().includes(term)
      || request.id.toString().includes(term)
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredRequests.length / this.pageSize));
  }

  get paginatedRequests(): ApprovalRequest[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredRequests.slice(start, start + this.pageSize);
  }

  get pageStart(): number {
    if (this.filteredRequests.length === 0) {
      return 0;
    }
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredRequests.length);
  }

  constructor(
    private approvalService: ApprovalService,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    console.log('RequestListComponent initialized');
    this.loadRequests();
    this.loadGrades();
    this.loadSections();
    this.loadStudents();
  }

  switchTask(task: 'task2' | 'task9'): void {
    this.activeTask = task;
    this.cdr.detectChanges();
  }

  loadRequests(): void {
    console.log('Fetching pending requests...');
    this.loading = true;
    this.cdr.detectChanges();
    this.approvalService.getPendingRequests().subscribe({
      next: (data) => {
        console.log('Data received:', data);
        this.requests = data;
        this.currentPage = 1;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error fetching requests:', err);
        this.loading = false;
        this.showToastMessage('Could not fetch pending requests. Please retry.', 'error');
        this.cdr.detectChanges();
      },
      complete: () => {
        console.log('Request complete');
      }
    });
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.cdr.detectChanges();
  }

  onPageSizeChange(): void {
    this.currentPage = 1;
    this.cdr.detectChanges();
  }

  goToPreviousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage -= 1;
      this.cdr.detectChanges();
    }
  }

  goToNextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage += 1;
      this.cdr.detectChanges();
    }
  }

  approve(id: number): void {
    console.log(`UI: Approving request ${id}`);
    const decision = { decisionBy: 'Admin' };
    this.approvalService.approveRequest(id, decision).subscribe({
      next: (resp) => {
        console.log('UI: Approve success:', resp);
        this.showToastMessage('Request approved successfully.', 'success');
        this.loadRequests();
      },
      error: (err) => {
        console.error('UI: Approve failed:', err);
        this.showToastMessage('Approve failed. Please try again.', 'error');
      }
    });
  }

  selectForRejection(id: number): void {
    this.selectedId = id;
    this.rejectReason = '';
    this.cdr.detectChanges();
  }

  cancelRejection(): void {
    this.selectedId = null;
    this.cdr.detectChanges();
  }

  confirmReject(): void {
    if (this.selectedId !== null) {
      console.log(`UI: Rejecting request ${this.selectedId}`);
      const decision = {
        decisionBy: 'Admin',
        rejectReason: this.rejectReason
      };
      this.approvalService.rejectRequest(this.selectedId, decision).subscribe({
        next: (resp) => {
          console.log('UI: Reject success:', resp);
          this.showToastMessage('Request rejected successfully.', 'success');
          this.selectedId = null;
          this.loadRequests();
        },
        error: (err) => {
          console.error('UI: Reject failed:', err);
          this.showToastMessage('Reject failed. Please try again.', 'error');
        }
      });
    }
  }

  closeToast(): void {
    this.showToast = false;
    if (this.toastTimerId !== null) {
      window.clearTimeout(this.toastTimerId);
      this.toastTimerId = null;
    }
    this.cdr.detectChanges();
  }

  private showToastMessage(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.toastMessage = message;
    this.toastType = type;
    this.showToast = true;

    if (this.toastTimerId !== null) {
      window.clearTimeout(this.toastTimerId);
    }

    this.toastTimerId = window.setTimeout(() => {
      this.showToast = false;
      this.toastTimerId = null;
      this.cdr.detectChanges();
    }, 3200);

    this.cdr.detectChanges();
  }

  onGradeChanged(): void {
    this.selectedSection = '';
    this.loadSections();
    this.reloadStudentsFromStart();
  }

  onStudentSearchChanged(): void {
    if (this.studentSearchDebounceId !== null) {
      window.clearTimeout(this.studentSearchDebounceId);
    }

    this.studentSearchDebounceId = window.setTimeout(() => {
      this.reloadStudentsFromStart();
      this.studentSearchDebounceId = null;
    }, 260);
  }

  reloadStudentsFromStart(): void {
    this.studentPage = 1;
    this.loadStudents();
  }

  get studentTotalPages(): number {
    return Math.max(1, Math.ceil(this.studentTotal / this.studentPageSize));
  }

  get studentPageStart(): number {
    if (this.studentTotal === 0) {
      return 0;
    }
    return (this.studentPage - 1) * this.studentPageSize + 1;
  }

  get studentPageEnd(): number {
    return Math.min(this.studentPage * this.studentPageSize, this.studentTotal);
  }

  goToPreviousStudentPage(): void {
    if (this.studentPage > 1) {
      this.studentPage -= 1;
      this.loadStudents();
    }
  }

  goToNextStudentPage(): void {
    if (this.studentPage < this.studentTotalPages) {
      this.studentPage += 1;
      this.loadStudents();
    }
  }

  isSelected(studentId: number): boolean {
    return this.selectedStudentIds.has(studentId);
  }

  toggleStudent(student: StudentDirectoryItem): void {
    if (this.selectedStudentIds.has(student.id)) {
      this.selectedStudentIds.delete(student.id);
      this.selectedStudents = this.selectedStudents.filter((item) => item.id !== student.id);
    } else {
      this.selectedStudentIds.add(student.id);
      this.selectedStudents = [...this.selectedStudents, student];
    }
    this.cdr.detectChanges();
  }

  removeSelected(studentId: number, event: Event): void {
    event.stopPropagation();
    this.selectedStudentIds.delete(studentId);
    this.selectedStudents = this.selectedStudents.filter((student) => student.id !== studentId);
    this.cdr.detectChanges();
  }

  clearSelectedStudents(): void {
    this.selectedStudentIds.clear();
    this.selectedStudents = [];
    this.cdr.detectChanges();
  }

  private loadGrades(): void {
    this.approvalService.getGrades('', 100).subscribe({
      next: (data) => {
        this.gradeOptions = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.showToastMessage('Could not load grades.', 'error');
      }
    });
  }

  private loadSections(): void {
    this.approvalService.getSections(this.selectedGrade, '', 100).subscribe({
      next: (data) => {
        this.sectionOptions = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.showToastMessage('Could not load sections.', 'error');
      }
    });
  }

  private loadStudents(): void {
    this.studentLoading = true;
    this.approvalService.getStudents({
      grade: this.selectedGrade,
      section: this.selectedSection,
      search: this.studentSearch,
      page: this.studentPage,
      pageSize: this.studentPageSize,
      onlyActive: true
    }).subscribe({
      next: (result) => {
        this.studentResults = result.items;
        this.studentTotal = result.total;
        this.studentLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.studentResults = [];
        this.studentTotal = 0;
        this.studentLoading = false;
        this.showToastMessage('Could not load students.', 'error');
        this.cdr.detectChanges();
      }
    });
  }
}
