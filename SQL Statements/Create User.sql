/* Kør som sysadmin i SSMS. Idempotent (kan køres flere gange). */
USE master;
GO

-- 0) Server-login (SQL authentication)
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'CerealApiCrudUser')
BEGIN
  CREATE LOGIN [CerealApiCrudUser]
  WITH PASSWORD = 'S3cure!Pass',
       CHECK_POLICY = ON,       -- password policy
       CHECK_EXPIRATION = OFF,  -- undgå udløb i dev
       DEFAULT_DATABASE = [CerealDb];
END
ELSE
BEGIN
  -- Sørg for rimelige properties, hvis login allerede fandtes:
  ALTER LOGIN [CerealApiCrudUser]
    WITH CHECK_POLICY = ON,
         CHECK_EXPIRATION = OFF,
         DEFAULT_DATABASE = [CerealDb];
END
GO

-- 1) Database
IF DB_ID(N'CerealDb') IS NULL
BEGIN
  CREATE DATABASE [CerealDb];
END
GO

-- 2) Skema + tabel
USE [CerealDb];
GO

IF OBJECT_ID(N'dbo.Cereal','U') IS NULL
BEGIN
  CREATE TABLE dbo.Cereal
  (
    name     NVARCHAR(100) NOT NULL,
    mfr      NVARCHAR(10)  NOT NULL,
    type     NVARCHAR(10)  NOT NULL,
    calories INT     NULL,
    protein  INT     NULL,
    fat      INT     NULL,
    sodium   INT     NULL,
    fiber    FLOAT   NULL,
    carbo    FLOAT   NULL,
    sugars   INT     NULL,
    potass   INT     NULL,
    vitamins INT     NULL,
    shelf    INT     NULL,
    weight   FLOAT   NULL,
    cups     FLOAT   NULL,
    rating   NVARCHAR(50) NULL,  -- VIGTIGT: STRING i DB
    CONSTRAINT PK_Cereal PRIMARY KEY (name, mfr, type)
  );
END
GO

-- 3) DB-bruger map til login
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'CerealApiCrudUser')
BEGIN
  CREATE USER [CerealApiCrudUser] FOR LOGIN [CerealApiCrudUser]
    WITH DEFAULT_SCHEMA = dbo;
END
GO

-- 4) Rettigheder (snævre CRUD på tabellen)
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Cereal TO [CerealApiCrudUser];
-- SqlBulkCopy behøver INSERT på tabellen (dækket ovenfor).
GO
