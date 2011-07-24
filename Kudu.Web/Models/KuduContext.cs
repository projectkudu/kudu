using System.Data.Entity;

namespace Kudu.Web.Models {
    public class KuduContext : DbContext {
        public DbSet<Application> Applications { get; set; }
    }
}