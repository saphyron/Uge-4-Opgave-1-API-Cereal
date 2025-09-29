CREATE TABLE dbo.Cereal (
    [name]     nvarchar(100)  NOT NULL,
    [mfr]      nchar(1)       NOT NULL,
    [type]     nchar(1)       NOT NULL,
    [calories] int            NULL,
    [protein]  int            NULL,
    [fat]      int            NULL,
    [sodium]   int            NULL,
    [fiber]    float          NULL,
    [carbo]    float          NULL,
    [sugars]   int            NULL,
    [potass]   int            NULL,
    [vitamins] int            NULL,
    [shelf]    int            NULL,
    [weight]   float          NULL,
    [cups]     float          NULL,
    [rating]   nvarchar(100)  NULL
);


/* 1) Tilføj IDENTITY-kolonne */
ALTER TABLE dbo.Cereal
    ADD Id INT IDENTITY(1,1) NOT NULL;

/* 2) Drop eksisterende PK (typisk 'PK_Cereal' på (name,mfr,type)) */
DECLARE @pkName sysname;
SELECT @pkName = kc.name
FROM sys.key_constraints kc
JOIN sys.tables t ON kc.parent_object_id = t.object_id
WHERE kc.type = 'PK' AND t.name = 'Cereal';
IF @pkName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'ALTER TABLE dbo.Cereal DROP CONSTRAINT [' + @pkName + N'];';
    EXEC sp_executesql @sql;
END

/* 3) Opret ny PK på Id */
ALTER TABLE dbo.Cereal
    ADD CONSTRAINT PK_Cereal_Id PRIMARY KEY CLUSTERED (Id);

/* 4) Sikr unikhed på (name,mfr,type) fremover */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'UQ_Cereal_name_mfr_type' AND object_id = OBJECT_ID('dbo.Cereal')
)
BEGIN
    ALTER TABLE dbo.Cereal
        ADD CONSTRAINT UQ_Cereal_name_mfr_type UNIQUE (name, mfr, type);
END
