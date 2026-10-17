/*
  NÂNG CẤP DATABASE NhaTroAnCu (bản ASP.NET MVC 5) ĐỂ DÙNG VỚI NHNhaTro (.NET 10)

  - Các bảng nghiệp vụ (Rooms, Contracts, Tenants, UtilityBills, IncomeExpenses...) giữ nguyên, không đổi dữ liệu.
  - Bảng tài khoản ASP.NET Identity 2 được bổ sung cột/bảng theo ASP.NET Core Identity (schema version 3).
    Tài khoản và mật khẩu cũ vẫn đăng nhập được: .NET 10 đọc được kiểu băm mật khẩu của Identity 2
    và tự băm lại theo chuẩn mới ở lần đăng nhập đầu tiên.
  - Ghi hai migration của EF Core vào __EFMigrationsHistory để ứng dụng không tạo lại bảng.
  - Cột cũ AspNetUsers.LockoutEndDateUtc được giữ lại (bản MVC 5 cũ vẫn chạy song song được).

  CÁCH CHẠY: SAO LƯU DATABASE TRƯỚC, rồi chạy file này bằng SSMS hoặc:
      sqlcmd -S <server> -d NhaTroAnCu -U <user> -I -i upgrade-from-mvc5.sql
  Script chạy lại nhiều lần không lỗi (bỏ qua bước đã làm).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

------------------------------------------------------------------ AspNetUsers
IF COL_LENGTH('dbo.AspNetUsers', 'NormalizedUserName') IS NULL ALTER TABLE dbo.AspNetUsers ADD NormalizedUserName nvarchar(256) NULL;
IF COL_LENGTH('dbo.AspNetUsers', 'NormalizedEmail') IS NULL ALTER TABLE dbo.AspNetUsers ADD NormalizedEmail nvarchar(256) NULL;
IF COL_LENGTH('dbo.AspNetUsers', 'ConcurrencyStamp') IS NULL ALTER TABLE dbo.AspNetUsers ADD ConcurrencyStamp nvarchar(max) NULL;
IF COL_LENGTH('dbo.AspNetUsers', 'LockoutEnd') IS NULL ALTER TABLE dbo.AspNetUsers ADD LockoutEnd datetimeoffset NULL;

EXEC(N'UPDATE dbo.AspNetUsers SET
    NormalizedUserName = ISNULL(NormalizedUserName, UPPER(UserName)),
    NormalizedEmail = ISNULL(NormalizedEmail, UPPER(Email)),
    ConcurrencyStamp = ISNULL(ConcurrencyStamp, CONVERT(nvarchar(36), NEWID())),
    SecurityStamp = ISNULL(SecurityStamp, CONVERT(nvarchar(36), NEWID()))');

IF COL_LENGTH('dbo.AspNetUsers', 'LockoutEndDateUtc') IS NOT NULL
    EXEC(N'UPDATE dbo.AspNetUsers SET LockoutEnd = TODATETIMEOFFSET(LockoutEndDateUtc, 0)
          WHERE LockoutEnd IS NULL AND LockoutEndDateUtc IS NOT NULL');

-- Index UserNameIndex của Identity 2 nằm trên cột UserName; Identity Core cần trên NormalizedUserName
IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE i.object_id = OBJECT_ID('dbo.AspNetUsers') AND i.name = 'UserNameIndex' AND c.name = 'UserName')
    DROP INDEX UserNameIndex ON dbo.AspNetUsers;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetUsers') AND name = 'UserNameIndex')
    EXEC(N'CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetUsers') AND name = 'EmailIndex')
    EXEC(N'CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail)');

------------------------------------------------------------------ AspNetRoles
IF COL_LENGTH('dbo.AspNetRoles', 'NormalizedName') IS NULL ALTER TABLE dbo.AspNetRoles ADD NormalizedName nvarchar(256) NULL;
IF COL_LENGTH('dbo.AspNetRoles', 'ConcurrencyStamp') IS NULL ALTER TABLE dbo.AspNetRoles ADD ConcurrencyStamp nvarchar(max) NULL;
EXEC(N'UPDATE dbo.AspNetRoles SET
    NormalizedName = ISNULL(NormalizedName, UPPER(Name)),
    ConcurrencyStamp = ISNULL(ConcurrencyStamp, CONVERT(nvarchar(36), NEWID()))');

IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE i.object_id = OBJECT_ID('dbo.AspNetRoles') AND i.name = 'RoleNameIndex' AND c.name = 'Name')
    DROP INDEX RoleNameIndex ON dbo.AspNetRoles;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetRoles') AND name = 'RoleNameIndex')
    EXEC(N'CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL');

------------------------------------------------------------------ AspNetUserLogins
IF COL_LENGTH('dbo.AspNetUserLogins', 'ProviderDisplayName') IS NULL ALTER TABLE dbo.AspNetUserLogins ADD ProviderDisplayName nvarchar(max) NULL;

-- Khóa chính Identity 2 gồm (LoginProvider, ProviderKey, UserId); Identity Core dùng (LoginProvider, ProviderKey)
DECLARE @pk sysname, @pkCols int;
SELECT @pk = kc.name, @pkCols = (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id)
FROM sys.key_constraints kc WHERE kc.parent_object_id = OBJECT_ID('dbo.AspNetUserLogins') AND kc.type = 'PK';
IF @pk IS NOT NULL AND @pkCols <> 2
BEGIN
    EXEC(N'ALTER TABLE dbo.AspNetUserLogins DROP CONSTRAINT [' + @pk + N']');
    SET @pk = NULL;
END
IF @pk IS NULL
    ALTER TABLE dbo.AspNetUserLogins ADD CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey);

------------------------------------------------------------------ Bảng mới của Identity Core
-- Khóa người dùng/vai trò giữ nvarchar(128) cho khớp kiểu cột Id của bảng cũ (khóa ngoại phải cùng kiểu).
IF OBJECT_ID('dbo.AspNetRoleClaims') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims (
        Id int NOT NULL IDENTITY,
        RoleId nvarchar(128) NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id),
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
END

IF OBJECT_ID('dbo.AspNetUserTokens') IS NULL
    CREATE TABLE dbo.AspNetUserTokens (
        UserId nvarchar(128) NOT NULL,
        LoginProvider nvarchar(128) NOT NULL,
        Name nvarchar(128) NOT NULL,
        Value nvarchar(max) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

IF OBJECT_ID('dbo.AspNetUserPasskeys') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserPasskeys (
        CredentialId varbinary(1024) NOT NULL,
        UserId nvarchar(128) NOT NULL,
        Data nvarchar(max) NOT NULL,
        CONSTRAINT PK_AspNetUserPasskeys PRIMARY KEY (CredentialId),
        CONSTRAINT FK_AspNetUserPasskeys_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserPasskeys_UserId ON dbo.AspNetUserPasskeys (UserId);
END

------------------------------------------------------------------ Lịch sử migration EF Core
IF OBJECT_ID('dbo.__EFMigrationsHistory') IS NULL
    CREATE TABLE dbo.__EFMigrationsHistory (
        MigrationId nvarchar(150) NOT NULL,
        ProductVersion nvarchar(32) NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
    );
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '00000000000000_CreateIdentitySchema')
    INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('00000000000000_CreateIdentitySchema', '10.0.13');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261017054055_NhaTroSchema')
    INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261017054055_NhaTroSchema', '10.0.13');

------------------------------------------------------------------ Danh mục thu chi hệ thống
IF NOT EXISTS (SELECT 1 FROM dbo.IncomeExpenseCategories WHERE Name = N'Thu tiền phòng' AND Type = 'Income')
    INSERT dbo.IncomeExpenseCategories (Name, Type, IsSystem, IsActive, CreatedAt) VALUES (N'Thu tiền phòng', 'Income', 1, 1, GETDATE());
IF NOT EXISTS (SELECT 1 FROM dbo.IncomeExpenseCategories WHERE Name = N'Trả tiền cọc' AND Type = 'Expense')
    INSERT dbo.IncomeExpenseCategories (Name, Type, IsSystem, IsActive, CreatedAt) VALUES (N'Trả tiền cọc', 'Expense', 1, 1, GETDATE());

COMMIT TRANSACTION;
PRINT N'Đã nâng cấp database xong.';
