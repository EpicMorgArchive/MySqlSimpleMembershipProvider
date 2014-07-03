using System.Web.Mvc;
using SimpleMembershipTest.Filters;

namespace SimpleMembershipTest {
    public static class FilterConfig {
        public static void RegisterGlobalFilters( GlobalFilterCollection filters ) {
            filters.Add( new HandleErrorAttribute() );
            filters.Add( new InitializeSimpleMembershipAttribute() );
        }
    }
}