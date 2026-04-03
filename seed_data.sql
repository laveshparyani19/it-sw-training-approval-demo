USE ApprovalDemoDb;
GO

-- Seed some pending requests for testing
INSERT INTO ApprovalRequest (Title, RequestedBy, Status, CreatedAt)
VALUES 
('Vacation Request - John Doe', 'John Doe', 0, GETDATE()),
('Hardware Purchase - Jane Smith', 'Jane Smith', 0, DATEADD(hour, -2, GETDATE())),
('Conference Registration - Bob Wilson', 'Bob Wilson', 0, DATEADD(day, -1, GETDATE())),
('New Software License - Alice Brown', 'Alice Brown', 0, GETDATE());

-- Verify the data
SELECT * FROM ApprovalRequest WHERE Status = 0;
GO
