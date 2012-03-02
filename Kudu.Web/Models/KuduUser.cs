using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class KuduUser
    {
        [Key]
        public string Username { get; set; }
        public string Password { get; set; }
    }
}