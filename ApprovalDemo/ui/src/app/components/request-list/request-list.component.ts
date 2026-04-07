import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApprovalService } from '../../services/approval.service';
import { ApprovalRequest } from '../../models/approval.model';

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
            <a href="#" class="sidebar-link active" aria-current="page">Task 2</a>
          </aside>

          <main class="content-panel">
            <header class="content-header">
              <p class="eyebrow">Operations Console</p>
              <p class="subtitle">Track, review, and resolve incoming requests quickly.</p>
            </header>

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
                  <tr *ngFor="let request of requests">
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
                  <tr *ngIf="requests.length === 0">
                    <td colspan="5" class="no-data">No pending requests found. Seed data and hit refresh.</td>
                  </tr>
                </tbody>
              </table>
            </div>
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
  requests: ApprovalRequest[] = [];
  loading = true;
  selectedId: number | null = null;
  rejectReason = '';
  showToast = false;
  toastMessage = '';
  toastType: 'success' | 'error' | 'info' = 'info';
  private toastTimerId: number | null = null;

  constructor(
    private approvalService: ApprovalService,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    console.log('RequestListComponent initialized');
    this.loadRequests();
  }

  loadRequests(): void {
    console.log('Fetching pending requests...');
    this.loading = true;
    this.cdr.detectChanges();
    this.approvalService.getPendingRequests().subscribe({
      next: (data) => {
        console.log('Data received:', data);
        this.requests = data;
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
}
