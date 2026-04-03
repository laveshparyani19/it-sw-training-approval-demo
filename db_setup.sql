USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ApprovalDemoDb')
BEGIN
    CREATE DATABASE ApprovalDemoDb;
END
GO

USE ApprovalDemoDb;
GO

-- Create ApprovalRequest Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApprovalRequest')
BEGIN
    CREATE TABLE ApprovalRequest (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(MAX) NOT NULL,
        RequestedBy NVARCHAR(MAX) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Rejected
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        DecisionBy NVARCHAR(MAX) NULL,
        DecisionAt DATETIME2 NULL,
        RejectReason NVARCHAR(MAX) NULL
    );
END
GO

-- Stored Procedure: Create Approval Request
CREATE OR ALTER PROCEDURE SP_ApprovalRequest_Create
    @Title NVARCHAR(MAX),
    @RequestedBy NVARCHAR(MAX)
AS
BEGIN
    INSERT INTO ApprovalRequest (Title, RequestedBy, Status, CreatedAt)
    VALUES (@Title, @RequestedBy, 0, GETDATE());
    
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

-- Stored Procedure: Get Pending Requests
CREATE OR ALTER PROCEDURE SP_ApprovalRequest_GetPending
AS
BEGIN
    SELECT * FROM ApprovalRequest WHERE Status = 0 ORDER BY CreatedAt DESC;
END
GO

-- Stored Procedure: Approve Request
CREATE OR ALTER PROCEDURE SP_ApprovalRequest_Approve
    @Id INT,
    @DecisionBy NVARCHAR(MAX)
AS
BEGIN
    UPDATE ApprovalRequest
    SET Status = 1,
        DecisionBy = @DecisionBy,
        DecisionAt = GETDATE()
    WHERE Id = @Id;
END
GO

-- Stored Procedure: Reject Request
CREATE OR ALTER PROCEDURE SP_ApprovalRequest_Reject
    @Id INT,
    @DecisionBy NVARCHAR(MAX),
    @RejectReason NVARCHAR(MAX)
AS
BEGIN
    UPDATE ApprovalRequest
    SET Status = 2,
        DecisionBy = @DecisionBy,
        DecisionAt = GETDATE(),
        RejectReason = @RejectReason
    WHERE Id = @Id;
END
GO

-- Stored Procedure: Get By Id
CREATE OR ALTER PROCEDURE SP_ApprovalRequest_GetById
    @Id INT
AS
BEGIN
    SELECT * FROM ApprovalRequest WHERE Id = @Id;
END
GO
