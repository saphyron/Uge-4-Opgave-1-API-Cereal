IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Users' AND schema_id=SCHEMA_ID('dbo'))
BEGIN
  CREATE TABLE dbo.Users (
      Id           INT IDENTITY(1,1) PRIMARY KEY,
      Username     NVARCHAR(64)  NOT NULL UNIQUE,
      PasswordHash NVARCHAR(200) NOT NULL,
      Role         NVARCHAR(32)  NOT NULL DEFAULT 'user',
      CreatedAt    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
  );
END
