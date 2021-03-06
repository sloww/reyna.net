﻿namespace Reyna.Facts
{
    using System;
    using System.Collections.Generic;
    using Moq;
    using Reyna.Interfaces;
    using Xunit;

    public class GivenABatchProvider
    {
        private const long IntervalDay = 24 * 60 * 60 * 1000;

        public GivenABatchProvider()
        {
            this.Repository = new Mock<IRepository>();
            this.BatchConfiguration = new Mock<IBatchConfiguration>();
            this.PeriodicBackoutCheck = new Mock<IPeriodicBackoutCheck>();

            this.BatchConfiguration.SetupGet(b => b.BatchMessageCount).Returns(3);
            this.BatchConfiguration.SetupGet(b => b.BatchMessagesSize).Returns(1000);
            this.BatchConfiguration.SetupGet(b => b.BatchUrl).Returns(new Uri("http://post.com/api/batch"));
            this.BatchConfiguration.SetupGet(b => b.SubmitInterval).Returns(24 * 60 * 60 * 1000);

            this.Provider = new BatchProvider(this.Repository.Object, this.PeriodicBackoutCheck.Object);
            this.Provider.BatchConfiguration = this.BatchConfiguration.Object;
        }

        private Mock<IRepository> Repository { get; set; }

        private Mock<IBatchConfiguration> BatchConfiguration { get; set; }

        private Mock<IPeriodicBackoutCheck> PeriodicBackoutCheck { get; set; }

        private BatchProvider Provider { get; set; }

        [Fact]
        public void WhenConstructingShouldNotThrow()
        {
            this.Provider = new BatchProvider(this.Repository.Object, this.PeriodicBackoutCheck.Object);
            Assert.NotNull(this.Provider);
            Assert.NotNull(this.BatchConfiguration);
        }

        [Fact]
        public void WhenCallingCanSendShouldRerurnTrue()
        {
            Assert.False(this.Provider.CanSend);
        }

        [Fact]
        public void WhenCallingDeleteShouldRemoveMessage()
        {
            var messge = new Mock<IMessage>();
            this.Provider.Delete(messge.Object);

            this.Repository.Verify(r => r.DeleteMessagesFrom(messge.Object));
        }

        [Fact]
        public void WhenCallingGetNextAndNoMessageShouldReturnNull()
        {
            this.Repository.Setup(r => r.Get()).Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.Null(actual);
        }

        [Fact]
        public void WhenCallingGetNextShouldReturnCorrectFormat()
        {
            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(messages[1]);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);

            Assert.Equal("http://post.com/api/batch", actual.Url.AbsoluteUri);
            Assert.Equal(
                "{\"events\":[" +
                    "{\"url\":\"http://google.com/\", \"reynaId\":1, \"payload\":{\"key01\":\"value01\",\"key02\":11}}, " +
                    "{\"url\":\"http://google2.com/\", \"reynaId\":2, \"payload\":{\"key11\":\"value11\",\"key12\":12}}, " +
                    "{\"url\":\"http://google3.com/\", \"reynaId\":3, \"payload\":{\"key21\":\"value21\",\"key22\":22}}" +
                    "]}",
                    actual.Body);

            this.AssertHeaders(actual);
            Assert.Equal(3, actual.Id);
        }

        [Fact]
        public void WhenCallingGetNextAndThereIsCorruptedMessageShouldPostIt()
        {
            Message message = new Message(new Uri("http://google2.com"), "{\"body\":\"{\\\"key11\\\":\"}");
            message.Id = 2;
            this.AddHeaders(message);

            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(message);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);

            Assert.Equal("http://post.com/api/batch", actual.Url.AbsoluteUri);
            Assert.Equal(
                "{\"events\":[" +
                    "{\"url\":\"http://google.com/\", \"reynaId\":1, \"payload\":{\"key01\":\"value01\",\"key02\":11}}, " +
                    "{\"url\":\"http://google2.com/\", \"reynaId\":2, \"payload\":{\"body\":\"{\\\"key11\\\":\"}}, " +
                    "{\"url\":\"http://google3.com/\", \"reynaId\":3, \"payload\":{\"key21\":\"value21\",\"key22\":22}}" +
                    "]}",
                    actual.Body);

            this.AssertHeaders(actual);
            Assert.Equal(3, actual.Id);
        }

        [Fact]
        public void WhenCallingGetNextShouldReturnMessagesRelatedToMaximumLimit()
        {
            this.BatchConfiguration.SetupGet(b => b.BatchMessageCount).Returns(2);

            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(messages[1]);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);

            Assert.Equal("http://post.com/api/batch", actual.Url.AbsoluteUri);
            Assert.Equal(
                "{\"events\":[" +
                    "{\"url\":\"http://google.com/\", \"reynaId\":1, \"payload\":{\"key01\":\"value01\",\"key02\":11}}, " +
                    "{\"url\":\"http://google2.com/\", \"reynaId\":2, \"payload\":{\"key11\":\"value11\",\"key12\":12}}" +
                    "]}",
                    actual.Body);

            this.AssertHeaders(actual);
            Assert.Equal(2, actual.Id);
        }

        [Fact]
        public void WhenCallingGetNextShouldReturnMessagesRelatedToMaximumSize()
        {
            this.BatchConfiguration.SetupGet(b => b.BatchMessagesSize).Returns(100);
            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(messages[1]);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);

            Assert.Equal("http://post.com/api/batch", actual.Url.AbsoluteUri);
            Assert.Equal(
                "{\"events\":[" +
                    "{\"url\":\"http://google.com/\", \"reynaId\":1, \"payload\":{\"key01\":\"value01\",\"key02\":11}}" +
                    "]}",
                    actual.Body);

            this.AssertHeaders(actual);
            Assert.Equal(1, actual.Id);
        }

        [Fact]
        public void WhenCallingGetNextAndNoUrlConfiguredShouldReturnUrlWithBatchAppended()
        {
            this.BatchConfiguration.SetupGet(b => b.BatchMessagesSize).Returns(95);
            this.BatchConfiguration.SetupGet(b => b.BatchUrl).Returns((Uri)null);
            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(messages[1]);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);
            Assert.Equal("http://google.com/batch", actual.Url.ToString());
        }

        [Fact]
        public void WhenCallingGetNextAndNoUrlConfiguredAndHTTPSShouldReturnUrlWithBatchAppended()
        {
            this.BatchConfiguration.SetupGet(b => b.BatchMessagesSize).Returns(95);
            this.BatchConfiguration.SetupGet(b => b.BatchUrl).Returns((Uri)null);

            var messages = this.GetTestMessages();
            this.Repository.Setup(r => r.Get()).Returns(messages[0]);
            this.Repository.Setup(r => r.GetNextMessageAfter(1))
                .Returns(messages[1]);
            this.Repository.Setup(r => r.GetNextMessageAfter(2))
                .Returns(messages[2]);
            this.Repository.Setup(r => r.GetNextMessageAfter(3))
                .Returns((IMessage)null);

            ((Message)messages[0]).Url = new Uri("https://www.post.com/1/2/req");
            ((Message)messages[1]).Url = new Uri("https://www.post.com/1/2/req");
            ((Message)messages[2]).Url = new Uri("https://www.post.com/1/2/req");

            var actual = this.Provider.GetNext();

            Assert.NotNull(actual);
            Assert.Equal("https://www.post.com/1/2/batch", actual.Url.ToString());
        }

        [Fact]
        public void WhenCallingCanSendAndTimeNotElapsedShouldReturnFalse()
        {
            long interval = (long)(IntervalDay * 0.9);
            this.PeriodicBackoutCheck.Setup(p => p.IsTimeElapsed("BatchProvider", interval)).Returns(false);

            var actual = this.Provider.CanSend;

            Assert.False(actual);
            this.PeriodicBackoutCheck.Verify(p => p.IsTimeElapsed("BatchProvider", interval), Times.Once());
        }

        [Fact]
        public void WhenCallingCanSendAndTimeElapsedShouldReturnTrue()
        {
            long interval = (long)(IntervalDay * 0.9);
            this.PeriodicBackoutCheck.Setup(p => p.IsTimeElapsed("BatchProvider", interval)).Returns(true);

            var actual = this.Provider.CanSend;

            Assert.True(actual);
            this.PeriodicBackoutCheck.Verify(p => p.IsTimeElapsed("BatchProvider", interval), Times.Once());
        }

        [Fact]
        public void WhenCallingCanSendThereAreMoreMessagesThanMaxMessagesCountShouldSend()
        {
            long interval = (long)(IntervalDay * 0.9);
            this.PeriodicBackoutCheck.Setup(p => p.IsTimeElapsed("BatchProvider", interval)).Returns(false);
            this.BatchConfiguration.SetupGet(b => b.BatchMessageCount).Returns(100);
            this.Repository.SetupGet(r => r.AvailableMessagesCount).Returns(100);

            var actual = this.Provider.CanSend;

            Assert.True(actual);
            this.PeriodicBackoutCheck.Verify(p => p.IsTimeElapsed("BatchProvider", interval), Times.Once());
            this.BatchConfiguration.Verify(p => p.BatchMessageCount, Times.Once());
            this.Repository.Verify(p => p.AvailableMessagesCount, Times.Once());
        }

        [Fact]
        public void WhenCallingCanSendThereAreLessMessagesThanMaxMessagesCountShouldSend()
        {
            long interval = (long)(IntervalDay * 0.9);
            this.PeriodicBackoutCheck.Setup(p => p.IsTimeElapsed("BatchProvider", interval)).Returns(false);
            this.BatchConfiguration.SetupGet(b => b.BatchMessageCount).Returns(100);
            this.Repository.SetupGet(r => r.AvailableMessagesCount).Returns(99);

            var actual = this.Provider.CanSend;

            Assert.False(actual);
            this.PeriodicBackoutCheck.Verify(p => p.IsTimeElapsed("BatchProvider", interval), Times.Once());
            this.BatchConfiguration.Verify(p => p.BatchMessageCount, Times.Once());
            this.Repository.Verify(p => p.AvailableMessagesCount, Times.Once());
        }

        [Fact]
        public void WhenCallingCloseAndNeverSendSuccessfulBatchShouldNotRecord()
        {
            this.Provider.Close();

            this.PeriodicBackoutCheck.Verify(p => p.Record("BatchProvider"), Times.Never());
        }

        [Fact]
        public void WhenCallingCloseAndSuccessfullySentBatchShouldRecord()
        {
            var message = new Message(new Uri("http://google.com"), "{\"key01\":\"value01\",\"key02\":11}");
            message.Id = 1;

            this.Provider.Delete(message);
            this.Provider.Close();

            this.PeriodicBackoutCheck.Verify(p => p.Record("BatchProvider"), Times.Once());
        }

        [Fact]
        public void WhenCallingDeleteShouldDeleteFromRepository()
        {
            var message = new Message(new Uri("http://google.com"), "{\"key01\":\"value01\",\"key02\":11}");
            message.Id = 100;

            this.Provider.Delete(message);

            this.Repository.Verify(p => p.DeleteMessagesFrom(message), Times.Once());
        }

        private List<IMessage> GetTestMessages()
        {
            var message1 = new Message(new Uri("http://google.com"), "{\"key01\":\"value01\",\"key02\":11}");
            var message2 = new Message(new Uri("http://google2.com"), "{\"key11\":\"value11\",\"key12\":12}");
            var message3 = new Message(new Uri("http://google3.com"), "{\"key21\":\"value21\",\"key22\":22}");

            message1.Id = 1;
            message2.Id = 2;
            message3.Id = 3;

            this.AddHeaders(message1);
            this.AddHeaders(message2);
            this.AddHeaders(message3);

            List<IMessage> messages = new List<IMessage>(3);
            messages.Add(message1);
            messages.Add(message2);
            messages.Add(message3);

            return messages;
        }

        private void AddHeaders(IMessage message)
        {
            message.Headers.Add("key1", "value1");
            message.Headers.Add("key2", "value2");
            message.Headers.Add("key4", "value4");
        }

        private void AssertHeaders(IMessage actual)
        {
            Assert.Equal(3, actual.Headers.Count);
            Assert.Equal("key1", actual.Headers.Keys[0]);
            Assert.Equal("value1", actual.Headers.Get(0));
            Assert.Equal("key2", actual.Headers.Keys[1]);
            Assert.Equal("value2", actual.Headers.Get(1));
            Assert.Equal("key4", actual.Headers.Keys[2]);
            Assert.Equal("value4", actual.Headers.Get(2));
        }
    }
}
