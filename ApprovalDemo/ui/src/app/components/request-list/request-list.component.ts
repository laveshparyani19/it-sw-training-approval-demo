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
    <div class="container">
      <div class="header">
        <h1>Pending Approval Requests</h1>
        <button class="btn btn-refresh" (click)="loadRequests()">Refresh List</button>
      </div>
      
      <div *ngIf="loading" class="loader-box">
        <p>Loading requests from API...</p>
        <small>Check browser console for details</small>
      </div>
      
      <table *ngIf="!loading" class="approval-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Title</th>
            <th>Requested By</th>
            <th>Created At</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let request of requests">
            <td>{{ request.id }}</td>
            <td>{{ request.title }}</td>
            <td>{{ request.requestedBy }}</td>
            <td>{{ request.createdAt | date:'medium' }}</td>
            <td>
              <button class="btn btn-approve" (click)="approve(request.id)">Approve</button>
              <button class="btn btn-reject" (click)="selectForRejection(request.id)">Reject</button>
            </td>
          </tr>
          <tr *ngIf="requests.length === 0">
            <td colspan="5" class="no-data">No pending requests found. Try seeding data via Swagger or SQL.</td>
          </tr>
        </tbody>
      </table>

      <!-- Simple Reject Modal -->
      <div *ngIf="selectedId !== null" class="modal-overlay">
        <div class="modal-content">
          <h3>Reject Request #{{ selectedId }}</h3>
          <p>Please provide a reason for rejection:</p>
          <textarea [(ngModel)]="rejectReason" rows="3" placeholder="Enter reason..."></textarea>
          <div class="modal-actions">
            <button class="btn btn-confirm-reject" (click)="confirmReject()" [disabled]="!rejectReason">Confirm Rejection</button>
            <button class="btn btn-cancel" (click)="cancelRejection()">Cancel</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .container { padding: 20px; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .btn-refresh { background-color: #007bff; color: white; }
    .loader-box { text-align: center; padding: 40px; background: #f8f9fa; border-radius: 8px; border: 1px dashed #ccc; }
    .approval-table { width: 100%; border-collapse: collapse; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }
    .approval-table th, .approval-table td { text-align: left; padding: 12px; border-bottom: 1px solid #ddd; }
    .approval-table th { background-color: #f8f9fa; font-weight: 600; }
    .btn { padding: 8px 16px; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; margin-right: 5px; transition: background 0.2s; }
    .btn-approve { background-color: #28a745; color: white; }
    .btn-reject { background-color: #dc3545; color: white; }
    .btn-cancel { background-color: #6c757d; color: white; }
    .no-data { text-align: center; padding: 20px; color: #666; font-style: italic; }
    .modal-overlay { position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal-content { background: white; padding: 24px; border-radius: 8px; width: 400px; box-shadow: 0 10px 25px rgba(0,0,0,0.2); }
    textarea { width: 100%; margin: 10px 0; padding: 10px; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; }
    .modal-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }
    .btn-confirm-reject { background-color: #dc3545; color: white; }
  `]
})
export class RequestListComponent implements OnInit {
  requests: ApprovalRequest[] = [];
  loading = true;
  selectedId: number | null = null;
  rejectReason = '';

  constructor(
    private approvalService: ApprovalService,
    private cdr: ChangeDetectorRef
  ) {}

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
        alert('Request Approved!');
        this.loadRequests();
      },
      error: (err) => {
        console.error('UI: Approve failed:', err);
        alert('Approve failed! Check console for details.');
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
          alert('Request Rejected!');
          this.selectedId = null;
          this.loadRequests();
        },
        error: (err) => {
          console.error('UI: Reject failed:', err);
          alert('Reject failed! Check console for details.');
        }
      });
    }
  }
}
