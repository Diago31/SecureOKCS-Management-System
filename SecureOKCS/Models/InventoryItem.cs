using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("inventory_items")]
    public class InventoryItem
    {
        [Key]
        [Column("inventory_id")]
        public int InventoryId { get; set; }

        [Required]
        [Column("item_code")]
        [StringLength(50)]
        public string ItemCode { get; set; }
            = string.Empty;

        [Required]
        [Column("item_name")]
        [StringLength(150)]
        public string ItemName { get; set; }
            = string.Empty;

        [Column("category")]
        [StringLength(100)]
        public string? Category { get; set; }

        [Column("description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Column("barcode")]
        [StringLength(100)]
        public string? Barcode { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("minimum_stock")]
        public int MinimumStock { get; set; }

        [Column("unit_price",
            TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public ICollection<InventoryTransaction>
            InventoryTransactions
        { get; set; }
            = new List<InventoryTransaction>();
    }
}