﻿// <copyright file="StepCommand.cs" company="Adam Ralph">
//  Copyright (c) Adam Ralph. All rights reserved.
// </copyright>

namespace Xbehave.Sdk
{
    using System;
    using System.Globalization;
    using Xunit.Sdk;
    using Guard = Xbehave.Sdk.Infrastructure.Guard;

    [CLSCompliant(false)]
    public class StepCommand : Command
    {
        private readonly Step step;

        public StepCommand(IMethodInfo method, object[] args, int contextOrdinal, int stepOrdinal, Step step)
            : base(method, args, contextOrdinal, stepOrdinal)
        {
            Guard.AgainstNullArgument("step", step);
            Guard.AgainstNullArgumentProperty("step", "Name", step.Name);

            this.step = step;

            var provider = CultureInfo.InvariantCulture;
            string stepName;
            try
            {
                stepName = string.Format(provider, step.Name, args);
            }
            catch (FormatException)
            {
                stepName = step.Name;
            }

            this.Name = string.Format(provider, "{0} {1}", this.Name, stepName);
            this.DisplayName = string.Format(CultureInfo.InvariantCulture, "{0} {1}", this.DisplayName, stepName);
        }

        public override MethodResult Execute(object testClass)
        {
            if (this.step.SkipReason != null)
            {
                return new SkipResult(this.testMethod, this.DisplayName, this.step.SkipReason);
            }

            if (Context.FailedStepName != null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Failed to execute preceding step \"{0}\".", Context.FailedStepName));
            }

            try
            {
                this.step.Execute();
            }
            catch (Exception)
            {
                Context.FailedStepName = this.Name;
                throw;
            }

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
