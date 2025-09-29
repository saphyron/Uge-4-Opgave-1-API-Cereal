using CerealAPI.Models;
using Dapper;

namespace CerealAPI.Endpoints.CRUD
{
    public static class CRUDEndpoints
    {
        public static IEndpointRouteBuilder MapCrudEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/cereals");

            // GET /cereals
            group.MapGet("", async (CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                var sql = @"SELECT name,mfr,type,calories,protein,fat,sodium,fiber,carbo,sugars,potass,vitamins,shelf,weight,cups,rating
                            FROM dbo.Cereal
                            ORDER BY name";
                using var conn = cereal.Create();
                var rows = await conn.QueryAsync<Cereal>(sql, new { });
                return Results.Ok(rows);
            })
            .WithSummary("Hent alle cereals")
            .WithDescription("Returnerer alle rækker fra dbo.Cereal i navneorden.");

            group.MapGet("/top/{take:int}", async (int take, CerealAPI.Data.SqlConnectionCeral cereal) =>
            {
                // defensiv validering (undgå minus/0 og urimeligt høje værdier)
                if (take < 1) take = 1;
                if (take > 10000) take = 10000; // juster grænsen efter behov

                const string sql = @"
                    SELECT TOP (@Take) name,mfr,type,calories,protein,fat,sodium,fiber,carbo,sugars,potass,vitamins,shelf,weight,cups,rating
                    FROM dbo.Cereal
                    ORDER BY name;";

                using var conn = cereal.Create();
                var rows = await conn.QueryAsync<Cereal>(sql, new { Take = take });
                return Results.Ok(rows);
            })
            .WithSummary("Hent top N cereals")
            .WithDescription("Returnerer de første N rækker fra dbo.Cereal i navneorden. Bruges som letvægtsliste.");


            // POST /cereals 
            group.MapPost("", async (Cereal cereal, CerealAPI.Data.SqlConnectionCeral cereals) =>
            {
                const string sql = @"
                    INSERT INTO dbo.Cereal
                    (name,mfr,type,calories,protein,fat,sodium,fiber,carbo,sugars,potass,vitamins,shelf,weight,cups,rating)
                    VALUES
                    (@name,@mfr,@type,@calories,@protein,@fat,@sodium,@fiber,@carbo,@sugars,
                    @potass,@vitamins,@shelf,@weight,@cups,@rating);";
                using var conn = cereals.Create();
                var affected = await conn.ExecuteAsync(sql, cereal);
                return Results.Ok(new { inserted = affected });
            });

            // PUT /cereals/{name}/{mfr}/{type} | Navne som 100% Bran skal URL-encodes: PUT /cereals/100%25%20Bran/K/C 
            group.MapPut("/{name}/{mfr}/{type}", async (string name, string mfr, string type, Cereal update, CerealAPI.Data.SqlConnectionCeral cereals) =>
            {
                const string sql = @"
                    UPDATE dbo.Cereal
                    SET
                        calories = @calories,
                        protein  = @protein,
                        fat      = @fat,
                        sodium   = @sodium,
                        fiber    = @fiber,
                        carbo    = @carbo,
                        sugars   = @sugars,
                        potass   = @potass,
                        vitamins = @vitamins,
                        shelf    = @shelf,
                        weight   = @weight,
                        cups     = @cups,
                        rating   = @rating
                    WHERE name = @key_name AND mfr = @key_mfr AND type = @key_type;";

                using var conn = cereals.Create();
                var affected = await conn.ExecuteAsync(sql, new
                {
                    key_name = name,
                    key_mfr = mfr,
                    key_type = type,
                    update.calories,
                    update.protein,
                    update.fat,
                    update.sodium,
                    update.fiber,
                    update.carbo,
                    update.sugars,
                    update.potass,
                    update.vitamins,
                    update.shelf,
                    update.weight,
                    update.cups,
                    update.rating
                });

                return affected == 0 ? Results.NotFound(new { message = "Row not found." }) 
                                     : Results.Ok(new { updated = affected });
            });

            // DELETE /cereals/{name}/{mfr}/{type}
            group.MapDelete("/{name}/{mfr}/{type}", async (string name, string mfr, string type, CerealAPI.Data.SqlConnectionCeral cereals) =>
            {
                const string sql = @"DELETE FROM dbo.Cereal WHERE name=@name AND mfr=@mfr AND type=@type;";
                using var conn = cereals.Create();
                var affected = await conn.ExecuteAsync(sql, new { name, mfr, type });
                return affected == 0 ? Results.NotFound(new { message = "Row not found." })
                                     : Results.Ok(new { deleted = affected });
            });

            return app;
        }
    }
}
