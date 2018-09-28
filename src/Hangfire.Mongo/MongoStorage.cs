using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hangfire.Logging;
using Hangfire.Mongo.Database;
using Hangfire.Server;
using Hangfire.Storage;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Hangfire.Mongo
{
    /// <summary>
    /// Hangfire Job Storage implementation for Mongo database
    /// </summary>
    public class MongoStorage : JobStorage
    {
        private readonly string _databaseName;

        private readonly MongoClientSettings _mongoClientSettings;

        private readonly MongoStorageOptions _storageOptions;

        private readonly HangfireDbContext _connection;

        private readonly JobQueueSemaphore _jobQueueSemaphore;

        static MongoStorage()
        {
            // We will register all our Dto classes with the default conventions.
            // By doing this, we can safely use strings for referencing class
            // property names with risking to have a mismatch with any convention
            // used by bson serializer.
            var conventionPack = new ConventionPack();
            conventionPack.Append(DefaultConventionPack.Instance);
            conventionPack.Append(AttributeConventionPack.Instance);
            var conventionRunner = new ConventionRunner(conventionPack);

            var assembly = typeof(MongoStorage).GetTypeInfo().Assembly;
            var classMaps = assembly.DefinedTypes
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType && t.Namespace == "Hangfire.Mongo.Dto")
                .Select(t => new BsonClassMap(t.AsType()));

            foreach (var classMap in classMaps)
            {
                conventionRunner.Apply(classMap);
                BsonClassMap.RegisterClassMap(classMap);
            }
        }


        /// <summary>
        /// Constructs Job Storage by database connection string and name
        /// </summary>
        /// <param name="connectionString">MongoDB connection string</param>
        /// <param name="databaseName">Database name</param>
        public MongoStorage(string connectionString, string databaseName)
            : this(connectionString, databaseName, new MongoStorageOptions())
        {
        }

        /// <summary>
        /// Constructs Job Storage by database connection string, name and options
        /// </summary>
        /// <param name="connectionString">MongoDB connection string</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="storageOptions">Storage options</param>
        public MongoStorage(string connectionString, string databaseName, MongoStorageOptions storageOptions)
            : this(MongoClientSettings.FromConnectionString(connectionString), databaseName, storageOptions)
        {
        }

        /// <summary>
        /// Constructs Job Storage by Mongo client settings and name
        /// </summary>
        /// <param name="mongoClientSettings">Client settings for MongoDB</param>
        /// <param name="databaseName">Database name</param>
        public MongoStorage(MongoClientSettings mongoClientSettings, string databaseName)
            : this(mongoClientSettings, databaseName, new MongoStorageOptions())
        {
        }

        /// <summary>
        /// Constructs Job Storage by Mongo client settings, name and options
        /// </summary>
        /// <param name="mongoClientSettings">Client settings for MongoDB</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="storageOptions">Storage options</param>
        public MongoStorage(MongoClientSettings mongoClientSettings, string databaseName, MongoStorageOptions storageOptions)
        {
            if (mongoClientSettings == null)
            {
                throw new ArgumentNullException(nameof(mongoClientSettings));
            }
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentNullException(nameof(databaseName));
            }

            _mongoClientSettings = mongoClientSettings;
            _databaseName = databaseName;
            _storageOptions = storageOptions ?? throw new ArgumentNullException(nameof(storageOptions));

            _connection = new HangfireDbContext(mongoClientSettings, databaseName, _storageOptions.Prefix);
            _connection.Init(storageOptions);
            _jobQueueSemaphore = new JobQueueSemaphore();
        }

        /// <summary>
        /// Database context
        /// </summary>
        [Obsolete(("This will be removed from the public API. Please open an issue in our Github page if you would " +
                   "like this to be exposed still"))]
        public HangfireDbContext Connection => _connection;

        /// <summary>
        /// Returns Monitoring API object
        /// </summary>
        /// <returns>Monitoring API object</returns>
        public override IMonitoringApi GetMonitoringApi()
        {
            return new MongoMonitoringApi(_connection);
        }

        /// <summary>
        /// Returns storage connection
        /// </summary>
        /// <returns>Storage connection</returns>
        public override IStorageConnection GetConnection()
        {
            return new MongoConnection(_connection, _storageOptions, _jobQueueSemaphore);
        }

        /// <summary>
        /// Returns collection of server components
        /// </summary>
        /// <returns>Collection of server components</returns>
        public override IEnumerable<IServerComponent> GetComponents()
        {
            yield return new ExpirationManager(_connection, _storageOptions.JobExpirationCheckInterval);
            yield return new JobQueueObserverProcess(_connection, _jobQueueSemaphore);
        }

        /// <summary>
        /// Writes storage options to log
        /// </summary>
        /// <param name="logger">Logger</param>
        public override void WriteOptionsToLog(ILog logger)
        {
            logger.Info("Using the following options for Mongo DB job storage:");
            logger.InfoFormat("    Prefix: {0}.", _storageOptions.Prefix);
        }

        /// <summary>
        /// Opens connection to database
        /// </summary>
        /// <returns>Database context</returns>
        [Obsolete("This will be removed from the public API. Please open an issue in our Github page if you would " +
                  "like this to be exposed still")]
        public HangfireDbContext CreateAndOpenConnection()
        {
            return _connection;
        }

        /// <summary>
        /// Returns text representation of the object
        /// </summary>
        public override string ToString()
        {
            // Obscure the username and password for display purposes
            var obscuredConnectionString = "mongodb://";

            if (_mongoClientSettings == null || _mongoClientSettings.Servers == null)
            {
                return $"Connection string: {obscuredConnectionString}, " +
                       $"database name: {_databaseName}, " +
                       $"prefix: {_storageOptions.Prefix}";
            }

            var servers = string.Join(",", _mongoClientSettings.Servers.Select(s => $"{s.Host}:{s.Port}"));
            obscuredConnectionString = $"mongodb://<username>:<password>@{servers}";
            return $"Connection string: {obscuredConnectionString}, database name: {_databaseName}, prefix: {_storageOptions.Prefix}";
        }
    }
}