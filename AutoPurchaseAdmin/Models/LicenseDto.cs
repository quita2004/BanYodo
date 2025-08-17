using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPurchaseAdmin.Models
{
    public class LicenseDto
    {
        public Guid LicenseId { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public bool IsActive { get; set; }
    }
}
