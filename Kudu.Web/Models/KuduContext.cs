using System.Data.Entity;

namespace Kudu.Web.Models
{
    public class KuduContext : DbContext
    {
        public KuduContext()
            : base("Kudu")
        {
        }

        public DbSet<KuduApplication> Applications { get; set; }
    }
}