using System.Web.Http;

namespace SimpleMembershipTest {
    public static class WebApiConfig {
        public static void Register( HttpConfiguration config ) {
            config.Routes.MapHttpRoute(
                "DefaultApi",
                "api/{controller}/{id}",
                new {
                    id = RouteParameter.Optional
                } );

            config.EnableQuerySupport();
        }
    }
}