using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class UpdateActivityAmbassadorCounts : IPlugin
    {
        // =========================
        // Activity states
        // =========================

        private const int ACTIVITY_STATE_OPEN = 0;
        private const int ACTIVITY_STATE_COMPLETED = 1;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;
                if (target == null || target.LogicalName != "sam_activityambassador")
                    return;

                // avoid recursion
                if (context.Depth > 1)
                    return;

                if (context.MessageName != "Create" && context.MessageName != "Update")
                    return;

                Entity preImage = null;
                if (context.MessageName == "Update" &&
                    context.PreEntityImages != null &&
                    context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                var activityRef =
                    target.GetAttributeValue<EntityReference>("sam_ambassadoractivity") ??
                    preImage?.GetAttributeValue<EntityReference>("sam_ambassadoractivity");

                if (activityRef == null)
                {
                    trace?.Trace("UpdateActivityAmbassadorCounts: no sam_ambassadoractivity on record, skipping.");
                    return;
                }

                Guid activityId = activityRef.Id;

                RecalculateCountsForActivity(service, activityId, trace);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in UpdateActivityAmbassadorCounts plugin: " + ex.Message, ex);
            }
        }

        private void RecalculateCountsForActivity(
            IOrganizationService service,
            Guid activityId,
            ITracingService trace)
        {
            var ambassadors = GetActivityAmbassadorsForActivity(service, activityId);

            int interested = 0;
            int confirmed = 0;

            foreach (var amb in ambassadors)
            {
                var status = amb.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1;

                // Interest Registered
                if (status == 999990001)
                {
                    interested++;
                }

                // Position Accepted / Awaiting Feedback / Position Finished
                if (status == 999990002 || status == 999990003 || status == 999990007)
                {
                    confirmed++;
                }
            }

            trace?.Trace(
                "UpdateActivityAmbassadorCounts: activity {0} -> interested={1}, confirmed={2}",
                activityId,
                interested,
                confirmed);

            // =========================
            // Get current activity state
            // =========================

            var activity = service.Retrieve(
                "sam_ambassadoractivity",
                activityId,
                new ColumnSet("statecode", "statuscode"));

            var currentState =
                activity.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? ACTIVITY_STATE_OPEN;

            var currentStatus =
                activity.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? 1;

            bool wasCompleted = currentState == ACTIVITY_STATE_COMPLETED;

            // =========================
            // Workaround:
            // Re-open completed activity
            // =========================

            if (wasCompleted)
            {
                trace?.Trace(
                    "UpdateActivityAmbassadorCounts: reopening completed activity {0}",
                    activityId);

                var reopenRequest = new SetStateRequest
                {
                    EntityMoniker = new EntityReference("sam_ambassadoractivity", activityId),
                    State = new OptionSetValue(ACTIVITY_STATE_OPEN),
                    Status = new OptionSetValue(1) // default Open status
                };

                service.Execute(reopenRequest);
            }

            // =========================
            // Update counts
            // =========================

            var activityToUpdate = new Entity("sam_ambassadoractivity");
            activityToUpdate.Id = activityId;

            activityToUpdate["sam_numberofinterestedambassadors"] = interested;
            activityToUpdate["sam_numberofambassadorsconfirmed"] = confirmed;

            service.Update(activityToUpdate);

            // =========================
            // Restore original state
            // =========================

            if (wasCompleted)
            {
                trace?.Trace(
                    "UpdateActivityAmbassadorCounts: restoring completed state for activity {0}",
                    activityId);

                var closeRequest = new SetStateRequest
                {
                    EntityMoniker = new EntityReference("sam_ambassadoractivity", activityId),
                    State = new OptionSetValue(currentState),
                    Status = new OptionSetValue(currentStatus)
                };

                service.Execute(closeRequest);
            }
        }

        public List<Entity> GetActivityAmbassadorsForActivity(
            IOrganizationService orgService,
            Guid activityId)
        {
            EntityCollection results = new EntityCollection();
            EntityCollection resultsTemp = new EntityCollection();

            try
            {
                var query = new QueryExpression("sam_activityambassador");

                query.ColumnSet.AddColumns("statuscode");

                query.Criteria.AddCondition(
                    "sam_ambassadoractivity",
                    ConditionOperator.Equal,
                    activityId);

                query.PageInfo = new PagingInfo()
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                do
                {
                    resultsTemp = orgService.RetrieveMultiple(query);

                    results.Entities.AddRange(resultsTemp.Entities);

                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = resultsTemp.PagingCookie;
                }
                while (resultsTemp.MoreRecords);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in GetActivityAmbassadorsForActivity, Plugin: UpdateActivityAmbassadorCounts " + ex.Message);
            }

            return results.Entities.ToList();
        }
    }
}