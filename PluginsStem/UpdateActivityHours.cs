using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class UpdateActivityHoursFromAmbassadorStatus : IPlugin
    {
        private const int STATUS_POSITION_AWAITING_FEEDBACK = 999990003;
        private const int STATUS_POSITION_FINISHED = 999990007;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            var factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var service =
                factory.CreateOrganizationService(context.UserId);

            var trace =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.Depth > 1)
                    return;

                Guid ambassadorId;

                // ==========================================
                // UPDATE
                // ==========================================

                if (context.MessageName == "Update")
                {
                    if (!context.InputParameters.Contains("Target"))
                        return;

                    var target =
                        context.InputParameters["Target"] as Entity;

                    if (target == null ||
                        target.LogicalName != "sam_activityambassador")
                    {
                        return;
                    }

                    ambassadorId = target.Id;
                }

                // ==========================================
                // SETSTATE
                // ==========================================

                else if (context.MessageName == "SetState")
                {
                    if (!context.InputParameters.Contains("EntityMoniker"))
                        return;

                    var moniker =
                        context.InputParameters["EntityMoniker"] as EntityReference;

                    if (moniker == null ||
                        moniker.LogicalName != "sam_activityambassador")
                    {
                        return;
                    }

                    ambassadorId = moniker.Id;
                }
                else
                {
                    return;
                }

                // ==========================================
                // ALWAYS GET LATEST RECORD
                // ==========================================

                var ambassador = service.Retrieve(
                    "sam_activityambassador",
                    ambassadorId,
                    new ColumnSet(
                        "statuscode",
                        "sam_ambassador",
                        "sam_ambassadoractivity",
                        "sam_activityhours_decimal"));

                int currentStatus =
                    ambassador.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1;

                bool shouldHaveHours =
                    currentStatus == STATUS_POSITION_AWAITING_FEEDBACK ||
                    currentStatus == STATUS_POSITION_FINISHED;

                trace?.Trace(
                    "Current status={0}, shouldHaveHours={1}",
                    currentStatus,
                    shouldHaveHours);

                UpdateAmbassadorHours(
                    service,
                    trace,
                    ambassador,
                    shouldHaveHours);

                UpdateContactTotalHours(
                    service,
                    trace,
                    ambassador);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in UpdateActivityHoursFromAmbassadorStatus plugin: " + ex.Message,
                    ex);
            }
        }

        private void UpdateAmbassadorHours(
            IOrganizationService service,
            ITracingService trace,
            Entity ambassador,
            bool shouldHaveHours)
        {
            var update = new Entity("sam_activityambassador")
            {
                Id = ambassador.Id
            };

            // ==========================================
            // NEGATIVE STATUS → CLEAR HOURS
            // ==========================================

            if (!shouldHaveHours)
            {
                update["sam_activityhours_decimal"] = null;

                service.Update(update);

                trace?.Trace(
                    "Activity hours CLEARED for ambassador {0}",
                    ambassador.Id);

                return;
            }

            // ==========================================
            // VALID STATUS → POPULATE HOURS
            // ==========================================

            var activityRef =
                ambassador.GetAttributeValue<EntityReference>(
                    "sam_ambassadoractivity");

            if (activityRef == null)
                return;

            var activity = service.Retrieve(
                "sam_ambassadoractivity",
                activityRef.Id,
                new ColumnSet("actualdurationminutes"));

            int minutes =
                activity.GetAttributeValue<int?>(
                    "actualdurationminutes") ?? 0;

            decimal hours =
                ConvertMinutesToQuarterHours(minutes);

            update["sam_activityhours_decimal"] = hours;

            service.Update(update);

            trace?.Trace(
                "Activity hours UPDATED for ambassador {0}: {1}",
                ambassador.Id,
                hours);
        }

        private void UpdateContactTotalHours(
            IOrganizationService service,
            ITracingService trace,
            Entity ambassador)
        {
            var contactRef =
                ambassador.GetAttributeValue<EntityReference>(
                    "sam_ambassador");

            if (contactRef == null)
                return;

            Guid contactId = contactRef.Id;

            List<Entity> ambassadors =
                GetValidAmbassadorsForContact(
                    service,
                    contactId);

            decimal totalHours = ambassadors.Sum(a =>
                a.GetAttributeValue<decimal?>(
                    "sam_activityhours_decimal") ?? 0m);

            var updateContact = new Entity("contact")
            {
                Id = contactId
            };

            updateContact["sam_ambassadoractivityhours_decimal"] =
                totalHours;

            service.Update(updateContact);

            trace?.Trace(
                "Contact total activity hours updated: {0}",
                totalHours);
        }

        private List<Entity> GetValidAmbassadorsForContact(
            IOrganizationService service,
            Guid contactId)
        {
            var query = new QueryExpression("sam_activityambassador")
            {
                ColumnSet = new ColumnSet(
                    "sam_activityhours_decimal",
                    "statuscode")
            };

            query.Criteria.AddCondition(
                "sam_ambassador",
                ConditionOperator.Equal,
                contactId);

            var filter =
                new FilterExpression(LogicalOperator.Or);

            filter.AddCondition(
                "statuscode",
                ConditionOperator.Equal,
                STATUS_POSITION_AWAITING_FEEDBACK);

            filter.AddCondition(
                "statuscode",
                ConditionOperator.Equal,
                STATUS_POSITION_FINISHED);

            query.Criteria.AddFilter(filter);

            return service
                .RetrieveMultiple(query)
                .Entities
                .ToList();
        }

        private decimal ConvertMinutesToQuarterHours(int minutes)
        {
            if (minutes <= 0)
                return 0m;

            decimal hours = minutes / 60m;

            return Math.Round(hours * 4, 0) / 4m;
        }
    }
}