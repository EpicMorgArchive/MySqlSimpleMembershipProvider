namespace MySql.Web.Security {
    internal static class StringExtensions {
        public static bool IsEmpty( this string value ) {
            return string.IsNullOrEmpty( value );
        }
    }
}