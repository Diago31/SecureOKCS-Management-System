using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("roles")]
    public class Role
    {
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }

        [Required]
        [Column("role_name")]
        [StringLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [Column("role_description")]
        [StringLength(255)]
        public string? RoleDescription { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public ICollection<User> Users { get; set; }
            = new List<User>();
    }
}