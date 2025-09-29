namespace CerealAPI.Models
{
    // Bruges af POST /products
    public sealed class ProductInsertDto
    {
        public int? id { get; set; }               
        public string name { get; set; } = "";
        public string mfr { get; set; } = "";      
        public string type { get; set; } = "";    
        public int? calories { get; set; }
        public int? protein { get; set; }
        public int? fat { get; set; }
        public int? sodium { get; set; }
        public double? fiber { get; set; }
        public double? carbo { get; set; }
        public int? sugars { get; set; }
        public int? potass { get; set; }
        public int? vitamins { get; set; }
        public int? shelf { get; set; }
        public double? weight { get; set; }
        public double? cups { get; set; }
        public string? rating { get; set; }
    }
}
