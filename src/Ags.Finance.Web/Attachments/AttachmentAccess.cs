using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Web.Backup;

namespace Ags.Finance.Web.Attachments;

internal sealed class AttachmentAccess(MaintenanceMode maintenance) : IAttachmentAccess
{
    public bool IsBlocked => maintenance.IsActive;
}
