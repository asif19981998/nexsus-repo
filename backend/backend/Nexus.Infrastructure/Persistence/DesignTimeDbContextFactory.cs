using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexus.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without starting the API.
/// The connection string only matters for `dotnet ef database update`; the API itself migrates on start-up.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NexusDbContext>
{
    public NexusDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("NEXUS_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=NexusInternship;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer(connection).Options;
        return new NexusDbContext(options);
    }
}
