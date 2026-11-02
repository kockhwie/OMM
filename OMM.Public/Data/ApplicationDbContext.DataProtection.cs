using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OMM.Public.Data;

// Partial extension of ApplicationDbContext to support ASP.NET Core Data Protection key storage.
// This allows Data Protection keys to be persisted in PostgreSQL rather than on the container
// filesystem, which is ephemeral on Render Docker (keys would be lost on restart/redeploy).
// Both OMM.Public (writes encrypted inquiry messages) and OMM.Admin (decrypts them) must
// share the same key ring — storing keys in the shared DB ensures both services use the same keys
// even after a container restart on Render's free tier (which restarts after 15 min of idle).
public partial class ApplicationDbContext : IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}

