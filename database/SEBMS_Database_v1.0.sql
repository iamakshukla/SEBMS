/* =====================================================================
   SEBMS - Database script v1.0 (DRAFT - not yet run on SQL Server)
   Creates: database, stand-in user table, Categories, Transactions,
            Budgets, constraints, 5 indexes, 13 default categories.
   Run ONCE on a fresh LocalDB / SQL Server Express instance.
   In the real app, ASP.NET Core Identity creates the user table via
   EF Core migrations (Phase 5); dbo.AspNetUsers here is a stand-in.
   ===================================================================== */
USE master;
GO
CREATE DATABASE SEBMS;
GO
USE SEBMS;
GO

/* ---------- Stand-in user table (Identity replaces this later) ---------- */
CREATE TABLE dbo.AspNetUsers (
    Id          NVARCHAR(450) NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
    UserName    NVARCHAR(256) NULL,
    Email       NVARCHAR(256) NULL,
    DisplayName NVARCHAR(100) NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

/* ---------- Categories ---------- */
CREATE TABLE dbo.Categories (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    UserId    NVARCHAR(450)     NULL,   -- NULL for shared default categories
    Name      NVARCHAR(50)      NOT NULL,
    [Type]    VARCHAR(7)        NOT NULL,
    IsDefault BIT               NOT NULL CONSTRAINT DF_Categories_IsDefault DEFAULT 0,
    CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id),
    CONSTRAINT CK_Categories_Type CHECK ([Type] IN ('Income','Expense')),
    CONSTRAINT CK_Categories_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    -- default rows have no UserId; custom rows must have one
    CONSTRAINT CK_Categories_DefaultOwner CHECK (
        (IsDefault = 1 AND UserId IS NULL) OR (IsDefault = 0 AND UserId IS NOT NULL))
);
GO

/* Index 5 of 5: loading a user's categories; no duplicate names per owner and type */
CREATE UNIQUE INDEX UX_Categories_User_Name_Type
    ON dbo.Categories (UserId, Name, [Type]);
GO

/* ---------- Transactions ---------- */
CREATE TABLE dbo.Transactions (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY,
    UserId          NVARCHAR(450)     NOT NULL,
    CategoryId      INT               NOT NULL,
    [Type]          VARCHAR(7)        NOT NULL,
    Amount          DECIMAL(12,2)     NOT NULL,
    TransactionDate DATE              NOT NULL,     -- future dates allowed
    Note            NVARCHAR(500)     NULL,
    CurrencyCode    CHAR(3)           NOT NULL CONSTRAINT DF_Transactions_Currency DEFAULT 'INR',
    IsDeleted       BIT               NOT NULL CONSTRAINT DF_Transactions_IsDeleted DEFAULT 0,
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Transactions_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2         NULL,
    CONSTRAINT FK_Transactions_Users      FOREIGN KEY (UserId)     REFERENCES dbo.AspNetUsers(Id),
    -- no cascade: a category that has transactions cannot be deleted
    CONSTRAINT FK_Transactions_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id),
    CONSTRAINT CK_Transactions_Type   CHECK ([Type] IN ('Income','Expense')),
    CONSTRAINT CK_Transactions_Amount CHECK (Amount > 0 AND Amount <= 9999999999),
    CONSTRAINT CK_Transactions_Currency CHECK (CurrencyCode COLLATE Latin1_General_BIN LIKE '[A-Z][A-Z][A-Z]')
    -- Type must match the category's type: checked in the services layer
);
GO

/* Index 1 of 5: transaction list, filters, recent transactions */
CREATE INDEX IX_Transactions_User_Date
    ON dbo.Transactions (UserId, TransactionDate DESC)
    WHERE IsDeleted = 0;
GO
/* Index 2 of 5: budget vs actual, category breakdown */
CREATE INDEX IX_Transactions_User_Category_Date
    ON dbo.Transactions (UserId, CategoryId, TransactionDate)
    INCLUDE (Amount, [Type])
    WHERE IsDeleted = 0;
GO

/* ---------- Budgets ---------- */
CREATE TABLE dbo.Budgets (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Budgets PRIMARY KEY,
    UserId      NVARCHAR(450)     NOT NULL,
    CategoryId  INT               NOT NULL,
    BudgetYear  SMALLINT          NOT NULL,
    BudgetMonth TINYINT           NOT NULL,
    Amount      DECIMAL(12,2)     NOT NULL,
    CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_Budgets_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2         NULL,
    CONSTRAINT FK_Budgets_Users      FOREIGN KEY (UserId)     REFERENCES dbo.AspNetUsers(Id),
    -- deleting a category deletes its budgets
    CONSTRAINT FK_Budgets_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Budgets_Month  CHECK (BudgetMonth BETWEEN 1 AND 12),
    CONSTRAINT CK_Budgets_Amount CHECK (Amount > 0)
);
GO

/* Index 3 of 5: loading one month's budgets */
CREATE INDEX IX_Budgets_User_Year_Month
    ON dbo.Budgets (UserId, BudgetYear, BudgetMonth);
GO
/* Index 4 of 5: one budget per user, category, year and month */
CREATE UNIQUE INDEX UX_Budgets_User_Category_Year_Month
    ON dbo.Budgets (UserId, CategoryId, BudgetYear, BudgetMonth);
GO

/* ---------- Default categories (13 shared rows, no owner) ---------- */
INSERT INTO dbo.Categories (UserId, Name, [Type], IsDefault) VALUES
    (NULL, N'Food and Dining',   'Expense', 1),
    (NULL, N'Transport',         'Expense', 1),
    (NULL, N'Rent and Housing',  'Expense', 1),
    (NULL, N'Utilities',         'Expense', 1),
    (NULL, N'Shopping',          'Expense', 1),
    (NULL, N'Health',            'Expense', 1),
    (NULL, N'Education',         'Expense', 1),
    (NULL, N'Entertainment',     'Expense', 1),
    (NULL, N'Other Expense',     'Expense', 1),
    (NULL, N'Salary',            'Income',  1),
    (NULL, N'Business',          'Income',  1),
    (NULL, N'Gift',              'Income',  1),
    (NULL, N'Other Income',      'Income',  1);
GO

/* ---------- Check: should return 13 ---------- */
SELECT COUNT(*) AS DefaultCategories FROM dbo.Categories WHERE IsDefault = 1;
GO
