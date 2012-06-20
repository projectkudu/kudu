using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        [Key]
        public string Name { get; set; }
    }
}