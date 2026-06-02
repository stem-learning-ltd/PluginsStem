using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class PreActivityProcess : IPlugin
    {
        private const string ENTITY_ACTIVITY = "sam_ambassadoractivity";

        private const string FIELD_START_DATE = "actualstart";
        private const string FIELD_STATECODE = "statecode";
        private const string FIELD_STATUSCODE = "statuscode";

        private const int STATE_OPEN = 0;
        private const int STATUS_PUBLISHED = 1;

        private const int STATUS_UNPUBLISHED = 999990002;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(
                    typeof(IPluginExecutionContext));

            var factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(
                    typeof(IOrganizationServiceFactory));

            var service =
                factory.CreateOrganizationService(context.UserId);

            var trace =
                (ITracingService)serviceProvider.GetService(
                    typeof(ITracingService));

            try
            {
                trace.Trace("=== PreActivityProcess START ===");
                trace.Trace("Depth={0}", context.Depth);
                trace.Trace("MessageName={0}", context.MessageName);
                trace.Trace("UserId={0}", context.UserId);

                if (context.Depth > 1)
                {
                    trace.Trace("Exiting because Depth > 1");
                    return;
                }

                var activities = GetActivitiesToUnpublish(service, trace);

                trace.Trace(
                    "Activities returned for unpublish: {0}",
                    activities.Count);

                foreach (var activity in activities)
                {
                    var actualStart =
                        activity.GetAttributeValue<DateTime?>(
                            FIELD_START_DATE);

                    var state =
                        activity.GetAttributeValue<OptionSetValue>(
                            FIELD_STATECODE)?.Value;

                    var status =
                        activity.GetAttributeValue<OptionSetValue>(
                            FIELD_STATUSCODE)?.Value;

                    trace.Trace(
                        "Processing ActivityId={0}",
                        activity.Id);

                    trace.Trace(
                        "actualstart={0}",
                        actualStart.HasValue
                            ? actualStart.Value.ToString("O")
                            : "NULL");

                    trace.Trace(
                        "statecode={0}, statuscode={1}",
                        state,
                        status);

                    var update = new Entity(ENTITY_ACTIVITY)
                    {
                        Id = activity.Id
                    };

                    update[FIELD_STATUSCODE] =
                        new OptionSetValue(
                            STATUS_UNPUBLISHED);

                    service.Update(update);

                    trace.Trace(
                        "Activity {0} updated to Unpublished ({1})",
                        activity.Id,
                        STATUS_UNPUBLISHED);
                }

                trace.Trace("=== PreActivityProcess END ===");
            }
            catch (Exception ex)
            {
                trace.Trace("ERROR:");
                trace.Trace(ex.ToString());
                throw;
            }
        }

        private static List<Entity> GetActivitiesToUnpublish(
    IOrganizationService service,
    ITracingService trace)
        {
            var now = DateTime.UtcNow;
            var next24Hours = now.AddHours(24);

            trace.Trace("=== GetActivitiesToUnpublish ===");
            trace.Trace("UtcNow={0}", now.ToString("O"));
            trace.Trace("Next24Hours={0}", next24Hours.ToString("O"));

            var query = new QueryExpression(ENTITY_ACTIVITY)
            {
                ColumnSet = new ColumnSet(
                    FIELD_START_DATE,
                    FIELD_STATECODE,
                    FIELD_STATUSCODE)
            };

            query.Criteria.AddCondition(
                FIELD_STATECODE,
                ConditionOperator.Equal,
                STATE_OPEN);

            query.Criteria.AddCondition(
                FIELD_STATUSCODE,
                ConditionOperator.Equal,
                STATUS_PUBLISHED);

            // NO DATE FILTERS HERE
            trace.Trace("Query Filters:");
            trace.Trace("statecode = {0}", STATE_OPEN);
            trace.Trace("statuscode = {0}", STATUS_PUBLISHED);

            var results = service.RetrieveMultiple(query);

            trace.Trace(
                "RetrieveMultiple returned {0} published/open activities",
                results.Entities.Count);

            var activitiesToUnpublish = new List<Entity>();

            foreach (var activity in results.Entities)
            {
                var start =
                    activity.GetAttributeValue<DateTime?>(
                        FIELD_START_DATE);

                var state =
                    activity.GetAttributeValue<OptionSetValue>(
                        FIELD_STATECODE)?.Value;

                var status =
                    activity.GetAttributeValue<OptionSetValue>(
                        FIELD_STATUSCODE)?.Value;

                trace.Trace(
                    "Evaluating ActivityId={0}",
                    activity.Id);

                trace.Trace(
                    "actualstart={0}",
                    start.HasValue
                        ? start.Value.ToString("O")
                        : "NULL");

                trace.Trace(
                    "statecode={0}, statuscode={1}",
                    state,
                    status);

                if (!start.HasValue)
                {
                    trace.Trace(
                        "Skipping activity {0} because actualstart is NULL",
                        activity.Id);

                    continue;
                }

                var hoursFromNow =
                    (start.Value - now).TotalHours;

                trace.Trace(
                    "Hours from now = {0}",
                    hoursFromNow);

                bool insideWindow =
                    start.Value >= now &&
                    start.Value <= next24Hours;

                trace.Trace(
                    "Inside 24h window = {0}",
                    insideWindow);

                if (!insideWindow)
                {
                    trace.Trace(
                        "Skipping activity {0} because it is outside the 24h window",
                        activity.Id);

                    continue;
                }

                trace.Trace(
                    "Activity {0} ADDED for unpublish",
                    activity.Id);

                activitiesToUnpublish.Add(activity);
            }

            trace.Trace(
                "Final activities to unpublish: {0}",
                activitiesToUnpublish.Count);

            return activitiesToUnpublish;
        }
    }
}