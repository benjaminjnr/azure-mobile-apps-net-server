// ---------------------------------------------------------------------------- 
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------------------- 

using System;

namespace Microsoft.Azure.Mobile.Server
{
    /// <summary>
    /// This class provides configuration information for connection strings.
    /// </summary>
    public class ConnectionSettings
    {
        private string name;
        private string connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionSettings"/> with a given <paramref name="name"/>
        /// and <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="name">The name of the connection string setting.</param>
        /// <param name="connectionString">The actual connection string.</param>
        public ConnectionSettings(string name, string connectionString)
        {
			this.name = name ?? throw new ArgumentNullException("name");
            this.connectionString = connectionString ?? throw new ArgumentNullException("connectionString");
        }

        /// <summary>
        /// Gets or sets the name of the connection string.
        /// </summary>
        public string Name
		{
			get => this.name;

			set => this.name = value ?? throw new ArgumentNullException("value");
		}

		/// <summary>
		/// Gets or sets the actual connection string.
		/// </summary>
		public string ConnectionString
		{
			get => this.connectionString;

			set => this.connectionString = value ?? throw new ArgumentNullException("value");
		}

		/// <summary>
		/// Gets or sets the provider to be used by this connection string, e.g. <c>System.Data.SqlClient</c>.
		/// </summary>
		public string Provider { get; set; }
    }
}
