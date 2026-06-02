using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class UpdateContactLastActivityDate : IPlugin
    {
        // =========================
        // Entities
        // =========================

        private const string ENTITY_ACTIVITYAMBASSADOR = "sam_activityambassador";
        private const string ENTITY_ACTIVITY = "sam_ambassadoractivity";
        private const string ENTITY_CONTACT = "contact";

        // =========================
        // Activity Ambassador fields
        // =========================

        private const string FIELD_ACTIVITYAMB_ACTIVITY = "sam_ambassadoractivity";
        private const string FIELD_ACTIVITYAMB_CONTACT = "sam_ambassador";
        private const string FIELD_ACTIVITYAMB_STATUS = "statuscode";

        // =========================
        // Activity fields
        // =========================

        private const string FIELD_ACTIVITY_ENDDATE = "actualend";

        // =========================
        // Contact fields
        // =========================

        private const string FIELD_CONTACT_LAST_ACTIVITY = "sam_lastactivitydate";

        // =========================
        // Statuses
        // =========================

        // Position Awaiting Feedback
        private const int ACTIVITYAMB_STATUS_AWAITING_FEEDBACK = 999990003;

        // Position Finished
        private const int ACTIVITYAMB_STATUS_POSITION_FINISHED = 999990007;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                // Only Create / Update
                if (context.MessageName != "Create" &&
                    context.MessageName != "Update")
                {
                    return;
                }

                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;

                if (target == null ||
                    target.LogicalName != ENTITY_ACTIVITYAMBASSADOR)
                {
                    return;
                }

                // avoid recursion
                if (context.Depth > 1)
                    return;

                Entity preImage = null;

                if (context.PreEntityImages != null &&
                    context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                var contactRef =
                    target.GetAttributeValue<EntityReference>(FIELD_ACTIVITYAMB_CONTACT) ??
                    preImage?.GetAttributeValue<EntityReference>(FIELD_ACTIVITYAMB_CONTACT);

                if (contactRef == null)
                {
                    trace?.Trace("UpdateContactLastActivityDate: No contact found.");
                    return;
                }

                // =========================
                // Get latest completed activity date
                // =========================

                var latestDate = GetLatestActivityDateForContact(
                    service,
                    contactRef.Id);

                // =========================
                // Update contact
                // =========================

                var contactToUpdate = new Entity(ENTITY_CONTACT)
                {
                    Id = contactRef.Id
                };

                if (latestDate.HasValue)
                {
                    contactToUpdate[FIELD_CONTACT_LAST_ACTIVITY] = latestDate.Value;

                    trace?.Trace(
                        "UpdateContactLastActivityDate: Updating contact {0} with date {1}",
                        contactRef.Id,
                        latestDate.Value);
                }
                else
                {
                    // clear field if no matching activities exist
                    contactToUpdate[FIELD_CONTACT_LAST_ACTIVITY] = null;

                    trace?.Trace(
                        "UpdateContactLastActivityDate: No matching activities found for contact {0}",
                        contactRef.Id);
                }

                service.Update(contactToUpdate);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in UpdateContactLastActivityDate plugin: " + ex.Message,
                    ex);
            }
        }

        // =========================
        // Get latest activity actualend
        // =========================

        private DateTime? GetLatestActivityDateForContact(
            IOrganizationService service,
            Guid contactId)
        {
            var query = new QueryExpression(ENTITY_ACTIVITYAMBASSADOR)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };

            // =========================
            // Filters
            // =========================

            query.Criteria.AddCondition(
                FIELD_ACTIVITYAMB_CONTACT,
                ConditionOperator.Equal,
                contactId);

            // Position Awaiting Feedback OR Position Finished
            var statusFilter = new FilterExpression(LogicalOperator.Or);

            statusFilter.AddCondition(
                FIELD_ACTIVITYAMB_STATUS,
                ConditionOperator.Equal,
                ACTIVITYAMB_STATUS_AWAITING_FEEDBACK);

            statusFilter.AddCondition(
                FIELD_ACTIVITYAMB_STATUS,
                ConditionOperator.Equal,
                ACTIVITYAMB_STATUS_POSITION_FINISHED);

            query.Criteria.AddFilter(statusFilter);

            // =========================
            // Join activity
            // =========================

            var activityLink = query.AddLink(
                ENTITY_ACTIVITY,
                FIELD_ACTIVITYAMB_ACTIVITY,
                "sam_ambassadoractivityid");

            activityLink.EntityAlias = "activity";

            activityLink.Columns = new ColumnSet(FIELD_ACTIVITY_ENDDATE);

            activityLink.LinkCriteria.AddCondition(
                FIELD_ACTIVITY_ENDDATE,
                ConditionOperator.NotNull);

            // newest first
            query.AddOrder("actualend", OrderType.Descending);

            var result = service.RetrieveMultiple(query);

            var first = result.Entities.FirstOrDefault();

            if (first == null)
                return null;

            var aliasedValue =
                first.GetAttributeValue<AliasedValue>("activity.actualend");

            if (aliasedValue?.Value is DateTime latestDate)
                return latestDate;

            return null;
        }
    }
}