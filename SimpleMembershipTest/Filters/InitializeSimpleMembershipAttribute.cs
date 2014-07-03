using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using System.Web.Security;
using MySql.Web.Security;
using SimpleMembershipTest.Dac;
using WebMatrix.WebData;

namespace SimpleMembershipTest.Filters {
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true )]
    public sealed class InitializeSimpleMembershipAttribute : ActionFilterAttribute {
        private static SimpleMembershipInitializer _initializer;
        private static object _initializerLock = new object();
        private static bool _isInitialized;

        public override void OnActionExecuting( ActionExecutingContext filterContext ) {
            // Ensure ASP.NET Simple Membership is initialized only once per app start
            LazyInitializer.EnsureInitialized( ref _initializer, ref _isInitialized, ref _initializerLock );
        }

        private class SimpleMembershipInitializer {
            public SimpleMembershipInitializer() {
                Database.SetInitializer<SimpleMembershipTestDbContext>( null );

                try {
                    using ( var context = SimpleMembershipTestDbContext.CreateContext() ) {
                        if ( !context.Database.Exists() ) {
                            // Create the SimpleMembership database without Entity Framework migration schema
                            ( (IObjectContextAdapter) context ).ObjectContext.CreateDatabase();
                        }
                    }

                    MySqlWebSecurity.InitializeDatabaseConnection( "SimpleMembershipTestDbContext" );

                    const string adminRoles = "Administrators";
                    const string adminUser = "admin";

                    if ( Roles.RoleExists( adminRoles ) ) return;
                    Roles.CreateRole( adminRoles );

                    if ( WebSecurity.UserExists( adminUser ) == false )
                        WebSecurity.CreateUserAndAccount( adminUser, "password" );

                    if ( Roles.GetRolesForUser( adminUser ).Contains( adminRoles ) == false )
                        Roles.AddUserToRole( adminUser, adminRoles );
                }
                catch ( Exception ex ) {
                    throw new InvalidOperationException(
                        "The ASP.NET Simple Membership database could not be initialized. For more information, please see http://go.microsoft.com/fwlink/?LinkId=256588",
                        ex );
                }
            }
        }
    }
}