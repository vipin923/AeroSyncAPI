using Microsoft.EntityFrameworkCore;

namespace AeroSyncAPI.DataAccessLayer
{
    public class ApplicationDbContext: DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

    }
}
