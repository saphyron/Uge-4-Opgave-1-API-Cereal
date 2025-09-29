/* 1) Opret SQL Login på serveren (kun hvis du bruger SQL Server Authentication) */
CREATE LOGIN CerealApiCrudUser
WITH PASSWORD = 'S3cure!Pass',
     CHECK_POLICY = ON,        -- Windows password policy
     CHECK_EXPIRATION = ON;    -- kræv password-udløb

/* 2) Skift til din database */
USE [Cereal API];
GO

/* 3) Opret database-bruger der mapper til login’et */
CREATE USER app_user FOR LOGIN CerealApiCrudUser
WITH DEFAULT_SCHEMA = dbo;     -- eller opret eget schema til appen

/* 4) Giv CRUD-rettigheder (mindste nødvendige) */

/* Nem variant (roller): læse/skriv alt i DB’en */
EXEC sp_addrolemember N'db_datareader', N'app_user'; -- SELECT
EXEC sp_addrolemember N'db_datawriter', N'app_user'; -- INSERT/UPDATE/DELETE

/* (Valgfrit) Tillad EXECUTE af stored procedures */
GRANT EXECUTE TO app_user;

/* 5) (Alternativ) Kun CRUD på et bestemt schema (mere stramt) */
/*
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO app_user;
GRANT EXECUTE ON SCHEMA::dbo TO app_user;
*/

/* 6) (Valgfrit) Opret en rolle og tildel rettigheder til rollen i stedet */
-- CREATE ROLE crud_role;
-- GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO crud_role;
-- EXEC sp_addrolemember N'crud_role', N'app_user';
