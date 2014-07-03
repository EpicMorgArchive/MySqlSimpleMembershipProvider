using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.Web.WebPages.OAuth;
using MySql.Web.Security;
using SimpleMembershipTest.Dac;
using SimpleMembershipTest.Filters;
using SimpleMembershipTest.Models;

namespace SimpleMembershipTest.Controllers {
    [Authorize]
    [InitializeSimpleMembership]
    public class AccountController : Controller {
        //
        // GET: /Account/Login

        //
        // POST: /Account/Disassociate

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Disassociate( string provider, string providerUserId ) {
            var ownerAccount = OAuthWebSecurity.GetUserName( provider, providerUserId );
            ManageMessageId? message = null;

            // Only disassociate the account if the currently logged in user is the owner
            if ( ownerAccount != this.User.Identity.Name ) {
                return this.RedirectToAction(
                    "Manage",
                    new {
                        Message = (ManageMessageId?) null
                    } );
            }
            // Use a transaction to prevent the user from deleting their last login credential
            using ( var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions {
                    IsolationLevel = IsolationLevel.Serializable
                } ) ) {
                var hasLocalAccount = OAuthWebSecurity.HasLocalAccount( MySqlWebSecurity.GetUserId( this.User.Identity.Name ) );
                var externalLoginCount = OAuthWebSecurity.GetAccountsFromUserName( this.User.Identity.Name ).Count;

                if ( hasLocalAccount || externalLoginCount > 1 ) {
                    OAuthWebSecurity.DeleteAccount( provider, providerUserId );
                    scope.Complete();
                    message = ManageMessageId.RemoveLoginSuccess;
                }
                else if ( externalLoginCount == 1 )
                    message = ManageMessageId.RequestOneExternalLogin;
            }

            return this.RedirectToAction(
                "Manage",
                new {
                    Message = message
                } );
        }

        //
        // GET: /Account/Manage

        //
        // POST: /Account/ExternalLogin

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin( string provider, string returnUrl ) {
            return new ExternalLoginResult(
                provider,
                this.Url.Action(
                    "ExternalLoginCallback",
                    new {
                        ReturnUrl = returnUrl
                    } ) );
        }

        //
        // GET: /Account/ExternalLoginCallback

        [AllowAnonymous]
        public ActionResult ExternalLoginCallback( string returnUrl ) {
            var result = OAuthWebSecurity.VerifyAuthentication(
                this.Url.Action(
                    "ExternalLoginCallback",
                    new {
                        ReturnUrl = returnUrl
                    } ) );
            if ( !result.IsSuccessful ) return this.RedirectToAction( "ExternalLoginFailure" );

            if ( OAuthWebSecurity.Login( result.Provider, result.ProviderUserId, false ) ) return this.RedirectToLocal( returnUrl );

            if ( this.User.Identity.IsAuthenticated ) {
                // If the current user is logged in add the new account
                OAuthWebSecurity.CreateOrUpdateAccount( result.Provider, result.ProviderUserId, this.User.Identity.Name );
                return this.RedirectToLocal( returnUrl );
            }
            // User is new, ask for their desired membership name
            var loginData = OAuthWebSecurity.SerializeProviderUserId( result.Provider, result.ProviderUserId );
            this.ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData( result.Provider ).DisplayName;
            this.ViewBag.ReturnUrl = returnUrl;
            return this.View(
                "ExternalLoginConfirmation",
                new RegisterExternalLoginModel {
                    UserName = result.UserName,
                    ExternalLoginData = loginData
                } );
        }

        //
        // POST: /Account/ExternalLoginConfirmation

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLoginConfirmation( RegisterExternalLoginModel model, string returnUrl ) {
            string provider;
            string providerUserId;

            if ( this.User.Identity.IsAuthenticated
                 || !OAuthWebSecurity.TryDeserializeProviderUserId( model.ExternalLoginData, out provider, out providerUserId ) )
                return this.RedirectToAction( "Manage" );

            if ( this.ModelState.IsValid ) {
                // Insert a new user into the database
                using ( var db = SimpleMembershipTestDbContext.CreateContext() ) {
                    var user = db.UserProfiles.FirstOrDefault( u => u.UserName.ToLower() == model.UserName.ToLower() );
                    // Check if user already exists
                    if ( user == null ) {
                        // Insert name into the profile table
                        db.UserProfiles.Add(
                            new UserProfile {
                                UserName = model.UserName
                            } );
                        db.SaveChanges();

                        OAuthWebSecurity.CreateOrUpdateAccount( provider, providerUserId, model.UserName );
                        OAuthWebSecurity.Login( provider, providerUserId, false );

                        return this.RedirectToLocal( returnUrl );
                    }
                    this.ModelState.AddModelError( "UserName", "User name already exists. Please enter a different user name." );
                }
            }

            this.ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData( provider ).DisplayName;
            this.ViewBag.ReturnUrl = returnUrl;
            return View( model );
        }

        //
        // GET: /Account/ExternalLoginFailure

        [AllowAnonymous]
        public ActionResult ExternalLoginFailure() {
            return this.View();
        }

        [AllowAnonymous]
        [ChildActionOnly]
        public ActionResult ExternalLoginsList( string returnUrl ) {
            this.ViewBag.ReturnUrl = returnUrl;

            // Return OAuth Providers does not used.
            ICollection<AuthenticationClientData> model;

            if ( this.User.Identity.Name == string.Empty )
                model = OAuthWebSecurity.RegisteredClientData;
            else {
                var userOAuthProviders = OAuthWebSecurity.GetAccountsFromUserName( this.User.Identity.Name ).Select( x => x.Provider );
                model =
                    OAuthWebSecurity.RegisteredClientData.Where(
                        x => userOAuthProviders.Contains( x.AuthenticationClient.ProviderName ) == false ).ToList();
            }

            return this.PartialView( "_ExternalLoginsListPartial", model );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff() {
            MySqlWebSecurity.Logout();

            return this.RedirectToAction( "Index", "Home" );
        }

        [AllowAnonymous]
        public ActionResult Login( string returnUrl ) {
            this.ViewBag.ReturnUrl = returnUrl;
            return this.View();
        }

        //
        // POST: /Account/Login

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login( LoginModel model, string returnUrl ) {
            if ( this.ModelState.IsValid && MySqlWebSecurity.Login( model.UserName, model.Password, model.RememberMe ) )
                return this.RedirectToLocal( returnUrl );

            // If we got this far, something failed, redisplay form
            this.ModelState.AddModelError( "", "The user name or password provided is incorrect." );
            return View( model );
        }

        public ActionResult Manage( ManageMessageId? message ) {
            this.ViewBag.StatusMessage = message == ManageMessageId.ChangePasswordSuccess
                                             ? "Your password has been changed."
                                             : message == ManageMessageId.SetPasswordSuccess
                                                   ? "Your password has been set."
                                                   : message == ManageMessageId.RemoveLoginSuccess
                                                         ? "The external login was removed."
                                                         : message == ManageMessageId.RequestOneExternalLogin
                                                               ? "You must one external login or local account."
                                                               : "";
            this.ViewBag.HasLocalPassword = OAuthWebSecurity.HasLocalAccount( MySqlWebSecurity.GetUserId( this.User.Identity.Name ) );
            this.ViewBag.ReturnUrl = this.Url.Action( "Manage" );
            var model = new ChangePropertyModel {
                LocalPasswordModel = new LocalPasswordModel(),
                PropertyModel = new PropertyModel(),
            };

            using ( var db = SimpleMembershipTestDbContext.CreateContext() ) {
                var userProperties = db.UserProperties.SingleOrDefault( x => x.UserName == this.User.Identity.Name );

                if ( userProperties != null ) {
                    model.PropertyModel = new PropertyModel {
                        Age = userProperties.Age,
                        Email = userProperties.Email,
                        Facebook = userProperties.Facebook,
                        FirstName = userProperties.FirstName,
                        LastName = userProperties.LastName,
                        Rate = userProperties.Rate,
                    };
                }
            }

            return View( model );
        }

        //
        // POST: /Account/Manage

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public ActionResult Manage( ChangePropertyModel model ) {
            var hasLocalAccount = OAuthWebSecurity.HasLocalAccount( MySqlWebSecurity.GetUserId( this.User.Identity.Name ) );
            this.ViewBag.HasLocalPassword = hasLocalAccount;
            this.ViewBag.ReturnUrl = this.Url.Action( "Manage" );
            if ( hasLocalAccount ) {
                if ( !this.ModelState.IsValid ) return this.View( model );
                // ChangePassword will throw an exception rather than return false in certain failure scenarios.
                bool changePasswordSucceeded;

                if ( string.IsNullOrEmpty( model.LocalPasswordModel.ConfirmPassword ) ) {
                    using ( var db = SimpleMembershipTestDbContext.CreateContext() ) {
                        var userProperty = db.UserProperties.SingleOrDefault( x => x.UserName == this.User.Identity.Name );

                        if ( userProperty == null ) {
                            var userId = MySqlWebSecurity.GetUserId( this.User.Identity.Name );

                            userProperty = new UserProperty {
                                UserId = userId,
                                UserName = this.User.Identity.Name,
                            };
                            db.UserProperties.Add( userProperty );
                        }

                        userProperty.Age = model.PropertyModel.Age;
                        userProperty.Email = model.PropertyModel.Email;
                        userProperty.Facebook = model.PropertyModel.Facebook;
                        userProperty.FirstName = model.PropertyModel.FirstName;
                        userProperty.LastName = model.PropertyModel.LastName;
                        userProperty.Rate = model.PropertyModel.Rate;

                        changePasswordSucceeded = db.SaveChanges() > 0;
                    }
                }
                else {
                    try {
                        changePasswordSucceeded = MySqlWebSecurity.ChangePassword(
                            this.User.Identity.Name,
                            model.LocalPasswordModel.OldPassword,
                            model.LocalPasswordModel.NewPassword );
                    }
                    catch ( Exception ) {
                        changePasswordSucceeded = false;
                    }
                }

                if ( changePasswordSucceeded ) {
                    return this.RedirectToAction(
                        "Manage",
                        new {
                            Message = ManageMessageId.ChangePasswordSuccess
                        } );
                }
                this.ModelState.AddModelError( "", "The current password is incorrect or the new password is invalid." );
            }
            else {
                // User does not have a local password so remove any validation errors caused by a missing
                // OldPassword field
                var state = this.ModelState[ "OldPassword" ];
                if ( state != null ) state.Errors.Clear();

                if ( !this.ModelState.IsValid ) return this.View( model );
                try {
                    MySqlWebSecurity.CreateAccount( this.User.Identity.Name, model.LocalPasswordModel.NewPassword );
                    return this.RedirectToAction(
                        "Manage",
                        new {
                            Message = ManageMessageId.SetPasswordSuccess
                        } );
                }
                catch ( Exception e ) {
                    this.ModelState.AddModelError( "", e );
                }
            }

            // If we got this far, something failed, redisplay form
            return View( model );
        }

        [AllowAnonymous]
        public ActionResult Register() {
            return this.View();
        }

        //
        // POST: /Account/Register

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register( RegisterModel model ) {
            if ( !this.ModelState.IsValid ) return this.View( model );
            // Attempt to register the user
            try {
                IDictionary<string, object> properties = new Dictionary<string, object>();

                // NOTICE: To use this property columns. Add "MySql.Data.Extension" project partial "UserProfile" class and add property columns.
                // by KIM-KIWON\xyz37(Kim Ki Won) in Thursday, April 18, 2013 5:02 PM
                //properties.Add("Email", model.Email);
                //properties.Add("Facebook", model.Facebook);
                //properties.Add("Age", model.Age);
                //properties.Add("Rate", model.Rate);

                using ( var scope = new TransactionScope() ) {
                    MySqlWebSecurity.CreateUserAndAccount( model.UserName, model.Password, properties );
                    MySqlWebSecurity.Login( model.UserName, model.Password );

                    var userId = MySqlWebSecurity.GetUserId( model.UserName );

                    using ( var db = SimpleMembershipTestDbContext.CreateContext() ) {
                        db.UserProperties.Add(
                            new UserProperty {
                                UserId = userId,
                                UserName = model.UserName,
                                Age = model.Age,
                                Email = model.Email,
                                Facebook = model.Facebook,
                                Rate = model.Rate,
                                LastName = model.LastName,
                                FirstName = model.FirstName,
                            } );
                        db.SaveChanges();
                    }

                    scope.Complete();
                }

                return this.RedirectToAction( "Index", "Home" );
            }
            catch ( MembershipCreateUserException e ) {
                this.ModelState.AddModelError( "", ErrorCodeToString( e.StatusCode ) );
            }

            // If we got this far, something failed, redisplay form
            return View( model );
        }

        [ChildActionOnly]
        public ActionResult RemoveExternalLogins() {
            var accounts = OAuthWebSecurity.GetAccountsFromUserName( this.User.Identity.Name );
            var externalLogins = ( from account in accounts
                let clientData = OAuthWebSecurity.GetOAuthClientData( account.Provider )
                select new ExternalLogin {
                    Provider = account.Provider,
                    ProviderDisplayName = clientData.DisplayName,
                    ProviderUserId = account.ProviderUserId,
                } ).ToList();

            this.ViewBag.ShowRemoveButton = externalLogins.Count > 1
                                            || OAuthWebSecurity.HasLocalAccount( MySqlWebSecurity.GetUserId( this.User.Identity.Name ) );
            return this.PartialView( "_RemoveExternalLoginsPartial", externalLogins );
        }

        #region Helpers
        public enum ManageMessageId {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RequestOneExternalLogin,
        }

        private static string ErrorCodeToString( MembershipCreateStatus createStatus ) {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            switch ( createStatus ) {
                case MembershipCreateStatus.DuplicateUserName:
                    return "User name already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.ProviderError:
                    return
                        "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.UserRejected:
                    return
                        "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return
                        "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }

        private ActionResult RedirectToLocal( string returnUrl ) {
            if ( this.Url.IsLocalUrl( returnUrl ) ) return this.Redirect( returnUrl );
            return this.RedirectToAction( "Index", "Home" );
        }

        internal class ExternalLoginResult : ActionResult {
            public ExternalLoginResult( string provider, string returnUrl ) {
                this.Provider = provider;
                this.ReturnUrl = returnUrl;
            }

            public string Provider { get; private set; }
            public string ReturnUrl { get; private set; }

            public override void ExecuteResult( ControllerContext context ) {
                OAuthWebSecurity.RequestAuthentication( this.Provider, this.ReturnUrl );
            }
        }
        #endregion
    }
}