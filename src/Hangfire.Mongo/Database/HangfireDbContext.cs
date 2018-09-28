using System;
using Hangfire.Mongo.DistributedLock;
using Hangfire.Mongo.Dto;
using Hangfire.Mongo.Migration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Hangfire.Mongo.Database
{
    /// <summary>
    /// Represents Mongo database context for Hangfire
    /// </summary>
    public sealed class HangfireDbContext : IDisposable
    {
        private readonly string _prefix;

        internal MongoClient Client { get; }
        
        internal IMongoDatabase Database { get; }

        /// <summary>
        /// Constructs context with connection string and database name
        /// </summary>
        /// <param name="connectionString">Connection string for Mongo database</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="prefix">Collections prefix</param>
        public HangfireDbContext(string connectionString, string databaseName, string prefix = "hangfire")
            :this(MongoClientSettings.FromUrl(MongoUrl.Create(connectionString)), databaseName, prefix)
        {
        }

        /// <summary>
        /// Constructs context with Mongo client settings and database name
        /// </summary>
        /// <param name="mongoClientSettings">Client settings for MongoDB</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="prefix">Collections prefix</param>
        public HangfireDbContext(MongoClientSettings mongoClientSettings, string databaseName, string prefix = "hangfire")
        {
            _prefix = prefix;

            Client = new MongoClient(mongoClientSettings);

            Database = Client.GetDatabase(databaseName);

            ConnectionId = Guid.NewGuid().ToString();
        }


        /// <summary>
        /// Mongo database connection identifier
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Reference to job graph collection
        /// </summary>
        public IMongoCollection<BaseJobDto> JobGraph => Database.GetCollection<BaseJobDto>(_prefix + ".jobGraph");

        /// <summary>
        /// Reference to collection which contains distributed locks
        /// </summary>
        public IMongoCollection<DistributedLockDto> DistributedLock => Database
            .GetCollection<DistributedLockDto>(_prefix + ".locks");

        /// <summary>
        /// Reference to collection which contains schemas
        /// </summary>
        public IMongoCollection<SchemaDto> Schema => Database.GetCollection<SchemaDto>(_prefix + ".schema");

        /// <summary>
        /// Reference to collection which contains servers information
        /// </summary>
        public IMongoCollection<ServerDto> Server => Database.GetCollection<ServerDto>(_prefix + ".server");

        
        
        private string JobEnqueueSignalsCollectionName => _prefix + ".jobQueueSignals";
        /// <summary>
        /// Reference to tailable collection which contains signal dtos for enqueued job items
        /// </summary>
        public IMongoCollection<JobEnqueuedDto> EnqueuedJobs =>
            Database.GetCollection<JobEnqueuedDto>(JobEnqueueSignalsCollectionName);
        
        /// <summary>
        /// Initializes intial collections schema for Hangfire
        /// </summary>
        public void Init(MongoStorageOptions storageOptions)
        {
            using (new MongoDistributedLock(nameof(Init), TimeSpan.FromSeconds(30), this, storageOptions))
            {
                var migrationManager = new MongoMigrationManager(storageOptions);
                migrationManager.Migrate(this);

                if (!CollectionExists(JobEnqueueSignalsCollectionName))
                {
                    Database.CreateCollection(JobEnqueueSignalsCollectionName, new CreateCollectionOptions
                    {
                        Capped = true,
                        MaxSize = 4096
                    });
                }
            }
        }

        private bool CollectionExists(string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            return Database.ListCollectionNames(options).Any();
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public void Dispose()
        {
        }
    }
}