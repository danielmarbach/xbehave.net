﻿// <copyright file="ScenarioOutlineRunner.cs" company="xBehave.net contributors">
//  Copyright (c) xBehave.net contributors. All rights reserved.
// </copyright>

namespace Xbehave.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class ScenarioOutlineRunner : XunitTestCaseRunner
    {
        private static readonly object[] noArguments = new object[0];

        private readonly IMessageSink diagnosticMessageSink;
        private readonly ExceptionAggregator cleanupAggregator = new ExceptionAggregator();
        private readonly List<Scenario> scenarios = new List<Scenario>();
        private Exception dataDiscoveryException;

        public ScenarioOutlineRunner(
            IMessageSink diagnosticMessageSink,
            IXunitTestCase scenarioOutline,
            string displayName,
            string skipReason,
            object[] constructorArguments,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            : base(
                scenarioOutline,
                displayName,
                skipReason,
                constructorArguments,
                noArguments,
                messageBus,
                aggregator,
                cancellationTokenSource)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        protected IMessageSink DiagnosticMessageSink
        {
            get { return this.diagnosticMessageSink; }
        }

        protected override async Task AfterTestCaseStartingAsync()
        {
            await base.AfterTestCaseStartingAsync();

            try
            {
                var dataAttributes = TestCase.TestMethod.Method.GetCustomAttributes(typeof(DataAttribute)).ToList();
                foreach (var dataAttribute in dataAttributes)
                {
                    var discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
                    var discoverer =
                        ExtensibilityPointFactory.GetDataDiscoverer(this.diagnosticMessageSink, discovererAttribute);

                    foreach (var dataRow in discoverer.GetData(dataAttribute, TestCase.TestMethod.Method))
                    {
                        var scenario = new Scenario(
                            this.TestCase,
                            this.DisplayName,
                            this.TestClass,
                            this.TestMethod,
                            dataRow,
                            this.SkipReason,
                            this.BeforeAfterAttributes);

                        this.scenarios.Add(scenario);
                    }
                }

                if (!this.scenarios.Any())
                {
                    var scenario = new Scenario(
                        this.TestCase,
                        this.DisplayName,
                        this.TestClass,
                        this.TestMethod,
                        noArguments,
                        this.SkipReason,
                        this.BeforeAfterAttributes);

                    this.scenarios.Add(scenario);
                }
            }
            catch (Exception ex)
            {
                this.dataDiscoveryException = ex;
            }
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            if (this.dataDiscoveryException != null)
            {
                this.MessageBus.Queue(
                    new XunitTest(TestCase, DisplayName),
                    test => new TestFailed(test, 0, null, this.dataDiscoveryException.Unwrap()),
                    this.CancellationTokenSource);

                return new RunSummary { Total = 1, Failed = 1 };
            }

            var summary = new RunSummary();
            foreach (var scenario in this.scenarios)
            {
                summary.Aggregate(await scenario.RunAsync(
                        this.diagnosticMessageSink,
                        this.MessageBus,
                        this.ConstructorArguments,
                        new ExceptionAggregator(this.Aggregator),
                        this.CancellationTokenSource));
            }

            // Run the cleanup here so we can include cleanup time in the run summary,
            // but save any exceptions so we can surface them during the cleanup phase,
            // so they get properly reported as test case cleanup failures.
            var timer = new ExecutionTimer();
            foreach (var scenario in this.scenarios)
            {
                timer.Aggregate(() => this.cleanupAggregator.Run(() => scenario.Dispose()));
            }

            summary.Time += timer.Total;
            return summary;
        }

        protected override Task BeforeTestCaseFinishedAsync()
        {
            Aggregator.Aggregate(this.cleanupAggregator);

            return base.BeforeTestCaseFinishedAsync();
        }
    }
}