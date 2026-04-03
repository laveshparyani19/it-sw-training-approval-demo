USE ApprovalDemoDb;
GO

-- 1. DELETE ALL EXISTING RECORDS
DELETE FROM ApprovalRequest;

-- 2. RESET IDENTITY COLUMN (IDs will start from 1 again)
DBCC CHECKIDENT ('ApprovalRequest', RESEED, 0);

-- 3. INSERT FRESH PENDING RECORDS (Status = 0)
INSERT INTO ApprovalRequest (Title, RequestedBy, Status, CreatedAt)
VALUES 
('Emergency Server Maintenance Request', 'System Admin', 0, GETDATE()),
('New Project Workspace Allocation', 'Sarah Manager', 0, DATEADD(hour, -1, GETDATE())),
('Budget Approval for Q2 Marketing', 'Mike Finance', 0, DATEADD(hour, -2, GETDATE())),
('Office Supplies Requisition', 'Emma Admin', 0, GETDATE()),
('GitHub Enterprise License Renewal', 'DevOps Team', 0, DATEADD(day, -1, GETDATE()));

-- 4. VERIFY THE RECORDS
SELECT * FROM ApprovalRequest WHERE Status = 0;
GO
