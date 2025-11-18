using System;

namespace MetaverseCloudEngine.Infrastructure.Models
{
    /// <summary>
    /// Join entity connecting projects and organizational units.
    /// </summary>
    public class ProjectOrganizationalUnit : Auditable
    {
        public Guid ProjectId { get; set; }
        public virtual Project Project { get; set; } = default!;

        public Guid OrganizationalUnitId { get; set; }
        public virtual OrganizationalUnit OrganizationalUnit { get; set; } = default!;

        public Guid AssignedByUserId { get; set; }
        public virtual SystemUser AssignedByUser { get; set; } = default!;
    }
}
