using System.ComponentModel.DataAnnotations;
using MySql.Web.Security;

namespace SimpleMembershipTest.Dac {
    /// <summary>
    ///     UserProperty
    /// </summary>
    public class UserProperty : UserProfile {
        //[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        //public int UserId
        //{
        //    get;
        //    set;
        //}

        //public string UserName
        //{
        //    get;
        //    set;
        //}

        [DataType( DataType.EmailAddress )]
        public string Email { get; set; }

        public string Facebook { get; set; }

        public int? Age { get; set; }

        public double? Rate { get; set; }

        public string LastName { get; set; }

        public string FirstName { get; set; }
    }
}