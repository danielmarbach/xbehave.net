﻿// <copyright file="Scenario.cs" company="Adam Ralph">
//  Copyright (c) Adam Ralph. All rights reserved.
// </copyright>

namespace Xbehave.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using Xbehave.Fluent;
    using Xunit.Sdk;

    internal static class Scenario
    {
        [ThreadStatic]
        private static bool initialized;

        [ThreadStatic]
        private static DisposableStep given;

        [ThreadStatic]
        private static Step when;

        [ThreadStatic]
        private static List<Step> thensInIsolation;

        [ThreadStatic]
        private static List<Step> thens;

        [ThreadStatic]
        private static List<Step> thenSkips;

        [ThreadStatic]
        private static List<Action> throwActions;

        public static IStep Given(string message, Func<IDisposable> arrange)
        {
            EnsureInitialized();

            if (given == null)
            {
                given = new DisposableStep(message, arrange);
            }
            else
            {
                throwActions.Add(() => { throw new InvalidOperationException("The scenario has more than one Given step."); });
            }

            return given;
        }

        public static IStep When(string message, Action action)
        {
            EnsureInitialized();

            if (when == null)
            {
                when = new Step(message, action);
            }
            else
            {
                throwActions.Add(() => { throw new InvalidOperationException("The scenario has more than one When step."); });
            }

            return when;
        }

        public static IStep ThenInIsolation(string message, Action assert)
        {
            EnsureInitialized();

            var step = new Step(message, assert);
            thensInIsolation.Add(step);
            return step;
        }

        public static IStep Then(string message, Action assert)
        {
            EnsureInitialized();

            var step = new Step(message, assert);
            thens.Add(step);
            return step;
        }

        public static IStep ThenSkip(string message, Action assert)
        {
            EnsureInitialized();

            var step = new Step(message, assert);
            thenSkips.Add(step);
            return step;
        }

        // TODO: address DoNotCatchGeneralExceptionTypes
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Part of the original SubSpec code - will be addressed.")]
        public static IEnumerable<ITestCommand> GetTestCommands(IMethodInfo method, Action registerSteps)
        {
            try
            {
                registerSteps();
                EnsureInitialized();

                try
                {
                    return CommandFactory.Create(throwActions, given, when, thens, thensInIsolation, thenSkips, method);
                }
                finally
                {
                    Reset();
                }
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    CultureInfo.InvariantCulture,
                    "An exception was thrown while building tests from scenario {0}.{1}:\r\n{2}",
                    method.TypeName,
                    method.Name,
                    ex.Message);

                return new[] { new ExceptionTestCommand(method, () => { throw new InvalidOperationException(message, ex); }) };
            }
        }

        private static void Reset()
        {
            given = null;
            when = null;
            thensInIsolation = new List<Step>();
            thens = new List<Step>();
            thenSkips = new List<Step>();
            throwActions = new List<Action>();
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                Reset();
                initialized = true;
            }
        }
    }
}