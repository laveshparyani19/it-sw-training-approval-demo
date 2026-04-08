import { Component, OnInit, ChangeDetectorRef, HostListener } from '@angular/core';
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
                  <label for="grade-select">Select Grade(s)</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="grade-select" class="multi-dropdown-toggle" (click)="toggleGradeDropdown()">
                      <span>{{ gradeSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu" *ngIf="gradeDropdownOpen">
                      <label class="multi-option">
                        <input type="checkbox" [checked]="isAllGradesSelected" (change)="toggleAllGrades($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let grade of gradeOptions">
                        <input type="checkbox" [checked]="selectedGrades.includes(grade)" (change)="toggleGradeSelection(grade, $event)" />
                        <span>{{ grade }}</span>
                      </label>
                    </div>
                  </div>
                </div>

                <div class="student-filter-item">
                  <label for="section-select">Select Section(s)</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="section-select" class="multi-dropdown-toggle" (click)="toggleSectionDropdown()">
                      <span>{{ sectionSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu" *ngIf="sectionDropdownOpen">
                      <label class="multi-option">
                        <input type="checkbox" [checked]="isAllSectionsSelected" (change)="toggleAllSections($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let section of sectionOptions">
                        <input type="checkbox" [checked]="selectedSections.includes(section)" (change)="toggleSectionSelection(section, $event)" />
                        <span>{{ section }}</span>
                      </label>
                    </div>
                  </div>
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

              <div class="student-picker-row">
                <div class="student-filter-item student-picker-field">
                  <label for="student-picker">Select Student(s)</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="student-picker" class="multi-dropdown-toggle" (click)="toggleStudentDropdown()">
                      <span>{{ studentSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu student-menu" *ngIf="studentDropdownOpen">
                      <label class="multi-option" *ngIf="selectableStudents.length > 0">
                        <input type="checkbox" [checked]="isAllStudentsSelected" (change)="toggleAllStudents($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let student of selectableStudents">
                        <input type="checkbox" [checked]="pendingStudentIds.includes(student.id)" (change)="toggleStudentSelection(student.id, $event)" />
                        <span>{{ student.fullName }} ({{ student.studentCode }})</span>
                      </label>
                      <p class="multi-empty" *ngIf="selectableStudents.length === 0">No students available</p>
                    </div>
                  </div>
                </div>
                <button class="btn btn-approve" (click)="addPendingStudents()" [disabled]="pendingStudentIds.length === 0">Add Selected</button>
              </div>

              <div *ngIf="studentLoading" class="loader-box">
                <div class="spinner"></div>
                <p>Loading students...</p>
              </div>

              <div *ngIf="!studentLoading" class="student-grid">
                <article
                  *ngFor="let student of studentResults"
                  class="student-card"
                  [class.selected]="isSelected(student.id)">
                  <img [src]="resolveStudentPhoto(student)" [alt]="student.fullName" (error)="onStudentImageError($event, student)" loading="lazy" />
                  <div>
                    <p class="student-name">{{ student.fullName }}</p>
                    <p class="student-meta">{{ student.studentCode }} | {{ student.gradeName }}-{{ student.sectionName }}</p>
                  </div>
                  <button class="remove-selected" (click)="addStudent(student, $event)" [disabled]="isSelected(student.id)">
                    {{ isSelected(student.id) ? 'Added' : 'Add' }}
                  </button>
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
                    <img [src]="resolveStudentPhoto(student)" [alt]="student.fullName" (error)="onStudentImageError($event, student)" loading="lazy" />
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
  selectedGrades: string[] = [];
  selectedSections: string[] = [];
  studentSearch = '';
  studentResults: StudentDirectoryItem[] = [];
  selectedStudents: StudentDirectoryItem[] = [];
  selectedStudentIds = new Set<number>();
  pendingStudentIds: number[] = [];
  gradeDropdownOpen = false;
  sectionDropdownOpen = false;
  studentDropdownOpen = false;
  studentLoading = false;
  studentPage = 1;
  studentPageSize = 24;
  studentTotal = 0;
  private failedPhotoStudentIds = new Set<number>();
  private readonly maleAvatar = this.buildAvatarDataUri('#d7ecff', '#1b5f8c');
  private readonly femaleAvatar = this.buildAvatarDataUri('#ffe0ee', '#8a2f5a');
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

  onGradesChanged(): void {
    this.selectedSections = [];
    this.loadSections();
    this.reloadStudentsFromStart();
  }

  onSectionsChanged(): void {
    this.reloadStudentsFromStart();
  }

  @HostListener('document:click')
  closeDropdownsOnOutsideClick(): void {
    this.gradeDropdownOpen = false;
    this.sectionDropdownOpen = false;
    this.studentDropdownOpen = false;
  }

  get gradeSelectionSummary(): string {
    if (this.selectedGrades.length === 0) {
      return 'Select grade';
    }
    return this.selectedGrades.join(', ');
  }

  get sectionSelectionSummary(): string {
    if (this.selectedSections.length === 0) {
      return 'Select section';
    }
    return this.selectedSections.join(', ');
  }

  get studentSelectionSummary(): string {
    if (this.pendingStudentIds.length === 0) {
      return 'Select student';
    }

    const selectedNames = this.selectableStudents
      .filter((student) => this.pendingStudentIds.includes(student.id))
      .map((student) => student.fullName);

    if (selectedNames.length === 0) {
      return `${this.pendingStudentIds.length} selected`;
    }

    return selectedNames.join(', ');
  }

  get isAllGradesSelected(): boolean {
    return this.gradeOptions.length > 0 && this.selectedGrades.length === this.gradeOptions.length;
  }

  get isAllSectionsSelected(): boolean {
    return this.sectionOptions.length > 0 && this.selectedSections.length === this.sectionOptions.length;
  }

  get isAllStudentsSelected(): boolean {
    return this.selectableStudents.length > 0 && this.pendingStudentIds.length === this.selectableStudents.length;
  }

  toggleGradeDropdown(): void {
    this.gradeDropdownOpen = !this.gradeDropdownOpen;
    this.sectionDropdownOpen = false;
    this.studentDropdownOpen = false;
  }

  toggleSectionDropdown(): void {
    this.sectionDropdownOpen = !this.sectionDropdownOpen;
    this.gradeDropdownOpen = false;
    this.studentDropdownOpen = false;
  }

  toggleStudentDropdown(): void {
    this.studentDropdownOpen = !this.studentDropdownOpen;
    this.gradeDropdownOpen = false;
    this.sectionDropdownOpen = false;
  }

  toggleAllGrades(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedGrades = checked ? [...this.gradeOptions] : [];
    this.onGradesChanged();
  }

  toggleGradeSelection(grade: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedGrades = checked
      ? [...this.selectedGrades, grade]
      : this.selectedGrades.filter((value) => value !== grade);
    this.onGradesChanged();
  }

  toggleAllSections(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedSections = checked ? [...this.sectionOptions] : [];
    this.onSectionsChanged();
  }

  toggleSectionSelection(section: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedSections = checked
      ? [...this.selectedSections, section]
      : this.selectedSections.filter((value) => value !== section);
    this.onSectionsChanged();
  }

  toggleAllStudents(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingStudentIds = checked ? this.selectableStudents.map((student) => student.id) : [];
  }

  toggleStudentSelection(studentId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingStudentIds = checked
      ? [...this.pendingStudentIds, studentId]
      : this.pendingStudentIds.filter((id) => id !== studentId);
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

  addStudent(student: StudentDirectoryItem, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    if (this.selectedStudentIds.has(student.id)) {
      return;
    }

    this.selectedStudentIds.add(student.id);
    this.selectedStudents = [...this.selectedStudents, student];
    this.pendingStudentIds = this.pendingStudentIds.filter((id) => id !== student.id);
    this.cdr.detectChanges();
  }

  addPendingStudents(): void {
    if (this.pendingStudentIds.length === 0) {
      return;
    }

    const studentsToAdd = this.studentResults.filter((student) => this.pendingStudentIds.includes(student.id));
    if (studentsToAdd.length === 0) {
      this.showToastMessage('Selected students are not available on this page. Adjust filters and try again.', 'info');
      return;
    }

    for (const student of studentsToAdd) {
      if (!this.selectedStudentIds.has(student.id)) {
        this.selectedStudentIds.add(student.id);
        this.selectedStudents = [...this.selectedStudents, student];
      }
    }

    this.pendingStudentIds = [];
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
    this.pendingStudentIds = [];
    this.cdr.detectChanges();
  }

  get selectableStudents(): StudentDirectoryItem[] {
    return this.studentResults.filter((student) => !this.selectedStudentIds.has(student.id));
  }

  resolveStudentPhoto(student: StudentDirectoryItem): string {
    if (student.photoUrl && !this.failedPhotoStudentIds.has(student.id)) {
      return student.photoUrl;
    }
    return this.isLikelyFemale(student) ? this.femaleAvatar : this.maleAvatar;
  }

  onStudentImageError(event: Event, student: StudentDirectoryItem): void {
    this.failedPhotoStudentIds.add(student.id);
    const image = event.target as HTMLImageElement;
    image.src = this.resolveStudentPhoto(student);
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
    this.approvalService.getSections(this.selectedGrades, '', 100).subscribe({
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
      grades: this.selectedGrades,
      sections: this.selectedSections,
      search: this.studentSearch,
      page: this.studentPage,
      pageSize: this.studentPageSize,
      onlyActive: true
    }).subscribe({
      next: (result) => {
        this.studentResults = result.items;
        this.studentTotal = result.total;
        this.pendingStudentIds = this.pendingStudentIds.filter((id) => this.studentResults.some((student) => student.id === id));
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

  private isLikelyFemale(student: StudentDirectoryItem): boolean {
    const firstName = (student.fullName.split(' ')[0] ?? '').toLowerCase();
    const knownFemaleNames = ['aadya', 'aaeesha', 'diya', 'ira', 'mira', 'riya', 'anaya', 'siya'];
    if (knownFemaleNames.includes(firstName)) {
      return true;
    }

    if (firstName.endsWith('a') || firstName.endsWith('i')) {
      return true;
    }

    return student.id % 2 === 0;
  }

  private buildAvatarDataUri(background: string, foreground: string): string {
    const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96' viewBox='0 0 96 96'><rect width='96' height='96' rx='48' fill='${background}'/><circle cx='48' cy='34' r='14' fill='${foreground}' opacity='0.25'/><path d='M22 80c2-13 12-21 26-21s24 8 26 21' fill='${foreground}' opacity='0.28'/><circle cx='48' cy='48' r='42' fill='none' stroke='${foreground}' stroke-opacity='0.12' stroke-width='2'/></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }
}
