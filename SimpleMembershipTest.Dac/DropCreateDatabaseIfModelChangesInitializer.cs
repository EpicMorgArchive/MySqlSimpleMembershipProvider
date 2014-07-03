using MySql.Data.MySqlClient;

namespace SimpleMembershipTest.Dac {
    public class DropCreateDatabaseIfModelChangesInitializer : DropCreateMySqlDatabaseIfModelChanges<SimpleMembershipTestDbContext> {
        protected override void Seed( SimpleMembershipTestDbContext db ) {
            db.UserProperties.Add(
                new UserProperty {
                    UserId = 1,
                    UserName = "admin",
                    Age = 40,
                    Email = "xyz3710@gmail.com",
                    Facebook = "http://facebook.com/xyz37",
                    Rate = 100,
                    LastName = "Kim",
                    FirstName = "Ki Won",
                } );
        }
    }
}