using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

[DbContext(typeof(ReimbursementsDbContext))]
internal sealed class ReimbursementsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}
