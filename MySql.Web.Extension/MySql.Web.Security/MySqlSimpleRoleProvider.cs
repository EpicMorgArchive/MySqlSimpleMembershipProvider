﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

/**********************************************************************************************************************/
/*	Domain		:	MySql.Web.Security.MySqlSimpleRoleProvider
/*	Creator		:	KIM-KIWON\xyz37(Kim Ki Won)
/*	Create		:	Friday, April 12, 2013 10:56 AM
/*	Purpose		:	Defines the contract that ASP.NET implements to provide role-management services using custom role providers for MySql database.
/*--------------------------------------------------------------------------------------------------------------------*/
/*	Modifier	:	
/*	Update		:	
/*	Changes		:	
/*--------------------------------------------------------------------------------------------------------------------*/
/*	Comment		:	
/*--------------------------------------------------------------------------------------------------------------------*/
/*	Reviewer	:	Kim Ki Won
/*	Rev. Date	:	
/**********************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Configuration.Provider;
using System.Globalization;
using System.Linq;
using System.Web.Security;
using MySql.Web.Extension.Resources;

namespace MySql.Web.Security {
    /// <summary>
    ///     Defines the contract that ASP.NET implements to provide role-management services using custom role providers for
    ///     MySql database.
    /// </summary>
    public class MySqlSimpleRoleProvider : RoleProvider {
        private readonly RoleProvider _previousProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlSimpleRoleProvider" /> class.
        /// </summary>
        public MySqlSimpleRoleProvider() : this( null ) {}

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlSimpleRoleProvider" /> class.
        /// </summary>
        /// <param name="previousProvider">The previous provider.</param>
        public MySqlSimpleRoleProvider( RoleProvider previousProvider ) {
            this._previousProvider = previousProvider;
        }

        private RoleProvider PreviousProvider {
            get {
                if ( this._previousProvider == null ) throw new InvalidOperationException( Resources.Security_InitializeMustBeCalledFirst );
                return this._previousProvider;
            }
        }

        private MySqlSecurityDbContext NewMySqlSecurityDbContext {
            get {
                //if (string.IsNullOrEmpty(ConfigUtil.MySqlSecurityInheritedContextType) == true)
                //{
                //	string nameOrConnectionString = ConnectionInfo.ConnectionString;

                //	if (nameOrConnectionString.IsEmpty() == true)
                //		nameOrConnectionString = ConnectionInfo.ConnectionStringName;

                //	return new MySqlSecurityDbContext(nameOrConnectionString);
                //}
                //else
                {
                    // NOTICE: In the above manner, the DatabaseInitializer interface method call had occurred.
                    // by X10-MOBILE\xyz37(Kim Ki Won) in Friday, April 19, 2013 12:31 AM
                    var contextInstance =
                        Activator.CreateInstance( Type.GetType( ConfigUtil.MySqlSecurityInheritedContextType, false, true ) );

                    return contextInstance as MySqlSecurityDbContext;
                }
            }
        }

        internal DatabaseConnectionInfo ConnectionInfo { get; set; }

        internal bool InitializeCalled { get; set; }

        /// <summary>
        ///     Gets or sets the name of the application to store and retrieve role information for.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <value>The name of the application.</value>
        /// <exception cref="System.NotSupportedException">
        /// </exception>
        /// <returns>The name of the application to store and retrieve role information for.</returns>
        public override string ApplicationName {
            get {
                if ( this.InitializeCalled ) throw new NotSupportedException();
                return this.PreviousProvider.ApplicationName;
            }
            set {
                if ( this.InitializeCalled ) throw new NotSupportedException();
                this.PreviousProvider.ApplicationName = value;
            }
        }

        /// <summary>
        ///     Adds the specified user names to the specified roles for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="usernames">A string array of user names to be added to the specified roles.</param>
        /// <param name="roleNames">A string array of the role names to add the specified user names to.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        /// <exception cref="System.Configuration.Provider.ProviderException"></exception>
        public override void AddUsersToRoles( string[] usernames, string[] roleNames ) {
            if ( !this.InitializeCalled ) this.PreviousProvider.AddUsersToRoles( usernames, roleNames );
            else {
                using ( var db = this.NewMySqlSecurityDbContext ) {
                    var userCount = usernames.Length;
                    var roleCount = roleNames.Length;
                    var userIds = this.GetUserIdsFromNames( db, usernames );
                    var roleIds = GetRoleIdsFromNames( db, roleNames );
                    var affectedRow = 0;

                    // Generate a INSERT INTO for each userid/rowid combination, where userIds are the first params, and roleIds follow
                    for ( var uId = 0; uId < userCount; uId++ ) {
                        for ( var rId = 0; rId < roleCount; rId++ ) {
                            if ( this.IsUserInRole( usernames[ uId ], roleNames[ rId ] ) ) {
                                throw new InvalidOperationException(
                                    String.Format(
                                        CultureInfo.CurrentCulture,
                                        Resources.SimpleRoleProvder_UserAlreadyInRole,
                                        usernames[ uId ],
                                        roleNames[ rId ] ) );
                            }

                            // REVIEW: is there a way to batch up these inserts?
                            db.UsersInRoles.Add(
                                new UsersInRoles {
                                    UserId = userIds[ uId ],
                                    RoleId = roleIds[ rId ],
                                } );
                            affectedRow++;
                        }
                    }

                    if ( db.SaveChanges() != affectedRow ) throw new ProviderException( Resources.Security_DbFailure );
                }
            }
        }

        /// <summary>
        ///     Adds a new role to the data source for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="roleName">The name of the role to create.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        /// <exception cref="System.Configuration.Provider.ProviderException"></exception>
        public override void CreateRole( string roleName ) {
            if ( !this.InitializeCalled ) this.PreviousProvider.CreateRole( roleName );
            else {
                using ( var db = this.NewMySqlSecurityDbContext ) {
                    var roleId = FindRoleId( db, roleName );
                    if ( roleId != -1 ) {
                        throw new InvalidOperationException(
                            String.Format( CultureInfo.InvariantCulture, Resources.SimpleRoleProvider_RoleExists, roleName ) );
                    }

                    db.Roles.Add(
                        new Role {
                            RoleName = roleName
                        } );

                    var rows = db.SaveChanges();
                    if ( rows != 1 ) throw new ProviderException( Resources.Security_DbFailure );
                }
            }
        }

        /// <summary>
        ///     Removes a role from the data source for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="roleName">The name of the role to delete.</param>
        /// <param name="throwOnPopulatedRole">
        ///     If true, throw an exception if <paramref name="roleName" /> has one or more members
        ///     and do not delete <paramref name="roleName" />.
        /// </param>
        /// <returns>true if the role was successfully deleted; otherwise, false.</returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public override bool DeleteRole( string roleName, bool throwOnPopulatedRole ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.DeleteRole( roleName, throwOnPopulatedRole );

            using ( var db = this.NewMySqlSecurityDbContext ) {
                var roleId = FindRoleId( db, roleName );
                if ( roleId == -1 ) return false;

                var usersInRoles = db.UsersInRoles.Where( x => x.RoleId == roleId );

                if ( throwOnPopulatedRole ) {
                    if ( usersInRoles.Any() ) {
                        throw new InvalidOperationException(
                            String.Format( CultureInfo.InvariantCulture, Resources.SimpleRoleProvder_RolePopulated, roleName ) );
                    }
                }
                else {
                    // Delete any users in this role first
                    foreach ( var usersInRole in usersInRoles )
                        db.UsersInRoles.Remove( usersInRole );
                }

                var role = db.Roles.SingleOrDefault( x => x.RoleId == roleId );

                db.Roles.Remove( role );

                return ( db.SaveChanges() > 0 );
            }
        }

        /// <summary>
        ///     Gets an array of user names in a role where the user name contains the specified user name to match.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="roleName">The role to search in.</param>
        /// <param name="usernameToMatch">The user name to search for.</param>
        /// <returns>
        ///     A string array containing the names of all the users where the user name matches
        ///     <paramref name="usernameToMatch" /> and the user is a member of the specified role.
        /// </returns>
        public override string[] FindUsersInRole( string roleName, string usernameToMatch ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.FindUsersInRole( roleName, usernameToMatch );
            using ( var db = this.NewMySqlSecurityDbContext ) {
                // REVIEW: Is there any way to directly get out a string[]?
                var users =
                    db.UsersInRoles.Where( x => x.Role.RoleName == roleName && x.UserProfile.UserName.Contains( usernameToMatch ) )
                      .Select( x => x.UserProfile.UserName )
                      .ToArray();

                return users;
            }
        }

        /// <summary>
        ///     Gets a list of all the roles for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <returns>
        ///     A string array containing the names of all the roles stored in the data source for the configured
        ///     applicationName.
        /// </returns>
        public override string[] GetAllRoles() {
            if ( !this.InitializeCalled ) return this.PreviousProvider.GetAllRoles();
            using ( var db = this.NewMySqlSecurityDbContext ) {
                var roles = db.Roles.Select( x => x.RoleName ).ToArray();

                return roles;
            }
        }

        /// <summary>
        ///     Gets a list of the roles that a specified user is in for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="username">The user to return a list of roles for.</param>
        /// <returns>
        ///     A string array containing the names of all the roles that the specified user is in for the configured
        ///     applicationName.
        /// </returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public override string[] GetRolesForUser( string username ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.GetRolesForUser( username );
            using ( var db = this.NewMySqlSecurityDbContext ) {
                var userId = MySqlSimpleMembershipProvider.GetUserId( db, username );
                if ( userId == -1 ) {
                    throw new InvalidOperationException(
                        String.Format( CultureInfo.CurrentCulture, Resources.Security_NoUserFound, username ) );
                }

                var roles = db.UsersInRoles.Where( x => x.UserId == userId ).Select( x => x.Role.RoleName ).ToArray();

                return roles;
            }
        }

        /// <summary>
        ///     Gets a list of users in the specified role for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="roleName">The name of the role to get the list of users for.</param>
        /// <returns>
        ///     A string array containing the names of all the users who are members of the specified role for the configured
        ///     applicationName.
        /// </returns>
        public override string[] GetUsersInRole( string roleName ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.GetUsersInRole( roleName );
            using ( var db = this.NewMySqlSecurityDbContext ) {
                var users = db.UsersInRoles.Where( x => x.Role.RoleName == roleName ).Select( x => x.UserProfile.UserName ).ToArray();

                return users;
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the specified user is in the specified role for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="username">The user name to search for.</param>
        /// <param name="roleName">The role to search in.</param>
        /// <returns>true if the specified user is in the specified role for the configured applicationName; otherwise, false.</returns>
        public override bool IsUserInRole( string username, string roleName ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.IsUserInRole( username, roleName );
            using ( var db = this.NewMySqlSecurityDbContext ) {
                var count = db.UsersInRoles.Count( x => x.UserProfile.UserName == username && x.Role.RoleName == roleName );

                return ( count == 1 );
            }
        }

        /// <summary>
        ///     Removes the specified user names from the specified roles for the configured applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="usernames">A string array of user names to be removed from the specified roles.</param>
        /// <param name="roleNames">A string array of role names to remove the specified user names from.</param>
        /// <exception cref="System.InvalidOperationException">
        /// </exception>
        /// <exception cref="System.Configuration.Provider.ProviderException"></exception>
        public override void RemoveUsersFromRoles( string[] usernames, string[] roleNames ) {
            if ( !this.InitializeCalled ) this.PreviousProvider.RemoveUsersFromRoles( usernames, roleNames );
            else {
                foreach ( var rolename in roleNames.Where( rolename => !this.RoleExists( rolename ) ) ) {
                    throw new InvalidOperationException(
                        String.Format( CultureInfo.CurrentCulture, Resources.SimpleRoleProvider_NoRoleFound, rolename ) );
                }

                foreach ( var username in usernames ) {
                    foreach ( var rolename in roleNames.Where( rolename => !this.IsUserInRole( username, rolename ) ) ) {
                        throw new InvalidOperationException(
                            String.Format( CultureInfo.CurrentCulture, Resources.SimpleRoleProvder_UserNotInRole, username, rolename ) );
                    }
                }

                using ( var db = this.NewMySqlSecurityDbContext ) {
                    var userIds = this.GetUserIdsFromNames( db, usernames );
                    var roleIds = GetRoleIdsFromNames( db, roleNames );
                    var affectedRows = 0;

                    foreach ( var usersInRole in userIds.SelectMany(
                        userId => roleIds,
                        ( userId, roleId ) => db.UsersInRoles
                            .SingleOrDefault( x => x.UserId == userId && x.RoleId == roleId ) )
                            .Where( usersInRole => usersInRole != null ) ) {
                        db.UsersInRoles.Remove( usersInRole );
                        affectedRows++;
                    }

                    if ( db.SaveChanges() != affectedRows ) throw new ProviderException( Resources.Security_DbFailure );
                }
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the specified role name already exists in the role data source for the configured
        ///     applicationName.
        /// </summary>
        /// <remarks>Inherited from RoleProvider ==> Forwarded to previous provider if this provider hasn't been initialized</remarks>
        /// <param name="roleName">The name of the role to search for in the data source.</param>
        /// <returns>true if the role name already exists in the data source for the configured applicationName; otherwise, false.</returns>
        public override bool RoleExists( string roleName ) {
            if ( !this.InitializeCalled ) return this.PreviousProvider.RoleExists( roleName );
            using ( var db = this.NewMySqlSecurityDbContext ) return ( FindRoleId( db, roleName ) != -1 );
        }

        private static int FindRoleId( MySqlSecurityDbContext db, string roleName ) {
            var role = db.Roles.SingleOrDefault( x => x.RoleName == roleName );

            if ( role != null )
                return role.RoleId;
            return -1;
        }

        private static List<int> GetRoleIdsFromNames( MySqlSecurityDbContext db, string[] roleNames ) {
            var roleIds = new List<int>( roleNames.Length );
            foreach ( var role in roleNames ) {
                var id = FindRoleId( db, role );
                if ( id == -1 ) {
                    throw new InvalidOperationException(
                        String.Format( CultureInfo.CurrentCulture, Resources.SimpleRoleProvider_NoRoleFound, role ) );
                }
                roleIds.Add( id );
            }
            return roleIds;
        }

        private List<int> GetUserIdsFromNames( MySqlSecurityDbContext db, string[] usernames ) {
            var userIds = new List<int>( usernames.Length );
            foreach ( var username in usernames ) {
                var id = MySqlSimpleMembershipProvider.GetUserId( db, username );
                if ( id == -1 ) {
                    throw new InvalidOperationException(
                        String.Format( CultureInfo.CurrentCulture, Resources.Security_NoUserFound, username ) );
                }
                userIds.Add( id );
            }
            return userIds;
        }

        private void VerifyInitialized() {
            if ( !this.InitializeCalled ) throw new InvalidOperationException( Resources.Security_InitializeMustBeCalledFirst );
        }
    }
}