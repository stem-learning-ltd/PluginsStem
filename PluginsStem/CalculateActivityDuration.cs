using System;
using Microsoft.Xrm.Sdk;

namespace PluginsStem
{
    public class CalculateActivityDuration : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                // Only on Update
                if (context.MessageName != "Update")
                    return;

                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;

                if (target == null || target.LogicalName != "sam_ambassadoractivity")
                    return;

                // PRE image
                Entity preImage = null;

                if (context.PreEntityImages != null &&
                    context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                // =========================
                // Actual dates
                // =========================

                DateTime? actualStart =
                    target.GetAttributeValue<DateTime?>("actualstart") ??
                    preImage?.GetAttributeValue<DateTime?>("actualstart");

                DateTime? actualEnd =
                    target.GetAttributeValue<DateTime?>("actualend") ??
                    preImage?.GetAttributeValue<DateTime?>("actualend");

                // =========================
                // Sync scheduled fields
                // always keep aligned with actual
                // =========================

                if (actualStart != null)
                {
                    target["scheduledstart"] = actualStart;
                }

                if (actualEnd != null)
                {
                    target["scheduledend"] = actualEnd;
                }

                // =========================
                // Duration calculation
                // =========================

                if (actualStart == null || actualEnd == null)
                {
                    trace?.Trace("CalculateDuration: Start or End is null. Skipping.");
                    return;
                }

                var duration =
                    (actualEnd.Value - actualStart.Value).TotalMinutes;

                if (duration < 0)
                {
                    trace?.Trace("CalculateDuration: End < Start → setting duration to 0.");
                    duration = 0;
                }

                int minutes = (int)Math.Round(
                    duration,
                    MidpointRounding.AwayFromZero);

                trace?.Trace($"CalculateDuration: Calculated {minutes} minutes.");

                // PRE OP update
                target["actualdurationminutes"] = minutes;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "CalculateActivityDuration failed: " + ex.Message,
                    ex);
            }
        }
    }
}