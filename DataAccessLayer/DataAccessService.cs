namespace AeroSyncAPI.DataAccessLayer
{
    public class DataAccessService
    {
        private readonly ApplicationDbContext _context;
        public DataAccessService(ApplicationDbContext context)
        {
            _context = context;
        }
    }
}
