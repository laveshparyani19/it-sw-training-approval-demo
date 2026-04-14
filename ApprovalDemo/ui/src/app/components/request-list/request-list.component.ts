import { Component, OnInit, ChangeDetectorRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApprovalService } from '../../services/approval.service';
import { ApprovalRequest, CreateTlTeamAssignmentDto, StaffDirectoryItem, StudentDirectoryItem, TeamOptionItem, TlTeamAssignmentItem } from '../../models/approval.model';

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
            <button class="sidebar-link" [class.active]="activeTask === 'task10'" (click)="switchTask('task10')">Task 10</button>
            <button class="sidebar-link" [class.active]="activeTask === 'task11'" (click)="switchTask('task11')">Task 11 · Nucleus TL</button>
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

            <header class="content-header" *ngIf="activeTask === 'task10'">
              <p class="eyebrow">Staff Observation</p>
              <p class="subtitle">Select active staff only, automatically excluding system accounts.</p>
            </header>

            <header class="content-header" *ngIf="activeTask === 'task11'">
              <p class="eyebrow">Nucleus · Team lead</p>
              <p class="subtitle">Choose one team with round radio controls, then pick team members and capture the task (mirrors LIVE_PROJ department + staff picker flow).</p>
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
                      <p class="multi-empty" *ngIf="selectedGrades.length === 0">Select grade first to load sections</p>
                      <p class="multi-empty" *ngIf="selectedGrades.length > 0 && sectionOptions.length === 0">No sections available</p>
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
                    [disabled]="selectedGrades.length === 0"
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
                      <p class="multi-empty" *ngIf="selectedGrades.length === 0">Select grade first to load students</p>
                      <p class="multi-empty" *ngIf="selectedGrades.length > 0 && selectableStudents.length === 0">No students available</p>
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

              <p *ngIf="!studentLoading && selectedGrades.length === 0" class="no-data">
                Select at least one grade to load sections and students.
              </p>

              <p *ngIf="!studentLoading && selectedGrades.length > 0 && studentResults.length === 0" class="no-data">
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

            <ng-container *ngIf="activeTask === 'task10'">
              <div class="student-filters">
                <div class="student-filter-item">
                  <label for="department-select">Select Department(s)</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="department-select" class="multi-dropdown-toggle" (click)="toggleDepartmentDropdown()">
                      <span>{{ departmentSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu" *ngIf="departmentDropdownOpen">
                      <label class="multi-option">
                        <input type="checkbox" [checked]="isAllDepartmentsSelected" (change)="toggleAllDepartments($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let department of departmentOptions">
                        <input type="checkbox" [checked]="selectedDepartments.includes(department)" (change)="toggleDepartmentSelection(department, $event)" />
                        <span>{{ department }}</span>
                      </label>
                    </div>
                  </div>
                </div>

                <div class="student-filter-item">
                  <label for="team-select">Select Team(s)</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="team-select" class="multi-dropdown-toggle" (click)="toggleTeamDropdown()">
                      <span>{{ teamSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu" *ngIf="teamDropdownOpen">
                      <label class="multi-option">
                        <input type="checkbox" [checked]="isAllTeamsSelected" (change)="toggleAllTeams($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let team of teamOptions">
                        <input type="checkbox" [checked]="selectedTeams.includes(team)" (change)="toggleTeamSelection(team, $event)" />
                        <span>{{ team }}</span>
                      </label>
                      <p class="multi-empty" *ngIf="selectedDepartments.length === 0">Select department first to load teams</p>
                      <p class="multi-empty" *ngIf="selectedDepartments.length > 0 && teamOptions.length === 0">No teams available</p>
                    </div>
                  </div>
                </div>

                <div class="student-search-box">
                  <label for="staff-search">Search Staff</label>
                  <input
                    id="staff-search"
                    type="text"
                    [(ngModel)]="staffSearch"
                    (ngModelChange)="onStaffSearchChanged()"
                    [disabled]="selectedDepartments.length === 0"
                    placeholder="Name, code or designation"
                    aria-label="Search staff"
                  />
                </div>
              </div>

              <div class="student-picker-row">
                <div class="student-filter-item student-picker-field">
                  <label for="staff-picker">Select Staff</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="staff-picker" class="multi-dropdown-toggle" (click)="toggleStaffDropdown()">
                      <span>{{ staffSelectionSummary }}</span>
                      <span class="caret">▾</span>
                    </button>
                    <div class="multi-dropdown-menu student-menu" *ngIf="staffDropdownOpen">
                      <label class="multi-option" *ngIf="selectableStaff.length > 0">
                        <input type="checkbox" [checked]="isAllStaffSelected" (change)="toggleAllStaff($event)" />
                        <span>Check All</span>
                      </label>
                      <label class="multi-option" *ngFor="let staff of selectableStaff">
                        <input type="checkbox" [checked]="pendingStaffIds.includes(staff.id)" (change)="toggleStaffSelection(staff.id, $event)" />
                        <span>{{ staff.fullName }} ({{ staff.staffCode }})</span>
                      </label>
                      <p class="multi-empty" *ngIf="selectedDepartments.length === 0">Select department first to load staff</p>
                      <p class="multi-empty" *ngIf="selectedDepartments.length > 0 && selectableStaff.length === 0">No staff available</p>
                    </div>
                  </div>
                </div>
                <button class="btn btn-approve" (click)="addPendingStaff()" [disabled]="pendingStaffIds.length === 0">Add Selected</button>
              </div>

              <div *ngIf="staffLoading" class="loader-box">
                <div class="spinner"></div>
                <p>Loading staff...</p>
              </div>

              <div *ngIf="!staffLoading" class="student-grid">
                <article
                  *ngFor="let staff of staffResults"
                  class="student-card"
                  [class.selected]="isStaffSelected(staff.id)">
                  <img [src]="resolveStaffPhoto(staff)" [alt]="staff.fullName" (error)="onStaffImageError($event, staff)" loading="lazy" />
                  <div>
                    <p class="student-name">{{ staff.fullName }}</p>
                    <p class="student-meta">{{ staff.staffCode }} | {{ staff.departmentName }} - {{ staff.teamName }} | {{ staff.designation }}</p>
                  </div>
                  <button class="remove-selected" (click)="addStaff(staff, $event)" [disabled]="isStaffSelected(staff.id)">
                    {{ isStaffSelected(staff.id) ? 'Added' : 'Add' }}
                  </button>
                </article>
              </div>

              <p *ngIf="!staffLoading && selectedDepartments.length === 0" class="no-data">
                Select at least one department to load teams and staff.
              </p>

              <p *ngIf="!staffLoading && selectedDepartments.length > 0 && staffResults.length === 0" class="no-data">
                No staff match your filters.
              </p>

              <div *ngIf="staffTotal > 0" class="pagination-row">
                <p class="page-summary">
                  Showing {{ staffPageStart }}-{{ staffPageEnd }} of {{ staffTotal }} staff
                </p>

                <div class="page-actions">
                  <button class="btn btn-page" (click)="goToPreviousStaffPage()" [disabled]="staffPage === 1">Previous</button>
                  <span class="page-indicator">Page {{ staffPage }} / {{ staffTotalPages }}</span>
                  <button class="btn btn-page" (click)="goToNextStaffPage()" [disabled]="staffPage === staffTotalPages">Next</button>
                </div>
              </div>

              <section class="selected-students-panel">
                <div class="selected-header">
                  <h3>Selected Staff ({{ selectedStaff.length }})</h3>
                  <button class="btn btn-page" (click)="clearSelectedStaff()" [disabled]="selectedStaff.length === 0">Clear All</button>
                </div>

                <div class="selected-grid" *ngIf="selectedStaff.length > 0">
                  <article *ngFor="let staff of selectedStaff" class="selected-card">
                    <img [src]="resolveStaffPhoto(staff)" [alt]="staff.fullName" (error)="onStaffImageError($event, staff)" loading="lazy" />
                    <div>
                      <p class="student-name">{{ staff.fullName }}</p>
                      <p class="student-meta">{{ staff.staffCode }} | {{ staff.departmentName }} - {{ staff.teamName }} | {{ staff.designation }}</p>
                    </div>
                    <button class="remove-selected" (click)="removeSelectedStaff(staff.id, $event)">Remove</button>
                  </article>
                </div>

                <p *ngIf="selectedStaff.length === 0" class="no-data">No staff selected yet.</p>
              </section>
            </ng-container>

            <ng-container *ngIf="activeTask === 'task11'">
              <div class="tl-top-grid">
                <div class="tl-field">
                  <label for="tl-code">Team lead (staff code)</label>
                  <input
                    id="tl-code"
                    type="text"
                    [(ngModel)]="tlStaffCodeInput"
                    (ngModelChange)="onTlStaffCodeChanged()"
                    placeholder="e.g. STF-1007"
                    autocomplete="off"
                  />
                </div>
                <div class="student-search-box tl-search">
                  <label for="tl-member-search">Filter team members</label>
                  <input
                    id="tl-member-search"
                    type="text"
                    [(ngModel)]="tlMemberSearch"
                    (ngModelChange)="onTlMemberSearchChanged()"
                    [disabled]="!selectedTlTeam"
                    placeholder="Name or code"
                  />
                </div>
              </div>

              <div class="tl-team-section">
                <p class="field-label">Select team</p>
                <div class="team-radio-list" *ngIf="teamTlOptions.length > 0; else tlTeamsLoading">
                  <label *ngFor="let t of teamTlOptions" class="team-radio-row">
                    <input
                      type="radio"
                      class="circle-radio"
                      name="nucleusTeamPick"
                      [checked]="isTlTeamSelected(t)"
                      (change)="selectTlTeam(t)"
                    />
                    <span>{{ t.displayLabel }}</span>
                  </label>
                </div>
                <ng-template #tlTeamsLoading>
                  <p class="multi-empty">{{ tlTeamsLoading ? 'Loading teams…' : 'No teams found in staff directory.' }}</p>
                </ng-template>
              </div>

              <div class="student-picker-row tl-picker-row">
                <div class="student-filter-item student-picker-field">
                  <label for="tl-staff-picker">Select staff</label>
                  <div class="multi-dropdown" (click)="$event.stopPropagation()">
                    <button type="button" id="tl-staff-picker" class="multi-dropdown-toggle" (click)="toggleTlMemberDropdown()" [disabled]="!selectedTlTeam">
                      <span>{{ tlMemberSelectionSummary }}</span>
                      <span class="caret">&#9662;</span>
                    </button>
                    <div class="multi-dropdown-menu student-menu" *ngIf="tlMemberDropdownOpen">
                      <label class="multi-option" *ngIf="tlMembers.length > 0">
                        <input type="checkbox" [checked]="isAllTlMembersSelected" (change)="toggleAllTlMembers($event)" />
                        <span>Check/uncheck all</span>
                      </label>
                      <label class="multi-option" *ngFor="let staff of tlMembers">
                        <input type="checkbox" [checked]="pendingTlMemberIds.includes(staff.id)" (change)="toggleTlMemberSelection(staff.id, $event)" />
                        <span>{{ staff.fullName }}</span>
                      </label>
                      <p class="multi-empty" *ngIf="!selectedTlTeam">Select a team first.</p>
                      <p class="multi-empty" *ngIf="selectedTlTeam && tlMembersLoading">Loading staff…</p>
                      <p class="multi-empty" *ngIf="selectedTlTeam && !tlMembersLoading && tlMembers.length === 0">No staff in this team.</p>
                    </div>
                  </div>
                </div>
              </div>

              <div class="tl-task-area">
                <label for="tl-task-desc">Enter task (required)</label>
                <textarea id="tl-task-desc" rows="4" [(ngModel)]="tlTaskDescription" placeholder="Describe the task for the selected members"></textarea>
              </div>

              <button type="button" class="tl-submit-bar" (click)="submitTlAssignment()" [disabled]="tlSubmitting || !canSubmitTlAssignment">
                {{ tlSubmitting ? 'Saving…' : 'Task of ' + (tlStaffCodeInput || 'TL').trim() }}
              </button>

              <section class="selected-students-panel" *ngIf="recentTlAssignments.length > 0">
                <div class="selected-header">
                  <h3>Recent assignments ({{ tlStaffCodeInput.trim() || '—' }})</h3>
                </div>
                <ul class="tl-recent-list">
                  <li *ngFor="let a of recentTlAssignments">
                    <div>
                      <strong>{{ a.departmentName }} — {{ a.teamName }}</strong>
                      <p class="tl-recent-meta">{{ a.memberStaffIds.length }} member(s) · {{ a.createdAtUtc | date:'medium' }}</p>
                      <p class="tl-recent-task" *ngIf="a.taskDescription">{{ a.taskDescription }}</p>
                    </div>
                  </li>
                </ul>
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
  activeTask: 'task2' | 'task9' | 'task10' | 'task11' = 'task2';

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

  departmentOptions: string[] = [];
  teamOptions: string[] = [];
  selectedDepartments: string[] = [];
  selectedTeams: string[] = [];
  staffSearch = '';
  staffResults: StaffDirectoryItem[] = [];
  selectedStaff: StaffDirectoryItem[] = [];
  selectedStaffIds = new Set<number>();
  pendingStaffIds: number[] = [];
  departmentDropdownOpen = false;
  teamDropdownOpen = false;
  staffDropdownOpen = false;
  staffLoading = false;
  staffPage = 1;
  staffPageSize = 24;
  staffTotal = 0;
  private failedPhotoStaffIds = new Set<number>();
  private staffSearchDebounceId: number | null = null;

  teamTlOptions: TeamOptionItem[] = [];
  tlTeamsLoading = false;
  selectedTlTeam: TeamOptionItem | null = null;
  tlStaffCodeInput = 'STF-1007';
  tlMemberSearch = '';
  tlMembers: StaffDirectoryItem[] = [];
  tlMembersLoading = false;
  pendingTlMemberIds: number[] = [];
  tlMemberDropdownOpen = false;
  tlTaskDescription = '';
  tlSubmitting = false;
  recentTlAssignments: TlTeamAssignmentItem[] = [];
  private tlMemberSearchDebounceId: number | null = null;

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

  get canSubmitTlAssignment(): boolean {
    return !!(
      this.selectedTlTeam
      && this.pendingTlMemberIds.length > 0
      && this.tlStaffCodeInput.trim().length > 0
      && this.tlTaskDescription.trim().length > 0
    );
  }

  get tlMemberSelectionSummary(): string {
    if (this.pendingTlMemberIds.length === 0) {
      return 'Select staff';
    }
    const names = this.tlMembers
      .filter((s) => this.pendingTlMemberIds.includes(s.id))
      .map((s) => s.fullName);
    return names.length > 0 ? names.join(', ') : `${this.pendingTlMemberIds.length} selected`;
  }

  get isAllTlMembersSelected(): boolean {
    return this.tlMembers.length > 0 && this.pendingTlMemberIds.length === this.tlMembers.length;
  }

  constructor(
    private approvalService: ApprovalService,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    console.log('RequestListComponent initialized');
    this.loadRequests();
    this.loadGrades();
    this.loadDepartments();
  }

  switchTask(task: 'task2' | 'task9' | 'task10' | 'task11'): void {
    this.activeTask = task;

    if (task === 'task10' && this.departmentOptions.length === 0) {
      this.loadDepartments();
    }

    if (task === 'task11') {
      this.ensureTlTeamsLoaded();
      this.loadRecentTlAssignments();
    }

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
    this.pendingStudentIds = [];

    if (this.selectedGrades.length === 0) {
      this.sectionOptions = [];
      this.studentSearch = '';
      this.studentResults = [];
      this.studentTotal = 0;
      this.sectionDropdownOpen = false;
      this.studentDropdownOpen = false;
      this.cdr.detectChanges();
      return;
    }

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
    this.departmentDropdownOpen = false;
    this.teamDropdownOpen = false;
    this.staffDropdownOpen = false;
    this.tlMemberDropdownOpen = false;
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
    if (this.selectedGrades.length === 0) {
      this.studentResults = [];
      this.studentTotal = 0;
      this.pendingStudentIds = [];
      this.cdr.detectChanges();
      return;
    }

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

  onDepartmentsChanged(): void {
    this.selectedTeams = [];
    this.pendingStaffIds = [];

    if (this.selectedDepartments.length === 0) {
      this.teamOptions = [];
      this.staffSearch = '';
      this.staffResults = [];
      this.staffTotal = 0;
      this.teamDropdownOpen = false;
      this.staffDropdownOpen = false;
      this.cdr.detectChanges();
      return;
    }

    this.loadTeams();
    this.reloadStaffFromStart();
  }

  onTeamsChanged(): void {
    this.reloadStaffFromStart();
  }

  get departmentSelectionSummary(): string {
    if (this.selectedDepartments.length === 0) {
      return 'Select department';
    }
    return this.selectedDepartments.join(', ');
  }

  get teamSelectionSummary(): string {
    if (this.selectedTeams.length === 0) {
      return 'Select team';
    }
    return this.selectedTeams.join(', ');
  }

  get staffSelectionSummary(): string {
    if (this.pendingStaffIds.length === 0) {
      return 'Select staff';
    }

    const selectedNames = this.selectableStaff
      .filter((staff) => this.pendingStaffIds.includes(staff.id))
      .map((staff) => staff.fullName);

    if (selectedNames.length === 0) {
      return `${this.pendingStaffIds.length} selected`;
    }

    return selectedNames.join(', ');
  }

  get isAllDepartmentsSelected(): boolean {
    return this.departmentOptions.length > 0 && this.selectedDepartments.length === this.departmentOptions.length;
  }

  get isAllTeamsSelected(): boolean {
    return this.teamOptions.length > 0 && this.selectedTeams.length === this.teamOptions.length;
  }

  get isAllStaffSelected(): boolean {
    return this.selectableStaff.length > 0 && this.pendingStaffIds.length === this.selectableStaff.length;
  }

  toggleDepartmentDropdown(): void {
    this.departmentDropdownOpen = !this.departmentDropdownOpen;
    this.teamDropdownOpen = false;
    this.staffDropdownOpen = false;
  }

  toggleTeamDropdown(): void {
    this.teamDropdownOpen = !this.teamDropdownOpen;
    this.departmentDropdownOpen = false;
    this.staffDropdownOpen = false;
  }

  toggleStaffDropdown(): void {
    this.staffDropdownOpen = !this.staffDropdownOpen;
    this.departmentDropdownOpen = false;
    this.teamDropdownOpen = false;
  }

  toggleAllDepartments(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedDepartments = checked ? [...this.departmentOptions] : [];
    this.onDepartmentsChanged();
  }

  toggleDepartmentSelection(department: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedDepartments = checked
      ? [...this.selectedDepartments, department]
      : this.selectedDepartments.filter((value) => value !== department);
    this.onDepartmentsChanged();
  }

  toggleAllTeams(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedTeams = checked ? [...this.teamOptions] : [];
    this.onTeamsChanged();
  }

  toggleTeamSelection(team: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedTeams = checked
      ? [...this.selectedTeams, team]
      : this.selectedTeams.filter((value) => value !== team);
    this.onTeamsChanged();
  }

  toggleAllStaff(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingStaffIds = checked ? this.selectableStaff.map((staff) => staff.id) : [];
  }

  toggleStaffSelection(staffId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingStaffIds = checked
      ? [...this.pendingStaffIds, staffId]
      : this.pendingStaffIds.filter((id) => id !== staffId);
  }

  onStaffSearchChanged(): void {
    if (this.selectedDepartments.length === 0) {
      this.staffResults = [];
      this.staffTotal = 0;
      this.pendingStaffIds = [];
      this.cdr.detectChanges();
      return;
    }

    if (this.staffSearchDebounceId !== null) {
      window.clearTimeout(this.staffSearchDebounceId);
    }

    this.staffSearchDebounceId = window.setTimeout(() => {
      this.reloadStaffFromStart();
      this.staffSearchDebounceId = null;
    }, 260);
  }

  reloadStaffFromStart(): void {
    this.staffPage = 1;
    this.loadStaff();
  }

  get staffTotalPages(): number {
    return Math.max(1, Math.ceil(this.staffTotal / this.staffPageSize));
  }

  get staffPageStart(): number {
    if (this.staffTotal === 0) {
      return 0;
    }
    return (this.staffPage - 1) * this.staffPageSize + 1;
  }

  get staffPageEnd(): number {
    return Math.min(this.staffPage * this.staffPageSize, this.staffTotal);
  }

  goToPreviousStaffPage(): void {
    if (this.staffPage > 1) {
      this.staffPage -= 1;
      this.loadStaff();
    }
  }

  goToNextStaffPage(): void {
    if (this.staffPage < this.staffTotalPages) {
      this.staffPage += 1;
      this.loadStaff();
    }
  }

  isStaffSelected(staffId: number): boolean {
    return this.selectedStaffIds.has(staffId);
  }

  addStaff(staff: StaffDirectoryItem, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    if (this.selectedStaffIds.has(staff.id)) {
      return;
    }

    this.selectedStaffIds.add(staff.id);
    this.selectedStaff = [...this.selectedStaff, staff];
    this.pendingStaffIds = this.pendingStaffIds.filter((id) => id !== staff.id);
    this.cdr.detectChanges();
  }

  addPendingStaff(): void {
    if (this.pendingStaffIds.length === 0) {
      return;
    }

    const staffToAdd = this.staffResults.filter((staff) => this.pendingStaffIds.includes(staff.id));
    if (staffToAdd.length === 0) {
      this.showToastMessage('Selected staff are not available on this page. Adjust filters and try again.', 'info');
      return;
    }

    for (const staff of staffToAdd) {
      if (!this.selectedStaffIds.has(staff.id)) {
        this.selectedStaffIds.add(staff.id);
        this.selectedStaff = [...this.selectedStaff, staff];
      }
    }

    this.pendingStaffIds = [];
    this.cdr.detectChanges();
  }

  removeSelectedStaff(staffId: number, event: Event): void {
    event.stopPropagation();
    this.selectedStaffIds.delete(staffId);
    this.selectedStaff = this.selectedStaff.filter((staff) => staff.id !== staffId);
    this.cdr.detectChanges();
  }

  clearSelectedStaff(): void {
    this.selectedStaffIds.clear();
    this.selectedStaff = [];
    this.pendingStaffIds = [];
    this.cdr.detectChanges();
  }

  get selectableStaff(): StaffDirectoryItem[] {
    return this.staffResults.filter((staff) => !this.selectedStaffIds.has(staff.id));
  }

  resolveStaffPhoto(staff: StaffDirectoryItem): string {
    if (staff.photoUrl && !this.failedPhotoStaffIds.has(staff.id)) {
      return staff.photoUrl;
    }
    return this.isLikelyFemaleByName(staff.fullName) ? this.femaleAvatar : this.maleAvatar;
  }

  onStaffImageError(event: Event, staff: StaffDirectoryItem): void {
    this.failedPhotoStaffIds.add(staff.id);
    const image = event.target as HTMLImageElement;
    image.src = this.resolveStaffPhoto(staff);
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
    if (this.selectedGrades.length === 0) {
      this.sectionOptions = [];
      this.selectedSections = [];
      this.cdr.detectChanges();
      return;
    }

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
    if (this.selectedGrades.length === 0) {
      this.studentLoading = false;
      this.studentResults = [];
      this.studentTotal = 0;
      this.pendingStudentIds = [];
      this.cdr.detectChanges();
      return;
    }

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

  private loadDepartments(): void {
    this.approvalService.getDepartments('', 100).subscribe({
      next: (data) => {
        this.departmentOptions = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.showToastMessage('Could not load departments.', 'error');
      }
    });
  }

  private loadTeams(): void {
    if (this.selectedDepartments.length === 0) {
      this.teamOptions = [];
      this.selectedTeams = [];
      this.cdr.detectChanges();
      return;
    }

    this.approvalService.getTeams(this.selectedDepartments, '', 100).subscribe({
      next: (data) => {
        this.teamOptions = data;
        this.cdr.detectChanges();
      },
      error: () => {
        this.showToastMessage('Could not load teams.', 'error');
      }
    });
  }

  private loadStaff(): void {
    if (this.selectedDepartments.length === 0) {
      this.staffLoading = false;
      this.staffResults = [];
      this.staffTotal = 0;
      this.pendingStaffIds = [];
      this.cdr.detectChanges();
      return;
    }

    this.staffLoading = true;
    this.approvalService.getStaff({
      departments: this.selectedDepartments,
      teams: this.selectedTeams,
      search: this.staffSearch,
      page: this.staffPage,
      pageSize: this.staffPageSize,
      onlyActive: true,
      excludeSystemAccounts: true
    }).subscribe({
      next: (result) => {
        this.staffResults = result.items;
        this.staffTotal = result.total;
        this.pendingStaffIds = this.pendingStaffIds.filter((id) => this.staffResults.some((staff) => staff.id === id));
        this.staffLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.staffResults = [];
        this.staffTotal = 0;
        this.staffLoading = false;
        this.showToastMessage('Could not load staff.', 'error');
        this.cdr.detectChanges();
      }
    });
  }

  private isLikelyFemale(student: StudentDirectoryItem): boolean {
    return this.isLikelyFemaleByName(student.fullName);
  }

  private isLikelyFemaleByName(fullName: string): boolean {
    const firstName = (fullName.split(' ')[0] ?? '').toLowerCase();
    const knownFemaleNames = ['aadya', 'aaeesha', 'diya', 'ira', 'mira', 'riya', 'anaya', 'siya'];
    if (knownFemaleNames.includes(firstName)) {
      return true;
    }

    if (firstName.endsWith('a') || firstName.endsWith('i')) {
      return true;
    }

    return false;
  }

  private ensureTlTeamsLoaded(): void {
    if (this.teamTlOptions.length > 0 || this.tlTeamsLoading) {
      return;
    }
    this.tlTeamsLoading = true;
    this.approvalService.getTlTeamOptions(200).subscribe({
      next: (opts) => {
        this.teamTlOptions = opts;
        this.tlTeamsLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.tlTeamsLoading = false;
        this.showToastMessage('Could not load team list.', 'error');
        this.cdr.detectChanges();
      }
    });
  }

  isTlTeamSelected(t: TeamOptionItem): boolean {
    return !!(
      this.selectedTlTeam
      && this.selectedTlTeam.departmentName === t.departmentName
      && this.selectedTlTeam.teamName === t.teamName
    );
  }

  selectTlTeam(t: TeamOptionItem): void {
    this.selectedTlTeam = t;
    this.pendingTlMemberIds = [];
    this.tlMemberSearch = '';
    this.loadTlMembersForTeam();
  }

  onTlMemberSearchChanged(): void {
    if (!this.selectedTlTeam) {
      return;
    }
    if (this.tlMemberSearchDebounceId !== null) {
      window.clearTimeout(this.tlMemberSearchDebounceId);
    }
    this.tlMemberSearchDebounceId = window.setTimeout(() => {
      this.loadTlMembersForTeam();
      this.tlMemberSearchDebounceId = null;
    }, 260);
  }

  private loadTlMembersForTeam(): void {
    if (!this.selectedTlTeam) {
      this.tlMembers = [];
      return;
    }
    this.tlMembersLoading = true;
    this.approvalService
      .getTlTeamMembers({
        department: this.selectedTlTeam.departmentName,
        team: this.selectedTlTeam.teamName,
        search: this.tlMemberSearch,
        page: 1,
        pageSize: 200
      })
      .subscribe({
        next: (r) => {
          this.tlMembers = r.items;
          this.pendingTlMemberIds = this.pendingTlMemberIds.filter((id) =>
            this.tlMembers.some((m) => m.id === id)
          );
          this.tlMembersLoading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.tlMembers = [];
          this.tlMembersLoading = false;
          this.showToastMessage('Could not load team members.', 'error');
          this.cdr.detectChanges();
        }
      });
  }

  toggleTlMemberDropdown(): void {
    this.tlMemberDropdownOpen = !this.tlMemberDropdownOpen;
    this.gradeDropdownOpen = false;
    this.sectionDropdownOpen = false;
    this.studentDropdownOpen = false;
    this.departmentDropdownOpen = false;
    this.teamDropdownOpen = false;
    this.staffDropdownOpen = false;
  }

  toggleAllTlMembers(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingTlMemberIds = checked ? this.tlMembers.map((m) => m.id) : [];
  }

  toggleTlMemberSelection(staffId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.pendingTlMemberIds = checked
      ? [...this.pendingTlMemberIds, staffId]
      : this.pendingTlMemberIds.filter((id) => id !== staffId);
  }

  onTlStaffCodeChanged(): void {
    if (this.activeTask === 'task11') {
      this.loadRecentTlAssignments();
    }
  }

  private loadRecentTlAssignments(): void {
    const code = this.tlStaffCodeInput.trim();
    if (!code) {
      this.recentTlAssignments = [];
      return;
    }
    this.approvalService.getTlAssignments(code, 15).subscribe({
      next: (rows) => {
        this.recentTlAssignments = rows;
        this.cdr.detectChanges();
      },
      error: () => {
        this.recentTlAssignments = [];
      }
    });
  }

  submitTlAssignment(): void {
    if (!this.canSubmitTlAssignment || !this.selectedTlTeam || this.tlSubmitting) {
      return;
    }
    const dto: CreateTlTeamAssignmentDto = {
      tlStaffCode: this.tlStaffCodeInput.trim(),
      departmentName: this.selectedTlTeam.departmentName,
      teamName: this.selectedTlTeam.teamName,
      memberStaffIds: [...this.pendingTlMemberIds],
      taskDescription: this.tlTaskDescription.trim()
    };
    this.tlSubmitting = true;
    this.approvalService.createTlAssignment(dto).subscribe({
      next: () => {
        this.tlSubmitting = false;
        this.pendingTlMemberIds = [];
        this.tlTaskDescription = '';
        this.showToastMessage('Assignment saved. MSSQL mirror syncs on the next worker run when enabled.', 'success');
        this.loadRecentTlAssignments();
        this.cdr.detectChanges();
      },
      error: () => {
        this.tlSubmitting = false;
        this.showToastMessage('Could not save assignment. Check team, members, and task text.', 'error');
        this.cdr.detectChanges();
      }
    });
  }

  private buildAvatarDataUri(background: string, foreground: string): string {
    const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96' viewBox='0 0 96 96'><rect width='96' height='96' rx='48' fill='${background}'/><circle cx='48' cy='34' r='14' fill='${foreground}' opacity='0.25'/><path d='M22 80c2-13 12-21 26-21s24 8 26 21' fill='${foreground}' opacity='0.28'/><circle cx='48' cy='48' r='42' fill='none' stroke='${foreground}' stroke-opacity='0.12' stroke-width='2'/></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }
}
