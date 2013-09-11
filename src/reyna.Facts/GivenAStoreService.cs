﻿namespace Reyna.Facts
{
    using System;
    using System.Threading;
    using Moq;
    using Reyna.Interfaces;
    using Xunit;

    public class GivenAStoreService
    {
        public GivenAStoreService()
        {
            this.VolatileStore = new InMemoryQueue();
            this.PersistentStore = new Mock<IRepository>();

            this.PersistentStore.Setup(r => r.Add(It.IsAny<IMessage>()));

            this.StoreService = new StoreService(this.VolatileStore, this.PersistentStore.Object);
        }

        private IRepository VolatileStore { get; set; }

        private Mock<IRepository> PersistentStore { get; set; }

        private IService StoreService { get; set; }

        [Fact]
        public void WhenCallingStartAndMessageAddedShouldCallPutOnRepository()
        {
            this.StoreService.Start();

            this.VolatileStore.Add(new Message(new Uri("http://www.google.com"), string.Empty));
            Thread.Sleep(200);

            Assert.Null(this.VolatileStore.Get());
            this.PersistentStore.Verify(r => r.Add(It.IsAny<IMessage>()), Times.Once());
        }

        [Fact]
        public void WhenCallingStartAndMessageAddedThenImmediatelyStopShouldNotCallPutOnRepository()
        {
            this.StoreService.Start();
            Thread.Sleep(50);

            this.VolatileStore.Add(new Message(new Uri("http://www.google.com"), string.Empty));
            this.StoreService.Stop();
            Thread.Sleep(200);

            this.VolatileStore.Add(new Message(new Uri("http://www.google.com"), string.Empty));
            Thread.Sleep(200);

            Assert.NotNull(this.VolatileStore.Get());
            this.PersistentStore.Verify(r => r.Add(It.IsAny<IMessage>()), Times.Once());
        }

        [Fact]
        public void WhenCallingStartAndStopRapidlyWhilstAddingMessagesShouldNotCallPutOnRepository()
        {
            var messageAddingThread = new Thread(new ThreadStart(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        this.VolatileStore.Add(new Message(new Uri("http://www.google.com"), string.Empty));
                        Thread.Sleep(100);
                    }
                }));

            messageAddingThread.Start();
            Thread.Sleep(50);

            for (int k = 0; k < 10; k++)
            {
                this.StoreService.Start();
                Thread.Sleep(50);

                this.StoreService.Stop();
                Thread.Sleep(200);
            }

            Thread.Sleep(1000);

            Assert.Null(this.VolatileStore.Get());
            this.PersistentStore.Verify(r => r.Add(It.IsAny<IMessage>()), Times.Exactly(10));
        }

        [Fact]
        public void WhenCallingStopOnStoreThatHasntStartedShouldNotThrow()
        {
            this.StoreService.Stop();
        }

        [Fact]
        public void WhenCallingDisposeShouldNotThrow()
        {
            this.StoreService.Dispose();
        }

        [Fact]
        public void WhenCallingStartStopDisposeShouldNotThrow()
        {
            this.StoreService.Start();
            Thread.Sleep(50);

            this.StoreService.Stop();
            Thread.Sleep(50);
            
            this.StoreService.Dispose();
        }

        [Fact]
        public void WhenConstructingWithBothNullParametersShouldThrow()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StoreService(null, null));
            Assert.Equal("sourceStore", exception.ParamName);
        }

        [Fact]
        public void WhenConstructingWithNullMessageStoreParameterShouldThrow()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StoreService(null, new Mock<IRepository>().Object));
            Assert.Equal("sourceStore", exception.ParamName);
        }

        [Fact]
        public void WhenConstructingWithNullRepositoryParameterShouldThrow()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StoreService(new InMemoryQueue(), null));
            Assert.Equal("targetStore", exception.ParamName);
        }
    }
}