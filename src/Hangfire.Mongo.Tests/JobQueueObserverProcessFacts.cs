using System;
using System.Threading;
using Hangfire.Mongo.Tests.Utils;
using Moq;
using Xunit;

namespace Hangfire.Mongo.Tests
{
    public class JobQueueObserverProcessFacts
    {
        private readonly MongoStorage _storage;

        private readonly CancellationToken _token;
        private readonly Mock<JobQueueSemaphore> _jobQueueSemaphore;
        public JobQueueObserverProcessFacts()
        {
            _storage = ConnectionUtils.CreateStorage();

            _token = new CancellationToken(true);
            _jobQueueSemaphore = new Mock<JobQueueSemaphore>(MockBehavior.Strict);
        }
        
        [Fact]
        public void Ctor_ThrowsAnException_WhenStorageIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new JobQueueObserverProcess(null, _jobQueueSemaphore.Object));
        }

        [Fact, CleanDatabase]
        public void Execute_NoJobQueueSignals_Nothing()
        {
            using (var connection = ConnectionUtils.CreateConnection())
            {     
                var manager = new JobQueueObserverProcess(_storage.Connection, _jobQueueSemaphore.Object);

                manager.Execute(_token);

                
            }
        }
    }
}