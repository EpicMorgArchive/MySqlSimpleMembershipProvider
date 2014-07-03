// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace MySql.Web.Security {
    internal class DatabaseConnectionInfo {
        private string _connectionString;
        private string _connectionStringName;

        public string ConnectionString {
            get {
                return this._connectionString;
            }
            set {
                this._connectionString = value;
                this.Type = ConnectionType.ConnectionString;
            }
        }

        public string ConnectionStringName {
            get {
                return this._connectionStringName;
            }
            set {
                this._connectionStringName = value;
                this.Type = ConnectionType.ConnectionStringName;
            }
        }

        public string ProviderName { get; set; }

        private ConnectionType Type { get; set; }

        private enum ConnectionType {
            ConnectionStringName = 0,
            ConnectionString = 1
        }
    }
}