using CerealAPI.Models;
using Dapper;
using System.Text;
using Microsoft.Data.SqlClient;

namespace CerealAPI.Endpoints.Products
{
    public static class ProductsEndpoints
    {
        public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/products");

            // ---- GET /products (alle eller filtreret pr. felt)
            group.MapGet("", async ([AsParameters] ProductQuery q, CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                // Hvis manufacturer er sat (fuldt navn) og mfr ikke er sat, så map til kode
                var mfrCode = !string.IsNullOrWhiteSpace(q.manufacturer) && string.IsNullOrWhiteSpace(q.mfr)
                    ? NormalizeManufacturer(q.manufacturer)
                    : q.mfr;

                var sb = new StringBuilder(@"
SELECT *
FROM dbo.Cereal
WHERE 1=1
");

                var p = new DynamicParameters();

                // Hjælpere til at tilføje betingelser, kun når feltet er sat
                void Eq<T>(string col, string paramName, T? val) where T : struct
                {
                    if (val.HasValue)
                    {
                        sb.AppendLine($"  AND {col} = @{paramName}");
                        p.Add(paramName, val.Value);
                    }
                }
                void EqStr(string col, string paramName, string? val, bool caseInsensitive = true)
                {
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        if (caseInsensitive)
                        {
                            sb.AppendLine($"  AND UPPER({col}) = UPPER(@{paramName})");
                        }
                        else
                        {
                            sb.AppendLine($"  AND {col} = @{paramName}");
                        }
                        p.Add(paramName, val);
                    }
                }
                void LikeCI(string col, string paramName, string? val)
                {
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        sb.AppendLine($"  AND UPPER({col}) LIKE UPPER(@{paramName})");
                        p.Add(paramName, $"%{val}%");
                    }
                }

                // ID og strenge
                if (q.id.HasValue)
                {
                    sb.AppendLine("  AND Id = @id");
                    p.Add("id", q.id.Value);
                }
                EqStr("name", "name", q.name);
                LikeCI("name", "nameLike", q.nameLike);
                EqStr("mfr", "mfr", mfrCode);
                EqStr("type", "type", q.type);
                EqStr("rating", "rating", q.rating, caseInsensitive: false); // rating er ofte et frit tekstfelt

                // talfelter (præcise matches)
                Eq("calories", "calories", q.calories);
                Eq("protein",  "protein",  q.protein);
                Eq("fat",      "fat",      q.fat);
                Eq("sodium",   "sodium",   q.sodium);
                if (q.fiber.HasValue)   { sb.AppendLine("  AND fiber  = @fiber");  p.Add("fiber",  q.fiber); }
                if (q.carbo.HasValue)   { sb.AppendLine("  AND carbo  = @carbo");  p.Add("carbo",  q.carbo); }
                Eq("sugars",   "sugars",   q.sugars);
                Eq("potass",   "potass",   q.potass);
                Eq("vitamins", "vitamins", q.vitamins);
                Eq("shelf",    "shelf",    q.shelf);
                if (q.weight.HasValue)  { sb.AppendLine("  AND weight = @weight"); p.Add("weight", q.weight); }
                if (q.cups.HasValue)    { sb.AppendLine("  AND cups   = @cups");   p.Add("cups",   q.cups); }

                sb.AppendLine("ORDER BY name, Id;");

                using var conn = cereal.Create();
                var rows = await conn.QueryAsync<Cereal>(sb.ToString(), p);
                return Results.Ok(rows);
            })
            .WithSummary("Hent produkter (filtrér på vilkårlige felter)")
            .WithDescription("Brug querystring til at matche præcist på alle felter (fx ?calories=120&mfr=K&nameLike=bran).");

            // ---- GET /products/{id}
            group.MapGet("/{id:int}", async (int id, CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                const string sql = @"
SELECT *
FROM dbo.Cereal WHERE Id=@id;";
                using var conn = cereal.Create();
                var row = await conn.QuerySingleOrDefaultAsync<Cereal>(sql, new { id });
                return row is null ? Results.NotFound(new { message = "Product not found." }) : Results.Ok(row);
            })
            .WithSummary("Hent produkt via ID");

            // ---- DELETE /products/{id}
            group.MapDelete("/{id:int}", async (int id, CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                const string sql = "DELETE FROM dbo.Cereal WHERE Id=@id;";
                using var conn = cereal.Create();
                var affected = await conn.ExecuteAsync(sql, new { id });
                return affected == 0 ? Results.NotFound(new { message = "Product not found." }) 
                                     : Results.Ok(new { deleted = affected });
            })
            .WithSummary("Slet produkt via ID");

            // ---- POST /products  (create/update afhængigt af id)
            //  - Hvis body.id == null: Opret NYT (server vælger ID)
            //  - Hvis body.id  != null:
            //      * Findes ID -> UPDATE
            //      * Findes ID ikke -> 400 (ID må ikke vælges manuelt ved oprettelse)
            group.MapPost("", async (ProductInsertDto dto, CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                using var conn = cereal.Create();

                if (dto.id is null)
                {
                    // CREATE
                    const string insertSql = @"
INSERT INTO dbo.Cereal
(name,mfr,type,calories,protein,fat,sodium,fiber,carbo,sugars,potass,vitamins,shelf,weight,cups,rating)
VALUES
(@name,@mfr,@type,@calories,@protein,@fat,@sodium,@fiber,@carbo,@sugars,@potass,@vitamins,@shelf,@weight,@cups,@rating);
SELECT CAST(SCOPE_IDENTITY() AS int);";
                    try
                    {
                        var newId = await conn.ExecuteScalarAsync<int>(insertSql, dto);
                        return Results.Created($"/products/{newId}", new { id = newId });
                    }
                    catch (Microsoft.Data.SqlClient.SqlException ex) when (IsUniqueConstraint(ex))
                    {
                        // name/mfr/type unik konflikt
                        return Results.Conflict(new { message = "A product with the same (name,mfr,type) already exists." });
                    }
                }
                else
                {
                    // UPDATE hvis findes; ellers 400 (må ikke vælge ID manuelt ved oprettelse)
                    const string existsSql = "SELECT 1 FROM dbo.Cereal WHERE Id=@id;";
                    var exists = await conn.ExecuteScalarAsync<int?>(existsSql, new { id = dto.id });
                    if (exists is null)
                    {
                        return Results.BadRequest(new { message = "ID was provided but does not exist. IDs cannot be chosen manually." });
                    }

                    const string updateSql = @"
UPDATE dbo.Cereal SET
    name=@name, mfr=@mfr, type=@type,
    calories=@calories, protein=@protein, fat=@fat, sodium=@sodium,
    fiber=@fiber, carbo=@carbo, sugars=@sugars, potass=@potass, vitamins=@vitamins,
    shelf=@shelf, weight=@weight, cups=@cups, rating=@rating
WHERE Id=@id;";
                    var affected = await conn.ExecuteAsync(updateSql, dto);
                    return Results.Ok(new { updated = affected });
                }
            })
            .WithSummary("Opret/Opdater produkt")
            .WithDescription("Hvis id mangler → opret; hvis id findes → opdater; hvis id er angivet men ikke findes → 400.");

            return app;
        }

        // --- Helpers ---
        private static bool IsUniqueConstraint(Microsoft.Data.SqlClient.SqlException ex)
            => ex.Number == 2627 || ex.Number == 2601; // duplicate key

        /// <summary>
        /// Tillad brug af fulde producentnavne (case-insensitivt) eller direkte mfr-kode (K,G,P,...).
        /// Returnerer mfr-kode eller null hvis ikke angivet.
        /// </summary>
        private static string? NormalizeManufacturer(string? manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer)) return null;
            var s = manufacturer.Trim().ToUpperInvariant();

            // hvis allerede enkeltbogstav
            if (s.Length == 1) return s;

            // simple mapping (kan udvides)
            return s switch
            {
                "KELLOGGS" or "KELLOGG'S" or "KELLOGG" => "K",
                "GENERAL MILLS"                        => "G",
                "POST"                                  => "P",
                "NABISCO"                               => "N",
                "QUAKER"                                => "Q",
                "RALSTON"                               => "R",
                "ALBERS"                                => "A",
                _                                       => s // fallback: prøv som indtastet (hvis nogen bruger custom koder)
            };
        }
    }
}
