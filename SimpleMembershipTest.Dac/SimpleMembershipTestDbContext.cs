using System.Data.Entity;
using MySql.Web.Security;

namespace SimpleMembershipTest.Dac {
    public class SimpleMembershipTestDbContext : MySqlSecurityDbContext {
        // public non argument constructor for MySqlSimpleMembershipProvider
        public SimpleMembershipTestDbContext() : base( "SimpleMembershipTestDbContext" ) {}

        public DbSet<UserProperty> UserProperties { get; set; }

        public static SimpleMembershipTestDbContext CreateContext() {
            return new SimpleMembershipTestDbContext();
        }
    }
}