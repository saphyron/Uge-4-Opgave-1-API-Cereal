select * from dbo.Cereal
delete from dbo.Cereal where mfr = 'y'
select * from dbo.Cereal


ALTER TABLE dbo.Cereal DROP CONSTRAINT PK_Cereal_Id;
ALTER TABLE dbo.Cereal DROP COLUMN Id;

ALTER TABLE dbo.Cereal ADD Id INT IDENTITY(1,1) NOT NULL;
ALTER TABLE dbo.Cereal ADD CONSTRAINT PK_Cereal_Id PRIMARY KEY (Id);



/* === Kør i den database du vil tjekke, fx: ===
USE [CerealDb];
GO
*/

SET NOCOUNT ON;

-- 1) Brugere i databasen
SELECT
  dp.name                   AS user_name,
  dp.type_desc              AS user_type,              -- SQL_USER, WINDOWS_USER, EXTERNAL_USER, WINDOWS_GROUP
  dp.authentication_type_desc,
  dp.default_schema_name,
  dp.create_date,
  dp.modify_date
FROM sys.database_principals dp
WHERE dp.type IN ('S','U','E','G')                     -- S=SQL, U=Windows, E=External, G=Windows group
  AND dp.name NOT IN ('guest','sys','INFORMATION_SCHEMA')
ORDER BY dp.name;

-- 2) Rollemedlemskaber (hvilke DB-roller hver bruger er i)
SELECT
  m.name  AS user_name,
  r.name  AS role_name
FROM sys.database_role_members drm
JOIN sys.database_principals r ON r.principal_id  = drm.role_principal_id
JOIN sys.database_principals m ON m.principal_id  = drm.member_principal_id
ORDER BY m.name, r.name;

-- 3) Eksplicitte tilladelser (GRANT/DENY) i databasen
SELECT
  grantee.name              AS user_name,
  dp.permission_name,
  dp.state_desc             AS permission_state,       -- GRANT / DENY / GRANT_WITH_GRANT_OPTION
  dp.class_desc,                                      -- DATABASE / SCHEMA / OBJECT / COLUMN
  OBJECT_SCHEMA_NAME(dp.major_id) AS schema_name,
  OBJECT_NAME(dp.major_id)       AS object_name,
  c.name                   AS column_name
FROM sys.database_permissions dp
JOIN sys.database_principals grantee ON grantee.principal_id = dp.grantee_principal_id
LEFT JOIN sys.columns c ON c.object_id = dp.major_id AND c.column_id = dp.minor_id
WHERE grantee.type IN ('S','U','E','G')
ORDER BY user_name, dp.class_desc, schema_name, object_name, dp.permission_name;
