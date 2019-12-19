using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.HttpMetrics;
using System;
using System.Threading.Tasks;

namespace Tests.HttpExporter
{
    [TestClass]
    public class HttpInProgressMiddlewareTests
    {
        private FakeGauge _gauge;
        private RequestDelegate _requestDelegate;

        private HttpInProgressMiddleware _sut;

        [TestMethod]
        public void Given_no_requests_then_InProgressGauge_returns_zero()
        {
            Assert.AreEqual(0, _gauge.IncrementCount);
            Assert.AreEqual(0, _gauge.DecrementCount);
            Assert.AreEqual(0, _gauge.Value);
        }

        [TestMethod]
        public async Task
            Given_multiple_completed_parallel_requests_gauge_is_incremented_and_decremented_correct_number_of_times()
        {
            await Task.WhenAll(_sut.Invoke(new DefaultHttpContext()), _sut.Invoke(new DefaultHttpContext()),
                _sut.Invoke(new DefaultHttpContext()));

            Assert.AreEqual(3, _gauge.IncrementCount);
            Assert.AreEqual(3, _gauge.DecrementCount);
            Assert.AreEqual(0, _gauge.Value);
        }


        [TestMethod]
        public async Task Given_request_throws_then_InProgressGauge_is_decreased()
        {
            _requestDelegate = context => throw new InvalidOperationException();
            _sut = new HttpInProgressMiddleware(_requestDelegate, _gauge);


            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _sut.Invoke(new DefaultHttpContext()));

            Assert.AreEqual(1, _gauge.IncrementCount);
            Assert.AreEqual(1, _gauge.DecrementCount);
            Assert.AreEqual(0, _gauge.Value);
        }

        [TestInitialize]
        public void Init()
        {
            _gauge = new FakeGauge();
            _requestDelegate = context => Task.CompletedTask;

            _sut = new HttpInProgressMiddleware(_requestDelegate, _gauge);
        }
    }

    internal class FakeGauge : ICollector<IGauge>, IGauge
    {
        public int IncrementCount { get; private set; }
        public int DecrementCount { get; private set; }


        public void Inc(double increment = 1)
        {
            IncrementCount++;
            Value += increment;
        }

        public void Set(double val)
        {
            Value = val;
        }

        public void Dec(double decrement = 1)
        {
            DecrementCount++;
            Value -= decrement;
        }

        public void IncTo(double targetValue)
        {
            throw new NotImplementedException();
        }

        public void DecTo(double targetValue)
        {
            throw new NotImplementedException();
        }

        public IGauge WithLabels(params string[] labelValues)
        {
            return this;
        }

        public double Value { get; private set; }

        public IGauge Unlabelled => this;

        public string[] LabelNames => new string[0];

        public string Name => "name";
        public string Help => "help";
    }
}