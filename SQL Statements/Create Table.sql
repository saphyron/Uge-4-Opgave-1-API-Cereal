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
